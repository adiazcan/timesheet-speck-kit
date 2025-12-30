using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HRAgent.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HRAgent.Api.Services;

/// <summary>
/// Separate audit logger for GDPR deletion operations
/// FR-014d: Retention of audit logs for 7 years regardless of conversation deletion
/// Deletion audit logs are kept separate from conversation audit logs
/// </summary>
public class DeletionAuditLogger
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<DeletionAuditLogger> _logger;
    private const string ContainerName = "deletion-audit-logs";

    public DeletionAuditLogger(
        BlobServiceClient blobServiceClient,
        ILogger<DeletionAuditLogger> logger)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        _logger = logger;
    }

    /// <summary>
    /// Initialize container on startup (creates if doesn't exist)
    /// </summary>
    public async Task EnsureContainerExistsAsync(CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Log deletion request submission
    /// </summary>
    public async Task LogDeletionRequestSubmittedAsync(
        string requestId,
        string employeeId,
        string? requestOriginIp,
        CancellationToken cancellationToken = default)
    {
        var entry = new DeletionAuditLogEntry
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Action = "DeletionRequestSubmitted",
            Details = $"Employee submitted deletion request from IP: {requestOriginIp ?? "unknown"}",
            IpAddress = requestOriginIp
        };

        await WriteAuditLogAsync(entry, cancellationToken);
    }

    /// <summary>
    /// Log deletion request cancellation
    /// </summary>
    public async Task LogDeletionRequestCancelledAsync(
        string requestId,
        string employeeId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var entry = new DeletionAuditLogEntry
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Action = "DeletionRequestCancelled",
            Details = $"Deletion request cancelled. Reason: {reason}"
        };

        await WriteAuditLogAsync(entry, cancellationToken);
    }

    /// <summary>
    /// Log successful deletion completion
    /// </summary>
    public async Task LogDeletionCompletedAsync(
        string requestId,
        string employeeId,
        int conversationsDeleted,
        CancellationToken cancellationToken = default)
    {
        var entry = new DeletionAuditLogEntry
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Action = "DeletionCompleted",
            Details = $"Successfully deleted {conversationsDeleted} conversations",
            ConversationsDeleted = conversationsDeleted
        };

        await WriteAuditLogAsync(entry, cancellationToken);
    }

    /// <summary>
    /// Log deletion failure
    /// </summary>
    public async Task LogDeletionFailedAsync(
        string requestId,
        string employeeId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var entry = new DeletionAuditLogEntry
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Action = "DeletionFailed",
            Details = $"Deletion processing failed: {errorMessage}",
            ErrorMessage = errorMessage
        };

        await WriteAuditLogAsync(entry, cancellationToken);
    }

    /// <summary>
    /// Log confirmation email sent
    /// </summary>
    public async Task LogConfirmationEmailSentAsync(
        string requestId,
        string employeeId,
        string emailAddress,
        CancellationToken cancellationToken = default)
    {
        var entry = new DeletionAuditLogEntry
        {
            RequestId = requestId,
            EmployeeId = employeeId,
            Action = "ConfirmationEmailSent",
            Details = $"Deletion confirmation email sent to {emailAddress}"
        };

        await WriteAuditLogAsync(entry, cancellationToken);
    }

    /// <summary>
    /// Write audit log entry to blob storage
    /// Path: deletion-audit-logs/{yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json
    /// </summary>
    private async Task WriteAuditLogAsync(
        DeletionAuditLogEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var blobName = $"{now:yyyy}/{now:MM}/{now:dd}/{entry.EmployeeId}_{now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.json";

            var blobClient = _containerClient.GetBlobClient(blobName);

            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(
                stream,
                new BlobHttpHeaders { ContentType = "application/json" },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Deletion audit log written: {BlobName}", blobName);
        }
#pragma warning disable CA1031 // Do not catch general exception types - Audit logging failure should not break deletion process
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, "Failed to write deletion audit log for request {RequestId}", entry.RequestId);
            // Don't throw - audit logging failure should not break deletion process
        }
    }
}

/// <summary>
/// Model for deletion audit log entries
/// </summary>
public class DeletionAuditLogEntry
{
    public string RequestId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ConversationsDeleted { get; set; }
}
