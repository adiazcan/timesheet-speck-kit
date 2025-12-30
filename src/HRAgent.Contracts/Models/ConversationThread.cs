using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Primary entity representing an employee's conversation session with the HR agent
/// Stored in Cosmos DB (conversations container) partitioned by employeeId
/// </summary>
public class ConversationThread
{
    /// <summary>
    /// Unique conversation thread identifier (GUID)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Factorial HR employee ID (partition key for efficient queries)
    /// </summary>
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Session identifier for grouping related conversations (browser session, day, etc.)
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Ordered list of messages in this conversation
    /// </summary>
    [JsonPropertyName("messages")]
    public List<ConversationMessage> Messages { get; set; } = new();
    
    /// <summary>
    /// Current conversation state (clock-in status, last intent, etc.)
    /// </summary>
    [JsonPropertyName("state")]
    public ConversationState State { get; set; } = new();
    
    /// <summary>
    /// User metadata (name, timezone, preferences)
    /// </summary>
    [JsonPropertyName("userMetadata")]
    public UserMetadata? UserMetadata { get; set; }
    
    /// <summary>
    /// Conversation creation timestamp
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Last update timestamp (for indexing recent conversations)
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Time-to-live in seconds (null = permanent retention per FR-015a and GDPR requirements)
    /// Conversations only deleted via explicit user deletion request (FR-015b-c)
    /// </summary>
    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; } = null;
    
    /// <summary>
    /// Document type discriminator for Cosmos DB queries
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "conversation-thread";
}
