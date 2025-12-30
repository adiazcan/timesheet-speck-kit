using System.Text.Json.Serialization;

namespace HRAgent.Contracts.AgUI;

/// <summary>
/// Base interface for all AG-UI events streamed from backend to frontend
/// </summary>
public abstract class AGUIEvent
{
    /// <summary>
    /// Event type discriminator
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
    
    /// <summary>
    /// Unix timestamp in milliseconds
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

// ========================================
// Message Events
// ========================================

/// <summary>
/// Signals the start of an assistant message
/// </summary>
public class MessageStartEvent : AGUIEvent
{
    public override string Type => "message.start";
    
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}

/// <summary>
/// Streams incremental content chunks of the assistant message
/// </summary>
public class MessageContentEvent : AGUIEvent
{
    public override string Type => "message.content";
    
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// Incremental text chunk
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Signals the completion of an assistant message
/// </summary>
public class MessageEndEvent : AGUIEvent
{
    public override string Type => "message.end";
    
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
    
    [JsonPropertyName("metadata")]
    public MessageMetadata? Metadata { get; set; }
}

public class MessageMetadata
{
    [JsonPropertyName("intent")]
    public string? Intent { get; set; }
    
    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }
    
    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; set; }
}

// ========================================
// Tool Call Events
// ========================================

/// <summary>
/// Signals the start of an external API call (e.g., Factorial HR)
/// </summary>
public class ToolCallStartEvent : AGUIEvent
{
    public override string Type => "tool_call.start";
    
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;
    
    /// <summary>
    /// Tool name (e.g., 'factorial_clock_in', 'factorial_query_timesheet')
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Input parameters sent to the tool
    /// </summary>
    [JsonPropertyName("input")]
    public object? Input { get; set; }
}

/// <summary>
/// Signals the completion of an external API call
/// </summary>
public class ToolCallEndEvent : AGUIEvent
{
    public override string Type => "tool_call.end";
    
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;
    
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

// ========================================
// State Events
// ========================================

/// <summary>
/// Complete snapshot of conversation state
/// </summary>
public class StateSnapshotEvent : AGUIEvent
{
    public override string Type => "state.snapshot";
    
    [JsonPropertyName("state")]
    public ConversationStateSnapshot State { get; set; } = new();
}

public class ConversationStateSnapshot
{
    [JsonPropertyName("isClockedIn")]
    public bool IsClockedIn { get; set; }
    
    [JsonPropertyName("lastClockIn")]
    public string? LastClockIn { get; set; } // ISO 8601
    
    [JsonPropertyName("lastClockOut")]
    public string? LastClockOut { get; set; } // ISO 8601
    
    [JsonPropertyName("currentActivity")]
    public string? CurrentActivity { get; set; }
}

/// <summary>
/// Incremental state change (JSON Patch format)
/// </summary>
public class StateDeltaEvent : AGUIEvent
{
    public override string Type => "state.delta";
    
    /// <summary>
    /// JSON Patch operations (RFC 6902)
    /// </summary>
    [JsonPropertyName("patch")]
    public List<JsonPatchOperation> Patch { get; set; } = new();
}

public class JsonPatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = string.Empty; // "add", "remove", "replace"
    
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

// ========================================
// Activity Events
// ========================================

/// <summary>
/// Signals the start of a long-running activity
/// </summary>
public class ActivityStartEvent : AGUIEvent
{
    public override string Type => "activity.start";
    
    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Signals the completion of an activity
/// </summary>
public class ActivityEndEvent : AGUIEvent
{
    public override string Type => "activity.end";
    
    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; } = string.Empty;
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

// ========================================
// Error Events
// ========================================

/// <summary>
/// Signals an error during conversation processing
/// </summary>
public class ErrorEvent : AGUIEvent
{
    public override string Type => "error";
    
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("details")]
    public object? Details { get; set; }
    
    [JsonPropertyName("recoverable")]
    public bool Recoverable { get; set; }
}
