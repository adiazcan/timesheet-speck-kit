using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Represents an employee's request to delete their conversation data (GDPR Right to be Forgotten)
/// FR-014b: Employee self-service mechanism to request conversation data deletion
/// FR-014c: Process deletion requests within 30 days and confirm completion
/// </summary>
public class ConversationDeletionRequest
{
    /// <summary>
    /// Unique identifier for the deletion request
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Employee ID from Entra ID (employee requesting deletion)
    /// </summary>
    [JsonPropertyName("employeeId")]
    public required string EmployeeId { get; set; }

    /// <summary>
    /// Employee email address for confirmation notifications
    /// </summary>
    [JsonPropertyName("employeeEmail")]
    public required string EmployeeEmail { get; set; }

    /// <summary>
    /// Employee name for personalized communications
    /// </summary>
    [JsonPropertyName("employeeName")]
    public string? EmployeeName { get; set; }

    /// <summary>
    /// Timestamp when deletion request was submitted (UTC)
    /// </summary>
    [JsonPropertyName("requestedAt")]
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Status of the deletion request
    /// </summary>
    [JsonPropertyName("status")]
    public DeletionRequestStatus Status { get; set; } = DeletionRequestStatus.Pending;

    /// <summary>
    /// Scheduled date for deletion processing (requestedAt + 30 days per FR-014c)
    /// </summary>
    [JsonPropertyName("scheduledDeletionDate")]
    public DateTimeOffset ScheduledDeletionDate { get; set; }

    /// <summary>
    /// Actual timestamp when deletion was completed (null if not yet processed)
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Number of conversation threads deleted
    /// </summary>
    [JsonPropertyName("conversationsDeleted")]
    public int ConversationsDeleted { get; set; }

    /// <summary>
    /// Reason for cancellation (if status = Cancelled)
    /// </summary>
    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Error message if deletion failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// IP address of request origin (for audit trail)
    /// </summary>
    [JsonPropertyName("requestOriginIp")]
    public string? RequestOriginIp { get; set; }

    /// <summary>
    /// Confirmation email sent timestamp
    /// </summary>
    [JsonPropertyName("confirmationEmailSentAt")]
    public DateTimeOffset? ConfirmationEmailSentAt { get; set; }

    /// <summary>
    /// Time-to-live for deletion request record (7 years in seconds = 220752000)
    /// Deletion requests are auditable records, retained per FR-014d
    /// </summary>
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = 220752000; // 7 years in seconds

    /// <summary>
    /// Cosmos DB partition key (same as employeeId for efficient querying)
    /// </summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey => EmployeeId;

    /// <summary>
    /// Check if deletion request is due for processing (30 days elapsed)
    /// </summary>
    public bool IsReadyForProcessing()
    {
        return Status == DeletionRequestStatus.Pending 
               && DateTimeOffset.UtcNow >= ScheduledDeletionDate;
    }

    /// <summary>
    /// Check if request can be cancelled (still in pending state)
    /// </summary>
    public bool CanBeCancelled()
    {
        return Status == DeletionRequestStatus.Pending;
    }

    /// <summary>
    /// Calculate days remaining until scheduled deletion
    /// </summary>
    public int DaysUntilDeletion()
    {
        var remaining = ScheduledDeletionDate - DateTimeOffset.UtcNow;
        return Math.Max(0, (int)remaining.TotalDays);
    }
}

/// <summary>
/// Status values for deletion requests
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeletionRequestStatus
{
    /// <summary>
    /// Request submitted, awaiting 30-day processing window
    /// </summary>
    Pending,

    /// <summary>
    /// Deletion in progress (background job processing)
    /// </summary>
    Processing,

    /// <summary>
    /// Deletion completed successfully, confirmation email sent
    /// </summary>
    Completed,

    /// <summary>
    /// Deletion cancelled by employee or administrator before processing
    /// </summary>
    Cancelled,

    /// <summary>
    /// Deletion failed due to error (requires manual intervention)
    /// </summary>
    Failed
}
