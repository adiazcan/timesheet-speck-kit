using HRAgent.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for managing and tracking active user sessions
/// Detects session collisions when employees use multiple devices simultaneously
/// </summary>
public class SessionManager
{
    private readonly ConversationStore _conversationStore;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ConversationStore conversationStore, ILogger<SessionManager> logger)
    {
        _conversationStore = conversationStore;
        _logger = logger;
    }

    /// <summary>
    /// Checks if an employee has multiple active sessions
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="currentSessionId">Current session identifier</param>
    /// <returns>List of active sessions found (including current)</returns>
    public async Task<List<ActiveSession>> GetActiveSessionsAsync(string employeeId, string currentSessionId)
    {
        try
        {
            // Get recent threads (last 10) to check for active sessions
            var recentThreads = await _conversationStore.GetRecentThreadsAsync(employeeId, limit: 10);
            
            // Consider a session active if it was updated within the last 30 minutes
            var activeThreshold = DateTimeOffset.UtcNow.AddMinutes(-30);
            
            var activeSessions = recentThreads
                .Where(t => t.UpdatedAt >= activeThreshold && !string.IsNullOrEmpty(t.SessionId))
                .GroupBy(t => t.SessionId)
                .Select(g => new ActiveSession
                {
                    SessionId = g.Key,
                    LastActivity = g.Max(t => t.UpdatedAt),
                    ThreadCount = g.Count(),
                    IsCurrent = g.Key == currentSessionId,
                    DeviceInfo = g.OrderByDescending(t => t.UpdatedAt).First().UserMetadata?.Name ?? "Unknown Device"
                })
                .OrderByDescending(s => s.LastActivity)
                .ToList();

            if (activeSessions.Count > 1)
            {
                _logger.LogWarning(
                    "Multiple active sessions detected for employee {EmployeeId}: {SessionCount} sessions", 
                    employeeId, activeSessions.Count);
            }

            return activeSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active sessions for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    /// <summary>
    /// Detects session collision and returns warning message if multiple sessions are active
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="currentSessionId">Current session identifier</param>
    /// <returns>Warning message if collision detected, null otherwise</returns>
    public async Task<SessionCollisionWarning?> DetectSessionCollisionAsync(string employeeId, string currentSessionId)
    {
        var activeSessions = await GetActiveSessionsAsync(employeeId, currentSessionId);
        
        // Check if there are other active sessions besides the current one
        var otherSessions = activeSessions.Where(s => !s.IsCurrent).ToList();
        
        if (otherSessions.Count == 0)
        {
            return null; // No collision
        }

        _logger.LogInformation(
            "Session collision detected for employee {EmployeeId}: Current={CurrentSession}, Other sessions={OtherCount}", 
            employeeId, currentSessionId, otherSessions.Count);

        return new SessionCollisionWarning
        {
            Message = otherSessions.Count == 1
                ? "You have another active session open. Changes made in one session may not reflect immediately in the other."
                : $"You have {otherSessions.Count} other active sessions open. Changes made in one session may not reflect immediately in others.",
            CurrentSession = activeSessions.First(s => s.IsCurrent),
            OtherActiveSessions = otherSessions,
            TotalActiveSessions = activeSessions.Count
        };
    }

    /// <summary>
    /// Registers or updates a session's last activity timestamp
    /// Called at the start of each conversation request
    /// </summary>
    public async Task RegisterSessionActivityAsync(string threadId, string employeeId)
    {
        try
        {
            var thread = await _conversationStore.GetThreadAsync(threadId, employeeId);
            if (thread != null)
            {
                // UpdatedAt is automatically set in ConversationStore.UpdateThreadAsync
                await _conversationStore.UpdateThreadAsync(thread);
                
                _logger.LogDebug(
                    "Registered session activity for thread {ThreadId}, session {SessionId}", 
                    threadId, thread.SessionId);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types - Session tracking failure should not break main flow
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, 
                "Failed to register session activity for thread {ThreadId}", threadId);
            // Don't throw - session tracking failure shouldn't break the main flow
        }
    }

    /// <summary>
    /// Marks a session as inactive (e.g., when user explicitly logs out)
    /// </summary>
    public async Task DeactivateSessionAsync(string sessionId, string employeeId)
    {
        try
        {
            var recentThreads = await _conversationStore.GetRecentThreadsAsync(employeeId, limit: 20);
            var sessionThreads = recentThreads.Where(t => t.SessionId == sessionId).ToList();

            foreach (var thread in sessionThreads)
            {
                // Mark session as inactive by setting a flag in context memory
                if (thread.State.ContextMemory == null)
                {
                    thread.State.ContextMemory = new Dictionary<string, object>();
                }
                
                thread.State.ContextMemory["sessionActive"] = false;
                thread.State.ContextMemory["deactivatedAt"] = DateTimeOffset.UtcNow;
                
                await _conversationStore.UpdateThreadAsync(thread);
            }

            _logger.LogInformation(
                "Deactivated session {SessionId} for employee {EmployeeId} ({ThreadCount} threads)", 
                sessionId, employeeId, sessionThreads.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types - Session deactivation failure should not break main flow
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, 
                "Failed to deactivate session {SessionId} for employee {EmployeeId}", 
                sessionId, employeeId);
            // Don't throw - session deactivation failure shouldn't break the main flow
        }
    }
}

/// <summary>
/// Represents an active conversation session
/// </summary>
public class ActiveSession
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTimeOffset LastActivity { get; set; }

    /// <summary>
    /// Number of conversation threads in this session
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Whether this is the current session making the request
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Device or browser information (if available)
    /// </summary>
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable time since last activity
    /// </summary>
    public string TimeSinceLastActivity
    {
        get
        {
            var timeSpan = DateTimeOffset.UtcNow - LastActivity;
            
            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minutes ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hours ago";
            
            return $"{(int)timeSpan.TotalDays} days ago";
        }
    }
}

/// <summary>
/// Warning message when session collision is detected
/// </summary>
public class SessionCollisionWarning
{
    /// <summary>
    /// User-friendly warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Current active session
    /// </summary>
    public ActiveSession CurrentSession { get; set; } = new();

    /// <summary>
    /// Other active sessions
    /// </summary>
    public List<ActiveSession> OtherActiveSessions { get; set; } = new();

    /// <summary>
    /// Total number of active sessions
    /// </summary>
    public int TotalActiveSessions { get; set; }
}
