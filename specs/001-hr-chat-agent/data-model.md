# Data Model: HR Chat Agent for Timesheet Management

**Feature**: 001-hr-chat-agent  
**Date**: 2025-12-30  
**Status**: Design Complete

## Overview

This document defines the data models for the HR Chat Agent application, covering conversation state (Cosmos DB), audit logs (Blob Storage), and domain entities for integration with Factorial HR API.

---

## 1. Conversation Data Models (Cosmos DB)

### ConversationThread

Primary entity representing an employee's conversation session with the HR agent.

**Container**: `conversations`  
**Partition Key**: `/employeeId`  
**TTL**: Disabled by default (permanent retention per FR-015a, GDPR deletion via manual process)

```csharp
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
    public string EmployeeId { get; set; }
    
    /// <summary>
    /// Session identifier for grouping related conversations (browser session, day, etc.)
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; }
    
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
    public int? Ttl { get; set; } = null; // null = permanent, override for temporary conversations
    
    /// <summary>
    /// Document type discriminator for Cosmos DB queries
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = "conversation-thread";
}
```

**Indexes:**
- Primary: `/id` (automatic)
- Partition: `/employeeId`
- Composite: `[/employeeId, /updatedAt DESC]` - Recent conversations per employee
- Composite: `[/sessionId, /createdAt ASC]` - Session-based queries
- Single: `/state/isClockedIn` - Quick clock-in status lookups

**Validation Rules:**
- `employeeId`: Required, non-empty, matches Factorial HR ID format
- `messages`: Maximum 100 messages per thread (for performance)
- `ttl`: Between 1 day (86,400) and 90 days (7,776,000)

**State Transitions:**
- New thread: `state.isClockedIn = false`, `state.lastIntent = null`
- After clock-in: `state.isClockedIn = true`, `state.lastClockIn = <timestamp>`
- After clock-out: `state.isClockedIn = false`, `state.lastClockOut = <timestamp>`

---

### ConversationMessage

Individual message within a conversation (user or assistant).

```csharp
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
    public string Role { get; set; }
    
    /// <summary>
    /// Message text content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }
    
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
```

**Validation Rules:**
- `role`: Must be "user" or "assistant"
- `content`: Required, 1-2000 characters
- `intent`: Valid values: "clock-in", "clock-out", "status-query", "historical-query", "chitchat", null
- `intentConfidence`: Range 0.0-1.0, required if intent is not null

---

### ConversationState

Current state of the conversation and employee's timesheet status.

```csharp
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
```

---

### ToolCall

Record of external API calls made during message processing.

```csharp
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
    public string Name { get; set; }
    
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
    public string Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; }
    
    [JsonPropertyName("details")]
    public object? Details { get; set; }
}
```

---

### UserMetadata

Employee information cached from Factorial HR for personalization.

```csharp
public class UserMetadata
{
    /// <summary>
    /// Employee full name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    /// <summary>
    /// Employee email address
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; }
    
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
```

---

## 2. Audit Log Models (Blob Storage)

### AuditLogEntry

Immutable record of all timesheet-related actions for compliance and debugging.

**Container**: `audit-logs`  
**Path Structure**: `{yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json`  
**Lifecycle Policy**: Cool tier after 90 days, Archive tier after 1 year

```csharp
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
    public string EmployeeId { get; set; }
    
    /// <summary>
    /// Action performed (clock-in, clock-out, query-status, query-historical)
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; }
    
    /// <summary>
    /// Action timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Conversation thread ID (links to Cosmos DB)
    /// </summary>
    [JsonPropertyName("conversationThreadId")]
    public string ConversationThreadId { get; set; }
    
    /// <summary>
    /// Message ID that triggered the action
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; }
    
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
    public string SourceIp { get; set; }
    
    /// <summary>
    /// User agent string
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; }
    
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
    public string Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; }
    
    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }
}
```

