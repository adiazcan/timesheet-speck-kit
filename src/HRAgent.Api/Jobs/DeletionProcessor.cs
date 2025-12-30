using HRAgent.Api.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Jobs;

/// <summary>
/// Background service for processing GDPR deletion requests after 30-day window
/// FR-014c: Process deletion requests within 30 days and confirm completion
/// Runs daily to check for deletion requests ready for processing
/// </summary>
public class DeletionProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeletionProcessor> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromHours(24); // Run daily
    private readonly TimeSpan _startupDelay = TimeSpan.FromMinutes(5); // Wait 5 minutes after startup

    public DeletionProcessor(
        IServiceProvider serviceProvider,
        ILogger<DeletionProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeletionProcessor starting. Will run every {Interval} hours", 
            _processingInterval.TotalHours);

        // Wait before first execution to allow services to initialize
        await Task.Delay(_startupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDeletionRequestsAsync(stoppingToken);
            }
#pragma warning disable CA1031 // Do not catch general exception types - Background service must continue running
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex, "Error in DeletionProcessor cycle");
            }

            // Wait for next processing cycle
            await Task.Delay(_processingInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Process all deletion requests that are ready (30 days elapsed)
    /// </summary>
    private async Task ProcessDeletionRequestsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DeletionProcessor: Starting deletion request processing cycle");

        using var scope = _serviceProvider.CreateScope();
        var deletionService = scope.ServiceProvider.GetRequiredService<ConversationDeletionService>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailNotificationService>();
        var deletionAuditLogger = scope.ServiceProvider.GetRequiredService<DeletionAuditLogger>();

        var requestsToProcess = await deletionService.GetRequestsReadyForProcessingAsync(cancellationToken);
        var requestList = requestsToProcess.ToList();

        if (!requestList.Any())
        {
            _logger.LogInformation("DeletionProcessor: No deletion requests ready for processing");
            return;
        }

        _logger.LogInformation("DeletionProcessor: Found {Count} deletion requests ready for processing", 
            requestList.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var request in requestList)
        {
            try
            {
                _logger.LogInformation("Processing deletion request {RequestId} for employee {EmployeeId}",
                    request.Id, request.EmployeeId);

                // Process the deletion (deletes all conversations)
                var completedRequest = await deletionService.ProcessDeletionRequestAsync(
                    request.Id,
                    request.EmployeeId,
                    cancellationToken);

                // Send confirmation email
                await emailService.SendDeletionCompletedConfirmationAsync(
                    completedRequest.EmployeeEmail,
                    completedRequest.EmployeeName ?? "User",
                    completedRequest.Id,
                    completedRequest.ConversationsDeleted,
                    cancellationToken);

                // Log email sent to audit trail
                await deletionAuditLogger.LogConfirmationEmailSentAsync(
                    completedRequest.Id,
                    completedRequest.EmployeeId,
                    completedRequest.EmployeeEmail,
                    cancellationToken);

                successCount++;
                _logger.LogInformation("Successfully processed deletion request {RequestId}. Deleted {Count} conversations",
                    completedRequest.Id, completedRequest.ConversationsDeleted);
            }
#pragma warning disable CA1031 // Do not catch general exception types - Must continue processing remaining deletion requests
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                failureCount++;
                _logger.LogError(ex, "Failed to process deletion request {RequestId} for employee {EmployeeId}",
                    request.Id, request.EmployeeId);
                // Continue processing other requests even if one fails
            }
        }

        _logger.LogInformation("DeletionProcessor: Completed cycle. Success: {SuccessCount}, Failures: {FailureCount}",
            successCount, failureCount);
    }
}
