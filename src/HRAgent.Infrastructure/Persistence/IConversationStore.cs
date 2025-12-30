using HRAgent.Contracts.Models;

namespace HRAgent.Infrastructure.Persistence;

/// <summary>
/// Interface for conversation storage operations
/// Abstracts Cosmos DB and MongoDB implementations
/// </summary>
public interface IConversationStore
{
    // Conversation thread operations
    Task<ConversationThread?> GetThreadAsync(string threadId, string employeeId, CancellationToken cancellationToken = default);
    Task<ConversationThread> CreateThreadAsync(ConversationThread thread, CancellationToken cancellationToken = default);
    Task<ConversationThread> UpdateThreadAsync(ConversationThread thread, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversationThread>> GetRecentThreadsAsync(string employeeId, int limit = 10, CancellationToken cancellationToken = default);
    Task DeleteThreadAsync(string threadId, string employeeId, CancellationToken cancellationToken = default);
    
    // GDPR deletion operations (T033q)
    Task<int> DeleteAllConversationsAsync(string employeeId, CancellationToken cancellationToken = default);
    Task<ConversationDeletionRequest> SaveDeletionRequestAsync(ConversationDeletionRequest request, CancellationToken cancellationToken = default);
    Task<ConversationDeletionRequest> UpdateDeletionRequestAsync(ConversationDeletionRequest request, CancellationToken cancellationToken = default);
    Task<ConversationDeletionRequest?> GetDeletionRequestAsync(string requestId, string employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversationDeletionRequest>> GetDeletionRequestsByEmployeeAsync(string employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversationDeletionRequest>> GetAllPendingDeletionRequestsAsync(CancellationToken cancellationToken = default);
}