**Validation Rules:**
- `employeeId`: Required, matches Factorial HR ID format
- `action`: Required, valid values: "clock-in", "clock-out", "query-status", "query-historical"
- `timestamp`: Required, UTC timezone
- `sourceIp`: Required, valid IPv4/IPv6 format

**Retention Policy:**
- Hot tier: 0-90 days (immediate access)
- Cool tier: 91-365 days (infrequent access, lower cost)
- Archive tier: 365+ days (rare access, lowest cost)
- Deletion: After 7 years (compliance requirement)

---

## 3. Factorial HR Integration Models

### TimesheetEntry

Domain model representing a timesheet entry from Factorial HR.

```csharp
public class TimesheetEntry
{
    /// <summary>
    /// Factorial HR timesheet entry ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    /// <summary>
    /// Employee ID (Factorial HR)
    /// </summary>
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; }
    
    /// <summary>
    /// Date of the timesheet entry (YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Clock-in timestamp
    /// </summary>
    [JsonPropertyName("clockIn")]
    public DateTimeOffset? ClockIn { get; set; }
    
    /// <summary>
    /// Clock-out timestamp (null if still clocked in)
    /// </summary>
    [JsonPropertyName("clockOut")]
    public DateTimeOffset? ClockOut { get; set; }
    
    /// <summary>
    /// Total hours worked (calculated from clock-in/out)
    /// </summary>
    [JsonPropertyName("totalHours")]
    public decimal? TotalHours { get; set; }
    
    /// <summary>
    /// Timesheet entry status (draft, submitted, approved)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    /// <summary>
    /// Notes or comments (optional)
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
```

---

### ClockInRequest

Request payload for clocking in via Factorial HR API.

```csharp
public class ClockInRequest
{
    /// <summary>
    /// Employee ID
    /// </summary>
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; }
    
    /// <summary>
    /// Clock-in timestamp (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
```

---

### ClockOutRequest

Request payload for clocking out via Factorial HR API.

```csharp
public class ClockOutRequest
{
    /// <summary>
    /// Employee ID
    /// </summary>
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; }
    
    /// <summary>
    /// Clock-out timestamp (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
```

---

### TimesheetQuery

Query parameters for retrieving historical timesheet data.

```csharp
public class TimesheetQuery
{
    /// <summary>
    /// Employee ID
    /// </summary>
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; }
    
    /// <summary>
    /// Start date (inclusive, YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("start_date")]
    public DateOnly StartDate { get; set; }
    
    /// <summary>
    /// End date (inclusive, YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("end_date")]
    public DateOnly EndDate { get; set; }
    
    /// <summary>
    /// Pagination: Page number (1-indexed)
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Pagination: Results per page
    /// </summary>
    [JsonPropertyName("page_size")]
    public int PageSize { get; set; } = 30;
}
```

**Validation Rules:**
- `startDate`: Cannot be more than 2 years in the past
- `endDate`: Cannot be in the future
- `endDate`: Must be >= `startDate`
- Date range: Maximum 90 days per query (pagination required for longer ranges)
- `pageSize`: Between 1 and 100

---

## 4. AG-UI Protocol Models (Frontend-Backend)

### ConversationRequest

Request from frontend to backend to send a user message.

```typescript
interface ConversationRequest {
  /** User message content */
  message: string;
  
  /** Session ID for conversation continuity */
  sessionId: string;
  
  /** Employee ID (from authentication) */
  employeeId: string;
  
  /** Optional conversation thread ID (for resuming existing conversation) */
  threadId?: string;
  
  /** Client metadata (device, user agent, etc.) */
  metadata?: {
    userAgent: string;
    deviceType: 'desktop' | 'mobile' | 'tablet';
    timezone: string;
  };
}
```

---

### AGUIEvent (Base)

Base interface for all AG-UI events streamed from backend to frontend.

```typescript
interface AGUIEvent {
  /** Event type */
  type: string;
  
  /** Unix timestamp (milliseconds) */
  timestamp: number;
  
  /** Transport-specific data (optional) */
  rawEvent?: unknown;
}
```

