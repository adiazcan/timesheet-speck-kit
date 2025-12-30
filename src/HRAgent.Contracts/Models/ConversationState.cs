using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Current state of the conversation and employee's timesheet status
/// </summary>
public class ConversationState
{
    /// <summary>
    /// Whether employee is currently clocked in (fetched from Factorial HR)
    /// </summary>
    [JsonPropertyName("isClockedIn")]
    public bool IsClockedIn { get; set; } = false;
    
    /// <summary>
    /// Last clock-in timestamp (null if never clocked in or currently clocked out)
    /// </summary>
    [JsonPropertyName("lastClockIn")]
    public DateTimeOffset? LastClockIn { get; set; }
    
    /// <summary>
    /// Last clock-out timestamp (null if never clocked out or currently clocked in)
    /// </summary>
    [JsonPropertyName("lastClockOut")]
    public DateTimeOffset? LastClockOut { get; set; }
    
    /// <summary>
    /// Last detected intent (for conversation context)
    /// </summary>
    [JsonPropertyName("lastIntent")]
    public string? LastIntent { get; set; }
    
    /// <summary>
    /// Current activity (e.g., "fetching_timesheet", "processing_clock_in")
    /// Used for displaying progress indicators
    /// </summary>
    [JsonPropertyName("currentActivity")]
    public string? CurrentActivity { get; set; }
    
    /// <summary>
    /// Short-term memory for contextual follow-up questions
    /// Example: Last queried date range for "show me last week" -> "what about this week?"
    /// </summary>
    [JsonPropertyName("contextMemory")]
    public Dictionary<string, object>? ContextMemory { get; set; }
}

/// <summary>
/// Employee information cached from Factorial HR for personalization
/// </summary>
public class UserMetadata
{
    /// <summary>
    /// Employee full name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Employee email address
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Employee timezone (e.g., "America/New_York")
    /// Used for formatting timestamps in user's local time
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";
    
    /// <summary>
    /// Preferred language (e.g., "en", "es")
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
    
    /// <summary>
    /// Employee department
    /// </summary>
    [JsonPropertyName("department")]
    public string? Department { get; set; }
}
