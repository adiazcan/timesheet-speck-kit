using System.Text.Json.Serialization;

namespace HRAgent.Contracts.AgUI;

/// <summary>
/// Request from frontend to backend to send a user message
/// Follows AG-UI protocol specification
/// </summary>
public class ConversationRequest
{
    /// <summary>
    /// User message content
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Session ID for conversation continuity
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Employee ID (from authentication)
    /// </summary>
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional conversation thread ID (for resuming existing conversation)
    /// </summary>
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }
    
    /// <summary>
    /// User timezone (IANA timezone identifier, e.g., "America/New_York")
    /// Used for parsing relative dates and displaying timestamps
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
    
    /// <summary>
    /// Client metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public ConversationMetadata? Metadata { get; set; }
}

/// <summary>
/// Client metadata for conversation context
/// </summary>
public class ConversationMetadata
{
    /// <summary>
    /// User agent string
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Device type
    /// </summary>
    [JsonPropertyName("deviceType")]
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// Browser timezone (detected client-side)
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
}
