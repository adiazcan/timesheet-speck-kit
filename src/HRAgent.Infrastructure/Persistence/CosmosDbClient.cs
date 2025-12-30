using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HRAgent.Contracts.Models;

namespace HRAgent.Infrastructure.Persistence;

/// <summary>
/// Configuration for Azure Cosmos DB (DocumentDB API) client
/// Supports MongoDB for local development and Cosmos DB for production
/// </summary>
public static class CosmosDbConfig
{
    /// <summary>
    /// Registers Cosmos DB client with dependency injection
    /// </summary>
    public static IServiceCollection AddCosmosDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        var isDevelopment = environment == "Development";
        
        if (isDevelopment)
        {
            // Local MongoDB development (DocumentDB API compatible)
            var connectionString = configuration.GetConnectionString("mongodb") 
                ?? "mongodb://localhost:27017";
            
            services.AddSingleton<IConversationStore>(sp =>
            {
                var mongoClient = new MongoDB.Driver.MongoClient(connectionString);
                var database = mongoClient.GetDatabase("hrapp");
                return new MongoDbConversationStore(database);
            });
        }
        else
        {
            // Production Cosmos DB
            var cosmosEndpoint = configuration["CosmosDb:Endpoint"]
                ?? throw new InvalidOperationException("CosmosDb:Endpoint configuration is missing");
            
            var cosmosKey = configuration["CosmosDb:Key"];
            
            services.AddSingleton<CosmosClient>(sp =>
            {
                var endpointUri = new Uri(cosmosEndpoint);
                
                // Use API key if provided, otherwise use Managed Identity
                if (!string.IsNullOrEmpty(cosmosKey))
                {
                    return new CosmosClient(cosmosEndpoint, cosmosKey, new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        },
                        ConnectionMode = ConnectionMode.Direct,
                        MaxRetryAttemptsOnRateLimitedRequests = 3,
                        MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)
                    });
                }
                else
                {
                    return new CosmosClient(cosmosEndpoint, new DefaultAzureCredential(), new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        },
                        ConnectionMode = ConnectionMode.Direct,
                        MaxRetryAttemptsOnRateLimitedRequests = 3,
                        MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)
                    });
                }
            });
            
            services.AddScoped<IConversationStore, CosmosDbConversationStore>();
        }
        
        return services;
    }
}

/// <summary>
/// Interface for conversation storage operations
/// </summary>
public interface IConversationStore
{
    // Conversation operations
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

/// <summary>
/// Cosmos DB implementation of conversation store
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
    
    // GDPR Deletion Methods (T033q)
    
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

/// <summary>
/// MongoDB implementation for local development
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
