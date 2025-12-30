using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for managing GDPR conversation deletion requests
/// FR-014b: Employee self-service mechanism to request conversation data deletion
/// FR-014c: Process deletion requests within 30 days and confirm completion
/// </summary>
public class ConversationDeletionService
{
    private readonly IConversationStore _conversationStore;
    private readonly DeletionAuditLogger _deletionAuditLogger;
    private readonly ILogger<ConversationDeletionService> _logger;

    public ConversationDeletionService(
        IConversationStore conversationStore,
        DeletionAuditLogger deletionAuditLogger,
        ILogger<ConversationDeletionService> logger)
    {
        _conversationStore = conversationStore;
        _deletionAuditLogger = deletionAuditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Submit a new deletion request for an employee
    /// </summary>
    public async Task<ConversationDeletionRequest> SubmitDeletionRequestAsync(
        string employeeId,
        string employeeEmail,
        string? employeeName,
        string? requestOriginIp,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Submitting deletion request for employee {EmployeeId}", employeeId);

        // Check if there's already a pending deletion request
        var existingRequest = await GetPendingDeletionRequestAsync(employeeId, cancellationToken);
        if (existingRequest != null)
        {
            _logger.LogWarning("Employee {EmployeeId} already has pending deletion request {RequestId}", 
                employeeId, existingRequest.Id);
            throw new InvalidOperationException(
                $"A deletion request is already pending for this employee. Scheduled for: {existingRequest.ScheduledDeletionDate:yyyy-MM-dd}");
        }

        // Create deletion request with 30-day processing window (FR-014c)
        var deletionRequest = new ConversationDeletionRequest
        {
            EmployeeId = employeeId,
            EmployeeEmail = employeeEmail,
            EmployeeName = employeeName,
            RequestedAt = DateTimeOffset.UtcNow,
            ScheduledDeletionDate = DateTimeOffset.UtcNow.AddDays(30),
            Status = DeletionRequestStatus.Pending,
            RequestOriginIp = requestOriginIp
        };

        // Store in Cosmos DB DeletionRequests collection
        await _conversationStore.SaveDeletionRequestAsync(deletionRequest, cancellationToken);

        // Log to deletion audit trail (separate from conversation audit logs per FR-014d)
        await _deletionAuditLogger.LogDeletionRequestSubmittedAsync(
            deletionRequest.Id,
            employeeId,
            requestOriginIp,
            cancellationToken);

        _logger.LogInformation("Deletion request {RequestId} submitted for employee {EmployeeId}, scheduled for {ScheduledDate}",
            deletionRequest.Id, employeeId, deletionRequest.ScheduledDeletionDate);

        return deletionRequest;
    }

    /// <summary>
    /// Cancel a pending deletion request before processing
    /// </summary>
    public async Task<ConversationDeletionRequest> CancelDeletionRequestAsync(
        string requestId,
        string employeeId,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        var request = await _conversationStore.GetDeletionRequestAsync(requestId, employeeId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Deletion request {requestId} not found");
        }

        if (!request.CanBeCancelled())
        {
            throw new InvalidOperationException(
                $"Cannot cancel deletion request in status: {request.Status}");
        }

        request.Status = DeletionRequestStatus.Cancelled;
        request.CancellationReason = cancellationReason;
        request.CompletedAt = DateTimeOffset.UtcNow;

        await _conversationStore.UpdateDeletionRequestAsync(request, cancellationToken);

        await _deletionAuditLogger.LogDeletionRequestCancelledAsync(
            requestId,
            employeeId,
            cancellationReason,
            cancellationToken);

        _logger.LogInformation("Deletion request {RequestId} cancelled for employee {EmployeeId}", 
            requestId, employeeId);

        return request;
    }

    /// <summary>
    /// Process a deletion request (delete all conversations)
    /// Called by background job after 30-day window
    /// </summary>
    public async Task<ConversationDeletionRequest> ProcessDeletionRequestAsync(
        string requestId,
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing deletion request {RequestId} for employee {EmployeeId}",
            requestId, employeeId);

        var request = await _conversationStore.GetDeletionRequestAsync(requestId, employeeId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Deletion request {requestId} not found");
        }

        if (!request.IsReadyForProcessing())
        {
            throw new InvalidOperationException(
                $"Deletion request {requestId} is not ready for processing. Status: {request.Status}, Scheduled: {request.ScheduledDeletionDate}");
        }

        try
        {
            // Update status to Processing
            request.Status = DeletionRequestStatus.Processing;
            await _conversationStore.UpdateDeletionRequestAsync(request, cancellationToken);

            // Delete all conversations for employee (audit logs are preserved per FR-014d)
            var deletedCount = await _conversationStore.DeleteAllConversationsAsync(employeeId, cancellationToken);

            // Update request with completion details
            request.Status = DeletionRequestStatus.Completed;
            request.CompletedAt = DateTimeOffset.UtcNow;
            request.ConversationsDeleted = deletedCount;

            await _conversationStore.UpdateDeletionRequestAsync(request, cancellationToken);

            // Log to deletion audit trail
            await _deletionAuditLogger.LogDeletionCompletedAsync(
                requestId,
                employeeId,
                deletedCount,
                cancellationToken);

            _logger.LogInformation("Deletion request {RequestId} completed. Deleted {Count} conversations for employee {EmployeeId}",
                requestId, deletedCount, employeeId);

            return request;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deletion request {RequestId} for employee {EmployeeId}",
                requestId, employeeId);

            request.Status = DeletionRequestStatus.Failed;
            request.ErrorMessage = ex.Message;
            await _conversationStore.UpdateDeletionRequestAsync(request, cancellationToken);

            await _deletionAuditLogger.LogDeletionFailedAsync(
                requestId,
                employeeId,
                ex.Message,
                cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Get deletion request status for an employee
    /// </summary>
    public async Task<ConversationDeletionRequest?> GetDeletionRequestAsync(
        string requestId,
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        return await _conversationStore.GetDeletionRequestAsync(requestId, employeeId, cancellationToken);
    }

    /// <summary>
    /// Get pending deletion request for employee (if any)
    /// </summary>
    public async Task<ConversationDeletionRequest?> GetPendingDeletionRequestAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _conversationStore.GetDeletionRequestsByEmployeeAsync(employeeId, cancellationToken);
        return requests.FirstOrDefault(r => r.Status == DeletionRequestStatus.Pending);
    }

    /// <summary>
    /// Get all deletion requests ready for processing (30 days elapsed)
    /// Called by background job
    /// </summary>
    public async Task<IEnumerable<ConversationDeletionRequest>> GetRequestsReadyForProcessingAsync(
        CancellationToken cancellationToken = default)
    {
        var allRequests = await _conversationStore.GetAllPendingDeletionRequestsAsync(cancellationToken);
        return allRequests.Where(r => r.IsReadyForProcessing());
    }
}
