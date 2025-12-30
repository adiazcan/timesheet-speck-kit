using HRAgent.Contracts.Models;
using MongoDB.Driver;

namespace HRAgent.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation for local development
/// DocumentDB API compatible with Cosmos DB
/// </summary>
public class MongoDbConversationStore : IConversationStore
{
    private readonly MongoDB.Driver.IMongoCollection<ConversationThread> _conversationsCollection;
    private readonly MongoDB.Driver.IMongoCollection<ConversationDeletionRequest> _deletionRequestsCollection;
    
    public MongoDbConversationStore(MongoDB.Driver.IMongoDatabase database)
    {
        _conversationsCollection = database.GetCollection<ConversationThread>("conversations");
        _deletionRequestsCollection = database.GetCollection<ConversationDeletionRequest>("deletionRequests");
        
        // Create compound index for conversations
        var conversationIndexKeys = MongoDB.Driver.Builders<ConversationThread>.IndexKeys
            .Combine(
                MongoDB.Driver.Builders<ConversationThread>.IndexKeys.Ascending(t => t.EmployeeId),
                MongoDB.Driver.Builders<ConversationThread>.IndexKeys.Descending(t => t.UpdatedAt)
            );
        var conversationIndexModel = new MongoDB.Driver.CreateIndexModel<ConversationThread>(conversationIndexKeys);
        _conversationsCollection.Indexes.CreateOne(conversationIndexModel);
        
        // Create index for deletion requests
        var deletionIndexKeys = MongoDB.Driver.Builders<ConversationDeletionRequest>.IndexKeys
            .Ascending(r => r.EmployeeId);
        var deletionIndexModel = new MongoDB.Driver.CreateIndexModel<ConversationDeletionRequest>(deletionIndexKeys);
        _deletionRequestsCollection.Indexes.CreateOne(deletionIndexModel);
    }
    
    public async Task<ConversationThread?> GetThreadAsync(string threadId, string employeeId, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationThread>.Filter.And(
            MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.Id, threadId),
            MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.EmployeeId, employeeId));
        
        var cursor = await _conversationsCollection.FindAsync<ConversationThread>(filter, cancellationToken: cancellationToken);
        await cursor.MoveNextAsync(cancellationToken);
        return cursor.Current.FirstOrDefault();
    }
    
    public async Task<ConversationThread> CreateThreadAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        await _conversationsCollection.InsertOneAsync(thread, cancellationToken: cancellationToken);
        return thread;
    }
    
    public async Task<ConversationThread> UpdateThreadAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        
        var filter = MongoDB.Driver.Builders<ConversationThread>.Filter.And(
            MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.Id, thread.Id),
            MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.EmployeeId, thread.EmployeeId));
        
        await _conversationsCollection.ReplaceOneAsync(filter, thread, cancellationToken: cancellationToken);
        return thread;
    }
    
    public async Task<IEnumerable<ConversationThread>> GetRecentThreadsAsync(string employeeId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.EmployeeId, employeeId);
        var sort = MongoDB.Driver.Builders<ConversationThread>.Sort.Descending(t => t.UpdatedAt);
        
        var findOptions = new MongoDB.Driver.FindOptions<ConversationThread, ConversationThread>
        {
            Sort = sort,
            Limit = limit
        };
        
        var cursor = await _conversationsCollection.FindAsync(filter, findOptions, cancellationToken);
        var results = new List<ConversationThread>();
        
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            results.AddRange(cursor.Current);
        }
        
        return results;
    }
    
    public async Task DeleteThreadAsync(string threadId, string employeeId, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationThread>.Filter.And(
            MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.Id, threadId),
            MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.EmployeeId, employeeId));
        
        await _conversationsCollection.DeleteOneAsync(filter, cancellationToken);
    }
    
    // GDPR Deletion Methods (T033q)
    
    public async Task<int> DeleteAllConversationsAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationThread>.Filter.Eq(t => t.EmployeeId, employeeId);
        var result = await _conversationsCollection.DeleteManyAsync(filter, cancellationToken);
        return (int)result.DeletedCount;
    }
    
    public async Task<ConversationDeletionRequest> SaveDeletionRequestAsync(ConversationDeletionRequest request, CancellationToken cancellationToken = default)
    {
        await _deletionRequestsCollection.InsertOneAsync(request, cancellationToken: cancellationToken);
        return request;
    }
    
    public async Task<ConversationDeletionRequest> UpdateDeletionRequestAsync(ConversationDeletionRequest request, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.And(
            MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.Eq(r => r.Id, request.Id),
            MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.Eq(r => r.EmployeeId, request.EmployeeId));
        
        await _deletionRequestsCollection.ReplaceOneAsync(filter, request, cancellationToken: cancellationToken);
        return request;
    }
    
    public async Task<ConversationDeletionRequest?> GetDeletionRequestAsync(string requestId, string employeeId, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.And(
            MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.Eq(r => r.Id, requestId),
            MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.Eq(r => r.EmployeeId, employeeId));
        
        var cursor = await _deletionRequestsCollection.FindAsync<ConversationDeletionRequest>(filter, cancellationToken: cancellationToken);
        await cursor.MoveNextAsync(cancellationToken);
        return cursor.Current.FirstOrDefault();
    }
    
    public async Task<IEnumerable<ConversationDeletionRequest>> GetDeletionRequestsByEmployeeAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.Eq(r => r.EmployeeId, employeeId);
        
        var cursor = await _deletionRequestsCollection.FindAsync<ConversationDeletionRequest>(filter, cancellationToken: cancellationToken);
        var results = new List<ConversationDeletionRequest>();
        
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            results.AddRange(cursor.Current);
        }
        
        return results;
    }
    
    public async Task<IEnumerable<ConversationDeletionRequest>> GetAllPendingDeletionRequestsAsync(CancellationToken cancellationToken = default)
    {
        var filter = MongoDB.Driver.Builders<ConversationDeletionRequest>.Filter.Eq(r => r.Status, DeletionRequestStatus.Pending);
        
        var cursor = await _deletionRequestsCollection.FindAsync<ConversationDeletionRequest>(filter, cancellationToken: cancellationToken);
        var results = new List<ConversationDeletionRequest>();
        
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            results.AddRange(cursor.Current);
        }
        
        return results;
    }
}
