using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRAgent.Infrastructure.Persistence;

/// <summary>
/// Configuration for Azure Cosmos DB and MongoDB (local development)
/// Registers conversation store implementations with dependency injection
/// </summary>
public static class CosmosDbConfig
{
    /// <summary>
    /// Registers Cosmos DB client and conversation store with dependency injection
    /// Uses MongoDB for local development, Cosmos DB for production
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
            RegisterMongoDb(services, configuration);
        }
        else
        {
            // Production Cosmos DB
            RegisterCosmosDb(services, configuration);
        }
        
        return services;
    }

    private static void RegisterMongoDb(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("hrapp-local")
            ?? "mongodb://localhost:27017";
        
        services.AddSingleton<IConversationStore>(sp =>
        {
            var mongoClient = new MongoDB.Driver.MongoClient(connectionString);
            var database = mongoClient.GetDatabase("hrapp");
            return new MongoDbConversationStore(database);
        });
    }

    private static void RegisterCosmosDb(IServiceCollection services, IConfiguration configuration)
    {
        var cosmosEndpoint = configuration["CosmosDb:Endpoint"]
            ?? throw new InvalidOperationException("CosmosDb:Endpoint configuration is missing");
        
        var cosmosKey = configuration["CosmosDb:Key"];
        
        services.AddSingleton<CosmosClient>(sp =>
        {
            var options = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                ConnectionMode = ConnectionMode.Direct,
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)
            };

            // Use API key if provided, otherwise use Managed Identity
            if (!string.IsNullOrEmpty(cosmosKey))
            {
                return new CosmosClient(cosmosEndpoint, cosmosKey, options);
            }
            else
            {
                return new CosmosClient(cosmosEndpoint, new DefaultAzureCredential(), options);
            }
        });
        
        services.AddScoped<IConversationStore, CosmosDbConversationStore>();
    }
}
