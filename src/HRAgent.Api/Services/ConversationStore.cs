using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for managing conversation persistence
/// Wraps IConversationStore with business logic
/// </summary>
public class ConversationStore
{
    private readonly IConversationStore _store;
    private readonly ILogger<ConversationStore> _logger;

    public ConversationStore(IConversationStore store, ILogger<ConversationStore> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Gets a conversation thread by ID
    /// </summary>
    public async Task<ConversationThread?> GetThreadAsync(string threadId, string employeeId)
    {
        try
        {
            return await _store.GetThreadAsync(threadId, employeeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve conversation thread {ThreadId} for employee {EmployeeId}", 
                threadId, employeeId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new conversation thread
    /// </summary>
    public async Task<ConversationThread> CreateThreadAsync(ConversationThread thread)
    {
        try
        {
            thread.CreatedAt = DateTimeOffset.UtcNow;
            thread.UpdatedAt = DateTimeOffset.UtcNow;
            return await _store.CreateThreadAsync(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation thread for employee {EmployeeId}", 
                thread.EmployeeId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing conversation thread
    /// </summary>
    public async Task<ConversationThread> UpdateThreadAsync(ConversationThread thread)
    {
        try
        {
            thread.UpdatedAt = DateTimeOffset.UtcNow;
            return await _store.UpdateThreadAsync(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update conversation thread {ThreadId}", thread.Id);
            throw;
        }
    }

    /// <summary>
    /// Gets recent conversations for an employee
    /// </summary>
    public async Task<List<ConversationThread>> GetRecentThreadsAsync(string employeeId, int limit = 10)
    {
        try
        {
            var result = await _store.GetRecentThreadsAsync(employeeId, limit);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent threads for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a conversation thread (for GDPR compliance)
    /// </summary>
    public async Task<bool> DeleteThreadAsync(string threadId, string employeeId)
    {
        try
        {
            await _store.DeleteThreadAsync(threadId, employeeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation thread {ThreadId} for employee {EmployeeId}", 
                threadId, employeeId);
            throw;
        }
    }

    /// <summary>
    /// Adds a message to a conversation thread
    /// </summary>
    public async Task AddMessageAsync(string threadId, string employeeId, ConversationMessage message)
    {
        var thread = await GetThreadAsync(threadId, employeeId);
        if (thread == null)
        {
            throw new InvalidOperationException($"Conversation thread {threadId} not found");
        }

        message.Timestamp = DateTimeOffset.UtcNow;
        thread.Messages.Add(message);
        await UpdateThreadAsync(thread);

        _logger.LogInformation("Added {Role} message to thread {ThreadId}", message.Role, threadId);
    }

    /// <summary>
    /// Updates conversation state
    /// </summary>
    public async Task UpdateStateAsync(string threadId, string employeeId, ConversationState state)
    {
        var thread = await GetThreadAsync(threadId, employeeId);
        if (thread == null)
        {
            throw new InvalidOperationException($"Conversation thread {threadId} not found");
        }

        thread.State = state;
        await UpdateThreadAsync(thread);

        _logger.LogInformation("Updated state for thread {ThreadId}", threadId);
    }
}
