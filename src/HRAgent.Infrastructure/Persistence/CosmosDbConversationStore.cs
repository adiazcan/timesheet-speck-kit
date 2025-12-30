using HRAgent.Contracts.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace HRAgent.Infrastructure.Persistence;

/// <summary>
/// Cosmos DB implementation of conversation store
/// For production Azure Cosmos DB deployments
/// </summary>
public class CosmosDbConversationStore : IConversationStore
{
    private readonly Container _conversationsContainer;
    private readonly Container _deletionRequestsContainer;
    
    public CosmosDbConversationStore(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "hrapp";
        var conversationsContainerName = configuration["CosmosDb:ContainerName"] ?? "conversations";
        var deletionRequestsContainerName = configuration["CosmosDb:DeletionRequestsContainerName"] ?? "deletionRequests";
        
        _conversationsContainer = cosmosClient.GetContainer(databaseName, conversationsContainerName);
        _deletionRequestsContainer = cosmosClient.GetContainer(databaseName, deletionRequestsContainerName);
    }
    
    public async Task<ConversationThread?> GetThreadAsync(string threadId, string employeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _conversationsContainer.ReadItemAsync<ConversationThread>(
                threadId,
                new PartitionKey(employeeId),
                cancellationToken: cancellationToken);
            
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task<ConversationThread> CreateThreadAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        var response = await _conversationsContainer.CreateItemAsync(
            thread,
            new PartitionKey(thread.EmployeeId),
            cancellationToken: cancellationToken);
        
        return response.Resource;
    }
    
    public async Task<ConversationThread> UpdateThreadAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        
        var response = await _conversationsContainer.ReplaceItemAsync(
            thread,
            thread.Id,
            new PartitionKey(thread.EmployeeId),
            cancellationToken: cancellationToken);
        
        return response.Resource;
    }
    
    public async Task<IEnumerable<ConversationThread>> GetRecentThreadsAsync(string employeeId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.employeeId = @employeeId ORDER BY c.updatedAt DESC OFFSET 0 LIMIT @limit")
            .WithParameter("@employeeId", employeeId)
            .WithParameter("@limit", limit);
        
        var iterator = _conversationsContainer.GetItemQueryIterator<ConversationThread>(query);
        var threads = new List<ConversationThread>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            threads.AddRange(response);
        }
        
        return threads;
    }
    
    public async Task DeleteThreadAsync(string threadId, string employeeId, CancellationToken cancellationToken = default)
    {
        await _conversationsContainer.DeleteItemAsync<ConversationThread>(
            threadId,
            new PartitionKey(employeeId),
            cancellationToken: cancellationToken);
    }
    
    // GDPR Deletion Methods
    
    public async Task<int> DeleteAllConversationsAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT c.id FROM c WHERE c.employeeId = @employeeId")
            .WithParameter("@employeeId", employeeId);
        
        var iterator = _conversationsContainer.GetItemQueryIterator<dynamic>(query);
        var deletedCount = 0;
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var item in response)
            {
                await _conversationsContainer.DeleteItemAsync<ConversationThread>(
                    item.id.ToString(),
                    new PartitionKey(employeeId),
                    cancellationToken: cancellationToken);
                deletedCount++;
            }
        }
        
        return deletedCount;
    }
    
    public async Task<ConversationDeletionRequest> SaveDeletionRequestAsync(ConversationDeletionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _deletionRequestsContainer.CreateItemAsync(
            request,
            new PartitionKey(request.EmployeeId),
            cancellationToken: cancellationToken);
        
        return response.Resource;
    }
    
    public async Task<ConversationDeletionRequest> UpdateDeletionRequestAsync(ConversationDeletionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _deletionRequestsContainer.ReplaceItemAsync(
            request,
            request.Id,
            new PartitionKey(request.EmployeeId),
            cancellationToken: cancellationToken);
        
        return response.Resource;
    }
    
    public async Task<ConversationDeletionRequest?> GetDeletionRequestAsync(string requestId, string employeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _deletionRequestsContainer.ReadItemAsync<ConversationDeletionRequest>(
                requestId,
                new PartitionKey(employeeId),
                cancellationToken: cancellationToken);
            
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task<IEnumerable<ConversationDeletionRequest>> GetDeletionRequestsByEmployeeAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.employeeId = @employeeId")
            .WithParameter("@employeeId", employeeId);
        
        var iterator = _deletionRequestsContainer.GetItemQueryIterator<ConversationDeletionRequest>(query);
        var requests = new List<ConversationDeletionRequest>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            requests.AddRange(response);
        }
        
        return requests;
    }
    
    public async Task<IEnumerable<ConversationDeletionRequest>> GetAllPendingDeletionRequestsAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = 'Pending'");
        
        var iterator = _deletionRequestsContainer.GetItemQueryIterator<ConversationDeletionRequest>(query);
        var requests = new List<ConversationDeletionRequest>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            requests.AddRange(response);
        }
        
        return requests;
    }
}