---

### MessageEvents

Events related to agent message streaming.

```typescript
interface MessageStartEvent extends AGUIEvent {
  type: 'message.start';
  messageId: string;
}

interface MessageContentEvent extends AGUIEvent {
  type: 'message.content';
  messageId: string;
  content: string; // Incremental text chunk
}

interface MessageEndEvent extends AGUIEvent {
  type: 'message.end';
  messageId: string;
  metadata?: {
    intent?: string;
    confidence?: number;
    totalTokens?: number;
  };
}
```

---

### ToolCallEvents

Events related to external API calls (Factorial HR).

```typescript
interface ToolCallStartEvent extends AGUIEvent {
  type: 'tool_call.start';
  toolCallId: string;
  name: string; // e.g., 'factorial_clock_in'
  input?: unknown; // Call parameters
}

interface ToolCallEndEvent extends AGUIEvent {
  type: 'tool_call.end';
  toolCallId: string;
  output?: unknown; // API response
  error?: {
    code: string;
    message: string;
  };
}
```

---

### StateEvents

Events for conversation state synchronization.

```typescript
interface StateSnapshotEvent extends AGUIEvent {
  type: 'state.snapshot';
  state: {
    isClockedIn: boolean;
    lastClockIn?: string; // ISO 8601
    lastClockOut?: string; // ISO 8601
    currentActivity?: string;
  };
}

interface StateDeltaEvent extends AGUIEvent {
  type: 'state.delta';
  patch: JsonPatch[]; // RFC 6902 JSON Patch operations
}
```

---

## 5. Index Strategy

### Cosmos DB Indexes

**conversations Container:**
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    { "path": "/*" }
  ],
  "excludedPaths": [
    { "path": "/messages/*/content/*" },
    { "path": "/messages/*/metadata/*" }
  ],
  "compositeIndexes": [
    [
      { "path": "/employeeId", "order": "ascending" },
      { "path": "/updatedAt", "order": "descending" }
    ],
    [
      { "path": "/sessionId", "order": "ascending" },
      { "path": "/createdAt", "order": "ascending" }
    ]
  ]
}
```

**Query Patterns:**
- Get recent conversations for employee: `SELECT * FROM c WHERE c.employeeId = @id ORDER BY c.updatedAt DESC`
- Get conversation by session: `SELECT * FROM c WHERE c.sessionId = @sessionId`
- Get clocked-in users: `SELECT * FROM c WHERE c.state.isClockedIn = true`

---

## 6. Performance Considerations

### Cosmos DB
- **Partition Strategy**: Partition by `employeeId` ensures all employee conversations colocated
- **RU Consumption**: Estimated 5 RUs per read, 10 RUs per write
- **Throughput**: Serverless tier for initial deployment (pay-per-request)
- **Scaling**: Move to provisioned throughput (400+ RUs) if >100 concurrent users

### Blob Storage
- **Append Blobs**: Use for streaming audit logs (more efficient than block blobs for sequential writes)
- **Hierarchical Namespace**: Enables efficient date-based queries
- **Access Tier**: Hot for recent logs, lifecycle policy for automatic cool/archive transitions

### Caching
- **Redis**: Cache Factorial HR responses (5-minute TTL)
- **MemoryCache**: Cache Cosmos DB queries in .NET (1-minute TTL)
- **Browser**: Cache conversation history (session storage)

---

## Conclusion

Data models designed for:
- **Cosmos DB**: Flexible conversation storage with efficient queries
- **Blob Storage**: Immutable audit logs with compliance-friendly retention
- **Factorial HR**: Clean integration contracts
- **AG-UI**: Standardized frontend-backend event streaming

All models support the 4 user stories (P1-P4) with validation rules, indexes, and performance optimizations.

**Data Model Status**: ✅ Complete  
**Next Phase**: Generate API contracts (OpenAPI + AG-UI specifications)
