using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Immutable record of all timesheet-related actions for compliance and debugging
/// Stored in Azure Blob Storage: {yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Unique audit log entry identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Employee ID performing the action
    /// </summary>
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Action performed (clock-in, clock-out, query-status, query-historical)
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Action timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Conversation thread ID (links to Cosmos DB)
    /// </summary>
    [JsonPropertyName("conversationThreadId")]
    public string ConversationThreadId { get; set; } = string.Empty;
    
    /// <summary>
    /// Message ID that triggered the action
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// Request data sent to Factorial HR (sanitized, no PII)
    /// </summary>
    [JsonPropertyName("requestData")]
    public object? RequestData { get; set; }
    
    /// <summary>
    /// Response data received from Factorial HR (sanitized)
    /// </summary>
    [JsonPropertyName("responseData")]
    public object? ResponseData { get; set; }
    
    /// <summary>
    /// HTTP status code from Factorial HR API
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }
    
    /// <summary>
    /// Error information if action failed
    /// </summary>
    [JsonPropertyName("error")]
    public AuditError? Error { get; set; }
    
    /// <summary>
    /// Source IP address (for security auditing)
    /// </summary>
    [JsonPropertyName("sourceIp")]
    public string SourceIp { get; set; } = string.Empty;
    
    /// <summary>
    /// User agent string
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = string.Empty;
    
    /// <summary>
    /// Duration of the action in milliseconds
    /// </summary>
    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Document type discriminator
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "audit-log-entry";
}

public class AuditError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }
}
