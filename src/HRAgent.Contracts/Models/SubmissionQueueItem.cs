using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Represents a queued timesheet submission that failed and needs to be retried
/// Stored in Cosmos DB for durability across restarts
/// </summary>
public class SubmissionQueueItem
{
    /// <summary>
    /// Unique queue item identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Employee ID (partition key for efficient queries)
    /// </summary>
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>
    /// Action to perform (clock-in, clock-out)
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Target timestamp for the action
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Original message from user
    /// </summary>
    [JsonPropertyName("userMessage")]
    public string? UserMessage { get; set; }

    /// <summary>
    /// Conversation thread ID (for traceability)
    /// </summary>
    [JsonPropertyName("conversationThreadId")]
    public string ConversationThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Message ID that triggered the action
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Number of retry attempts made
    /// </summary>
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum retry attempts allowed (default: 3)
    /// </summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Queue item status (pending, processing, completed, failed)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When this item was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this item should be retried next
    /// </summary>
    [JsonPropertyName("nextRetryAt")]
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>
    /// When this item was last processed
    /// </summary>
    [JsonPropertyName("lastProcessedAt")]
    public DateTimeOffset? LastProcessedAt { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }

    /// <summary>
    /// Last HTTP status code from Factorial HR
    /// </summary>
    [JsonPropertyName("lastStatusCode")]
    public int? LastStatusCode { get; set; }

    /// <summary>
    /// Additional context data (JSON serialized)
    /// </summary>
    [JsonPropertyName("contextData")]
    public Dictionary<string, object>? ContextData { get; set; }

    /// <summary>
    /// Time-to-live in seconds (auto-delete after 7 days)
    /// </summary>
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = 604800; // 7 days

    /// <summary>
    /// Document type discriminator for Cosmos DB queries
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "submission-queue-item";

    /// <summary>
    /// Calculates the next retry delay using exponential backoff (1s, 2s, 4s)
    /// </summary>
    public TimeSpan CalculateNextRetryDelay()
    {
        // Exponential backoff: 1s, 2s, 4s
        var delaySeconds = Math.Pow(2, RetryCount);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Checks if this item has exhausted all retry attempts
    /// </summary>
    public bool IsRetryExhausted()
    {
        return RetryCount >= MaxRetries;
    }

    /// <summary>
    /// Checks if this item is ready to be retried
    /// </summary>
    public bool IsReadyForRetry()
    {
        return Status == "pending" && 
               NextRetryAt.HasValue && 
               NextRetryAt.Value <= DateTimeOffset.UtcNow;
    }
}
