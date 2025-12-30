using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Individual message within a conversation (user or assistant)
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// Unique message identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Message role: "user" (employee) or "assistant" (agent)
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
    
    /// <summary>
    /// Message text content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Message creation timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Detected intent (clock-in, clock-out, status-query, historical-query, chitchat)
    /// Null for assistant messages
    /// </summary>
    [JsonPropertyName("intent")]
    public string? Intent { get; set; }
    
    /// <summary>
    /// Intent classification confidence score (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("intentConfidence")]
    public double? IntentConfidence { get; set; }
    
    /// <summary>
    /// Tool calls executed for this message (e.g., Factorial HR API calls)
    /// </summary>
    [JsonPropertyName("toolCalls")]
    public List<ToolCall>? ToolCalls { get; set; }
    
    /// <summary>
    /// Additional metadata (source IP, user agent, device type, etc.)
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Record of external API calls made during message processing
/// </summary>
public class ToolCall
{
    /// <summary>
    /// Unique tool call identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Tool name (e.g., "factorial_clock_in", "factorial_query_timesheet")
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Tool call start timestamp
    /// </summary>
    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Tool call end timestamp
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTimeOffset? EndTime { get; set; }
    
    /// <summary>
    /// Input parameters sent to the tool
    /// </summary>
    [JsonPropertyName("input")]
    public object? Input { get; set; }
    
    /// <summary>
    /// Output returned by the tool
    /// </summary>
    [JsonPropertyName("output")]
    public object? Output { get; set; }
    
    /// <summary>
    /// Error information if tool call failed
    /// </summary>
    [JsonPropertyName("error")]
    public ToolCallError? Error { get; set; }
    
    /// <summary>
    /// Tool call status (pending, success, failed)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";
}

public class ToolCallError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("details")]
    public object? Details { get; set; }
}
