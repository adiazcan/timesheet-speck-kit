# .NET Aspire 13 Guide for Orchestrating Distributed Applications with Agent Frameworks

## Executive Summary

.NET Aspire 13 is a cloud-ready stack for building observable, production-ready distributed applications with .NET 10. It provides a code-first orchestration model that simplifies local development while enabling seamless deployment to various platforms including Azure Container Apps, Kubernetes, and Docker Compose.

This guide focuses on using Aspire to orchestrate distributed applications with agent frameworks, covering architecture, local development, service composition, and production deployment patterns.

---

## Table of Contents

1. [Aspire 13 Architecture and Core Concepts](#1-aspire-13-architecture-and-core-concepts)
2. [Orchestrating Multiple Services](#2-orchestrating-multiple-services)
3. [Local Development Setup with Docker Containers](#3-local-development-setup-with-docker-containers)
4. [Service-to-Service Communication Patterns](#4-service-to-service-communication-patterns)
5. [Configuration Management and Secrets Handling](#5-configuration-management-and-secrets-handling)
6. [Application Insights Integration](#6-application-insights-integration)
7. [Deployment to Azure Container Apps](#7-deployment-to-azure-container-apps)
8. [Best Practices for .NET 10](#8-best-practices-for-net-10)

---

## 1. Aspire 13 Architecture and Core Concepts

### 1.1 What is Aspire?

.NET Aspire is an opinionated, cloud-ready stack for building observable, production-ready distributed applications. It provides:

- **Unified Development Experience**: Launch and debug your entire distributed app with a single command
- **Code-First Configuration**: Define architecture in code, no complex config files
- **Local Orchestration**: Automatically handle service startup, dependencies, and connections
- **Deployment Flexibility**: Deploy to Kubernetes, cloud providers, or your own servers
- **Built-in Observability**: Automatic OpenTelemetry integration for logging, tracing, and metrics

### 1.2 The AppHost

The **AppHost** is the orchestration layer where you define your application's architecture. It's the code-first place where you declare services, resources, and their relationships.

**Key Responsibilities:**
- Service discovery and dependency resolution
- Configuration injection (connection strings, endpoints, secrets)
- Health monitoring and startup ordering
- Local orchestration via Developer Control Plane (DCP)

**Example AppHost Structure:**

```csharp
// Program.cs in AppHost project
var builder = DistributedApplication.CreateBuilder(args);

// Define resources
var postgres = builder.AddPostgres("db")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("appdata")
    .WithDataVolume();

// Define API service with database dependency
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

// Define frontend with API dependency
builder.AddProject<Projects.Web>("frontend")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
```

### 1.3 Resource Model

Aspire uses a flexible resource model that represents everything your app needs:

**Resource Types:**
- **Projects**: C# projects, the primary compute resources (.NET apps, APIs, workers)
- **Containers**: Docker containers for databases, message brokers, caches
- **Executables**: Node.js apps, Python services, scripts
- **Cloud Resources**: Azure Storage, Cosmos DB, Redis, Service Bus
- **Parameters**: External configuration values, secrets, connection strings

**Resource Hierarchy Example:**

```
AppHost
├── MongoDB (Container Resource)
│   └── Database "chatdb" (Database Resource)
├── Azure Storage (Cloud Resource)
│   ├── Blobs "chat-blobs"
│   └── Queues "chat-queue"
├── API Service (Project Resource)
│   ├── References: MongoDB, Storage
│   └── Exposes: HTTP/HTTPS endpoints
└── Agent Service (Project Resource)
    ├── References: MongoDB, Storage, API
    └── Exposes: HTTP/HTTPS endpoints
```

### 1.4 Service Discovery

Aspire provides automatic service discovery through configuration-based endpoint resolution. When you reference one resource from another using `WithReference()`, Aspire:

1. Generates connection information automatically
2. Injects configuration via environment variables
3. Provides service URLs for HTTP/HTTPS communication
4. Enables DNS-based discovery in deployment

**Service Discovery Conventions:**

```csharp
// In AppHost
var api = builder.AddProject<Projects.Api>("api");
var frontend = builder.AddProject<Projects.Web>("frontend")
    .WithReference(api);

// In frontend project, service is available at:
// - https://api (default endpoint)
// - https+http://api (scheme negotiation)
// - https://_admin.api (named endpoint "admin")
```

**Environment Variables Injected:**

```
API_HTTP=http://localhost:5000
API_HTTPS=https://localhost:5001
services__api__http__0=http://localhost:5000
services__api__https__0=https://localhost:5001
```

### 1.5 Service Defaults

The **ServiceDefaults** project provides opinionated configurations applied to all services:

**Key Features:**
- OpenTelemetry setup (logging, tracing, metrics)
- Default health check endpoints (`/health`, `/alive`)
- Service discovery configuration
- HttpClient with resilience and service discovery
- Standardized telemetry exporters

**Usage in Service Projects:**

```csharp
// Program.cs in any service project
var builder = WebApplication.CreateBuilder(args);

// Apply service defaults
builder.AddServiceDefaults();

// Your service configuration...
builder.Services.AddControllers();

var app = builder.Build();

// Map health check endpoints
app.MapDefaultEndpoints();

app.Run();
```

---

## 2. Orchestrating Multiple Services

### 2.1 Multi-Service Architecture for Agent Systems

A typical agent-based application might include:

```
┌──────────────────────────────────────────────────────┐
│                    AppHost                           │
│  (Orchestration Layer)                               │
└──────────────────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
   ┌────▼────┐     ┌────▼────┐     ┌────▼────┐
   │  Agent  │     │   API   │     │   Web   │
   │ Service │     │ Service │     │  Front  │
   └────┬────┘     └────┬────┘     └────┬────┘
        │               │               │
        └───────┬───────┴───────┬───────┘
                │               │
        ┌───────▼───┐    ┌──────▼──────┐
        │  MongoDB  │    │   Storage   │
        │ (Cosmos)  │    │   (Blobs)   │
        └───────────┘    └─────────────┘
```

### 2.2 Complete AppHost Example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// === Database Layer ===
// MongoDB for agent state and conversation history
var mongo = builder.AddMongoDB("mongodb")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .AddDatabase("agentdb");

// Or use Cosmos DB with emulator for local dev
var cosmos = builder.AddAzureCosmosDB("cosmosdb")
    .RunAsEmulator()
    .AddDatabase("conversations")
    .AddContainer("messages", "/conversationId")
    .AddContainer("agents", "/agentId");

// === Storage Layer ===
// Azure Storage for documents, embeddings, etc.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("documents")
    .AddQueues("agent-tasks")
    .AddTables("metrics");

// === Caching Layer ===
var redis = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

// === Backend Services ===
// Agent Service - handles AI agent orchestration
var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithReference(mongo)
    .WithReference(cosmos)
    .WithReference(storage)
    .WithReference(redis)
    .WaitFor(mongo)
    .WaitFor(cosmos)
    .WithReplicas(2); // Scale for load

// API Service - REST API for frontend
var apiService = builder.AddProject<Projects.ApiService>("api")
    .WithReference(agentService)
    .WithReference(mongo)
    .WithReference(redis)
    .WaitFor(agentService)
    .WithHttpsHealthCheck("/health");

// === Frontend ===
// Web UI or API Gateway
var frontend = builder.AddProject<Projects.WebApp>("frontend")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithExternalHttpEndpoints(); // Expose to external traffic

// === Monitoring ===
// Application Insights (for production)
var appInsights = builder.AddAzureApplicationInsights("monitoring");

// Apply monitoring to all services
agentService.WithReference(appInsights);
apiService.WithReference(appInsights);
frontend.WithReference(appInsights);

builder.Build().Run();
```

### 2.3 Resource Dependencies and Startup Order

Aspire handles dependencies and startup order through:

**1. WithReference()**: Injects connection information
```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(database); // Injects connection string
```

**2. WaitFor()**: Ensures dependency is ready before starting
```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(database); // Wait for database to be healthy
```

**3. Health Checks**: Define custom readiness criteria
```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health") // Poll this endpoint
    .WithHealthCheckTimeout(TimeSpan.FromSeconds(30));
```

### 2.4 Scaling Services

```csharp
// Run multiple replicas
var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithReplicas(3); // 3 instances for load balancing

// Dynamic scaling based on parameters
var replicaCount = builder.Configuration.GetValue<int>("AgentReplicas", 1);
var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithReplicas(replicaCount);
```

---

## 3. Local Development Setup with Docker Containers

### 3.1 Container Resources

Aspire automatically manages Docker containers for local development:

**MongoDB Container:**

```csharp
var mongo = builder.AddMongoDB("mongodb")
    .WithLifetime(ContainerLifetime.Persistent) // Survives restarts
    .WithDataVolume() // Persist data
    .WithMongoExpress() // Optional: Web UI at http://localhost:8081
    .AddDatabase("agentdb");
```

**Cosmos DB Emulator:**

```csharp
var cosmos = builder.AddAzureCosmosDB("cosmosdb")
    .RunAsEmulator(emulator => 
    {
        emulator.WithGatewayPort(8081); // Custom port
        emulator.WithDataVolume(); // Persist data
        emulator.WithPartitionCount(50); // Adjust capacity
    })
    .AddDatabase("conversations");
```

**Azure Storage Emulator (Azurite):**

```csharp
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite.WithBlobPort(10000);
        azurite.WithQueuePort(10001);
        azurite.WithTablePort(10002);
        azurite.WithDataVolume(); // Persist data
        azurite.WithLifetime(ContainerLifetime.Persistent);
    })
    .AddBlobs("documents")
    .AddQueues("tasks");
```

### 3.2 Container Networking

Aspire creates a dedicated container bridge network automatically:

**Network Behavior:**
- Session networks: `aspire-session-network-<unique-id>-<app-host-name>` (temporary)
- Persistent networks: `aspire-persistent-network-<project-hash>-<app-host-name>` (survives restarts)
- Containers register using their resource name for DNS resolution
- Example: `mongodb` container accessible at `mongodb:27017` from other containers

### 3.3 Data Persistence

**Data Volumes (Recommended):**

```csharp
var mongo = builder.AddMongoDB("mongodb")
    .WithDataVolume(); // Anonymous volume
    
// Or named volume
var mongo = builder.AddMongoDB("mongodb")
    .WithDataVolume("mongo-data"); // Named volume
```

**Bind Mounts (Development):**

```csharp
var mongo = builder.AddMongoDB("mongodb")
    .WithDataBindMount("./data/mongodb"); // Local directory
```

**Warning**: Some databases (including MongoDB) cannot use data volumes successfully when deployed to Azure Container Apps due to SMB limitations. For production, deploy to Kubernetes (AKS) or use managed services.

### 3.4 Development Workflow

**1. Prerequisites:**
- Docker Desktop or Podman running
- .NET 10 SDK installed
- Aspire CLI: `dotnet tool install -g aspire-cli`

**2. Create Project:**

```bash
# Create new Aspire project
aspire new AspireAgentApp --output AspireAgentApp
cd AspireAgentApp

# Add integrations
aspire add mongodb
aspire add azure-storage
aspire add azure-cosmosdb
```

**3. Run Locally:**

```bash
# Start with debugging (F5 in Visual Studio)
# OR
dotnet run --project AspireAgentApp.AppHost

# Access dashboard at http://localhost:15888
```

**4. Dashboard Features:**
- View all resources and their status
- Live logs from all services
- Distributed tracing visualization
- Metrics and performance data
- Health check status
- Resource dependencies graph

---

## 4. Service-to-Service Communication Patterns

### 4.1 HTTP Communication with Service Discovery

**In AppHost:**

```csharp
var api = builder.AddProject<Projects.Api>("api");
var agentService = builder.AddProject<Projects.AgentService>("agent-service");
var frontend = builder.AddProject<Projects.Web>("frontend")
    .WithReference(api)
    .WithReference(agentService);
```

**In Frontend Service:**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Register HttpClient with service discovery
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api"); // Service name
});

builder.Services.AddHttpClient<AgentClient>(client =>
{
    client.BaseAddress = new Uri("https://agent-service");
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();

// Usage in controller
public class ChatController : ControllerBase
{
    private readonly ApiClient _apiClient;
    private readonly AgentClient _agentClient;
    
    public ChatController(ApiClient apiClient, AgentClient agentClient)
    {
        _apiClient = apiClient;
        _agentClient = agentClient;
    }
    
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // Call API service
        var context = await _apiClient.GetContextAsync(request.ConversationId);
        
        // Call agent service
        var response = await _agentClient.ProcessAsync(request.Message, context);
        
        return Ok(response);
    }
}
```

### 4.2 Named Endpoints

Services can expose multiple endpoints:

```csharp
// In AppHost
var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithHttpsEndpoint(port: 5000, name: "api")
    .WithHttpsEndpoint(port: 9000, name: "admin");

// In consuming service
builder.Services.AddHttpClient<AgentClient>(client =>
{
    client.BaseAddress = new Uri("https://agent-service"); // Default endpoint
});

builder.Services.AddHttpClient<AgentAdminClient>(client =>
{
    client.BaseAddress = new Uri("https://_admin.agent-service"); // Named endpoint
});
```

### 4.3 Resilience and Retry Patterns

Service defaults automatically configure resilience:

```csharp
// Automatically applied via AddServiceDefaults()
builder.Services.ConfigureHttpClientDefaults(http =>
{
    // Standard resilience handler with:
    // - Retry with exponential backoff
    // - Circuit breaker
    // - Timeout
    http.AddStandardResilienceHandler();
    
    // Service discovery
    http.AddServiceDiscovery();
});

// Custom resilience policy
builder.Services.AddHttpClient<AgentClient>(client =>
{
    client.BaseAddress = new Uri("https://agent-service");
})
.AddResilienceHandler("agent-resilience", builder =>
{
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    });
    
    builder.AddTimeout(TimeSpan.FromSeconds(30));
});
```

### 4.4 gRPC Communication

```csharp
// In AppHost
var grpcService = builder.AddProject<Projects.GrpcService>("grpc-service")
    .WithHttpEndpoint(port: 5000, scheme: "http"); // gRPC requires HTTP/2

// In consuming service
builder.Services.AddGrpcClient<AgentService.AgentServiceClient>(options =>
{
    options.Address = new Uri("http://grpc-service");
})
.AddServiceDiscovery();
```

### 4.5 Message-Based Communication

**With RabbitMQ:**

```csharp
// In AppHost
var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

var publisher = builder.AddProject<Projects.Publisher>("publisher")
    .WithReference(rabbitmq);
    
var consumer = builder.AddProject<Projects.Consumer>("consumer")
    .WithReference(rabbitmq);

// In service
builder.AddRabbitMQClient("messaging");

// Usage
public class MessagePublisher
{
    private readonly IConnection _connection;
    
    public MessagePublisher(IConnection connection)
    {
        _connection = connection;
    }
    
    public void PublishAgentTask(AgentTask task)
    {
        using var channel = _connection.CreateModel();
        channel.QueueDeclare("agent-tasks", durable: true, exclusive: false);
        
        var body = JsonSerializer.SerializeToUtf8Bytes(task);
        channel.BasicPublish("", "agent-tasks", null, body);
    }
}
```

**With Azure Service Bus:**

```csharp
// In AppHost
var serviceBus = builder.AddAzureServiceBus("messaging")
    .AddQueue("agent-tasks")
    .AddTopic("agent-events", new[] { "processed", "failed" });

// In service
builder.AddAzureServiceBusClient("messaging");
```

---

## 5. Configuration Management and Secrets Handling

### 5.1 External Parameters

Parameters express external values that vary between environments:

**Define Parameters in AppHost:**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// String parameter
var apiKey = builder.AddParameter("api-key");

// Secret parameter (never logged)
var dbPassword = builder.AddParameter("db-password", secret: true);

// Connection string parameter
var cosmosConnection = builder.AddConnectionString("cosmos");

// Parameter with description (shown in dashboard)
var modelEndpoint = builder.AddParameter("model-endpoint")
    .WithDescription("Azure OpenAI endpoint URL");

// Use parameters in resources
var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithEnvironment("OPENAI_API_KEY", apiKey)
    .WithEnvironment("MODEL_ENDPOINT", modelEndpoint)
    .WithReference(cosmosConnection);
```

**Configure Parameter Values:**

**appsettings.json** (for local development):

```json
{
  "Parameters": {
    "api-key": "local-dev-key",
    "db-password": "local-password",
    "model-endpoint": "https://mymodel.openai.azure.com"
  },
  "ConnectionStrings": {
    "cosmos": "AccountEndpoint=https://localhost:8081/;AccountKey=..."
  }
}
```

**User Secrets** (for sensitive local data):

```bash
dotnet user-secrets init --project AspireAgentApp.AppHost
dotnet user-secrets set "Parameters:api-key" "my-secret-key" --project AspireAgentApp.AppHost
dotnet user-secrets set "Parameters:db-password" "super-secret" --project AspireAgentApp.AppHost
```

**Environment Variables** (for CI/CD):

```bash
export Parameters__api_key="production-key"
export Parameters__db_password="prod-password"
export ConnectionStrings__cosmos="AccountEndpoint=..."
```

### 5.2 Configuration in Service Projects

**appsettings.json:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Agent": {
    "MaxConcurrentTasks": 10,
    "TimeoutSeconds": 300,
    "Model": "gpt-4"
  },
  "Azure": {
    "OpenAI": {
      "Endpoint": "", // Injected via environment variable
      "DeploymentName": "gpt-4"
    }
  }
}
```

**Read Configuration:**

```csharp
// Strongly-typed configuration
public class AgentOptions
{
    public int MaxConcurrentTasks { get; set; }
    public int TimeoutSeconds { get; set; }
    public string Model { get; set; }
}

// Register in Program.cs
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection("Agent"));

// Use in service
public class AgentOrchestrator
{
    private readonly AgentOptions _options;
    
    public AgentOrchestrator(IOptions<AgentOptions> options)
    {
        _options = options.Value;
    }
}
```

### 5.3 Connection String Composition

Build connection strings from parameters:

```csharp
var dbPassword = builder.AddParameter("db-password", secret: true);

var connectionString = builder.AddConnectionString(
    "postgres",
    ReferenceExpression.Create(
        $"Host=postgres;Port=5432;Username=admin;Password={dbPassword};Database=agentdb"
    )
);

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(connectionString);
```

### 5.4 Azure Key Vault Integration

```csharp
// In AppHost
var keyVault = builder.AddAzureKeyVault("keyvault");

var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithReference(keyVault);

// In service (Program.cs)
// Automatically integrates with Azure Key Vault
// Secrets from Key Vault override local configuration
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["ConnectionStrings:keyvault"]),
    new DefaultAzureCredential());
```

### 5.5 Dashboard Parameter Prompts

If parameters aren't configured, the Aspire dashboard prompts for values:

- Shows unresolved parameters on startup
- Provides form to enter values
- Option to save to user secrets
- Supports custom input validation and descriptions

---

## 6. Application Insights Integration

### 6.1 Add Application Insights

**In AppHost:**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Application Insights resource
var appInsights = builder.AddAzureApplicationInsights("monitoring");

// Reference from all services
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(appInsights);

var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithReference(appInsights);

var frontend = builder.AddProject<Projects.Web>("frontend")
    .WithReference(appInsights);
```

**Connection String Injection:**

Aspire automatically injects `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable.

### 6.2 Configure Service Defaults for Azure Monitor

**Extensions.cs in ServiceDefaults:**

```csharp
private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    // Enable Azure Monitor exporter
    if (!string.IsNullOrEmpty(
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    {
        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor();
    }

    return builder;
}
```

**Add Package:**

```bash
dotnet add package Azure.Monitor.OpenTelemetry.AspNetCore
```

### 6.3 Telemetry - The Three Pillars

**1. Logging:**

```csharp
public class AgentService
{
    private readonly ILogger<AgentService> _logger;
    
    public AgentService(ILogger<AgentService> logger)
    {
        _logger = logger;
    }
    
    public async Task ProcessAsync(AgentTask task)
    {
        _logger.LogInformation(
            "Processing task {TaskId} for conversation {ConversationId}",
            task.Id, task.ConversationId);
        
        try
        {
            await ExecuteTaskAsync(task);
            
            _logger.LogInformation(
                "Task {TaskId} completed successfully",
                task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Task {TaskId} failed",
                task.Id);
            throw;
        }
    }
}
```

**2. Distributed Tracing:**

```csharp
// Automatically instrumented via service defaults
// Manual activity creation:
public class AgentOrchestrator
{
    private static readonly ActivitySource _activitySource = 
        new("AgentService.Orchestrator");
    
    public async Task<AgentResponse> OrchestrateAsync(AgentRequest request)
    {
        using var activity = _activitySource.StartActivity("OrchestrateAgent");
        activity?.SetTag("agent.type", request.AgentType);
        activity?.SetTag("conversation.id", request.ConversationId);
        
        // Your orchestration logic
        var response = await ProcessAgentRequestAsync(request);
        
        activity?.SetTag("agent.status", response.Status);
        return response;
    }
}
```

**3. Metrics:**

```csharp
public class AgentMetrics
{
    private static readonly Meter _meter = new("AgentService.Metrics");
    
    private readonly Counter<long> _tasksProcessed;
    private readonly Histogram<double> _taskDuration;
    private readonly ObservableGauge<int> _activeAgents;
    
    public AgentMetrics()
    {
        _tasksProcessed = _meter.CreateCounter<long>(
            "agent.tasks.processed",
            description: "Number of tasks processed");
            
        _taskDuration = _meter.CreateHistogram<double>(
            "agent.task.duration",
            unit: "ms",
            description: "Task processing duration");
            
        _activeAgents = _meter.CreateObservableGauge<int>(
            "agent.active.count",
            () => GetActiveAgentCount(),
            description: "Number of active agents");
    }
    
    public void RecordTaskProcessed(string agentType, double durationMs)
    {
        _tasksProcessed.Add(1, 
            new KeyValuePair<string, object?>("agent.type", agentType));
        _taskDuration.Record(durationMs,
            new KeyValuePair<string, object?>("agent.type", agentType));
    }
}
```

### 6.4 Health Checks

**Configure Health Checks:**

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<AgentHealthCheck>("agent-health")
    .AddCheck<DatabaseHealthCheck>("database-health")
    .AddCheck("external-api", () =>
    {
        // Check external service
        return HealthCheckResult.Healthy();
    }, tags: new[] { "live" });

// Custom health check
public class AgentHealthCheck : IHealthCheck
{
    private readonly IAgentManager _agentManager;
    
    public AgentHealthCheck(IAgentManager agentManager)
    {
        _agentManager = agentManager;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activeAgents = await _agentManager.GetActiveAgentsAsync();
            var data = new Dictionary<string, object>
            {
                { "active_agents", activeAgents.Count },
                { "max_agents", 100 }
            };
            
            return activeAgents.Count < 100
                ? HealthCheckResult.Healthy("Agents are running", data)
                : HealthCheckResult.Degraded("Too many active agents", null, data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Agent check failed", ex);
        }
    }
}
```

**AppHost Health Checks:**

```csharp
// In AppHost
var api = builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health")
    .WithHealthCheckTimeout(TimeSpan.FromSeconds(30));

var frontend = builder.AddProject<Projects.Web>("frontend")
    .WithReference(api)
    .WaitFor(api); // Waits for /health to return 200
```

### 6.5 Query Telemetry in Application Insights

**Kusto Query Examples:**

```kusto
// Failed requests
requests
| where success == false
| where timestamp > ago(1h)
| summarize count() by name, resultCode
| order by count_ desc

// Agent task duration
customMetrics
| where name == "agent.task.duration"
| summarize avg(value), percentile(value, 95) by bin(timestamp, 5m)

// Distributed trace
dependencies
| where operation_Name == "ProcessAgentRequest"
| join kind=inner (traces) on operation_Id
| project timestamp, operation_Name, name, duration, message
| order by timestamp desc

// Exception analysis
exceptions
| where timestamp > ago(24h)
| summarize count() by type, outerMessage
| order by count_ desc
```

---

## 7. Deployment to Azure Container Apps

### 7.1 Deployment Overview

Aspire separates publishing (generating artifacts) from deployment (applying to target):

- **`aspire publish`**: Generates parameterized deployment artifacts
- **`aspire deploy`**: Resolves parameters and deploys to target

### 7.2 Enable Deploy Command

```bash
# Set environment variable (deploy is in preview)
export DOTNET_ASPIRE_ENABLE_DEPLOY_COMMAND=true

# On Windows PowerShell:
$env:DOTNET_ASPIRE_ENABLE_DEPLOY_COMMAND="true"
```

### 7.3 Authenticate with Azure

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "<subscription-id>"

# Show current subscription
az account show
```

### 7.4 Configure Azure Resources in AppHost

**Use Azure Resources Instead of Local Containers:**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Azure resources for production
var cosmosDb = builder.AddAzureCosmosDB("cosmosdb")
    .AddDatabase("conversations");

var storage = builder.AddAzureStorage("storage")
    .AddBlobs("documents")
    .AddQueues("tasks");

var redis = builder.AddAzureRedis("cache");

var appInsights = builder.AddAzureApplicationInsights("monitoring");

// Services reference Azure resources
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(cosmosDb)
    .WithReference(storage)
    .WithReference(redis)
    .WithReference(appInsights);

var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithReference(cosmosDb)
    .WithReference(storage)
    .WithReference(redis)
    .WithReference(appInsights);

var frontend = builder.AddProject<Projects.Web>("frontend")
    .WithReference(api)
    .WithReference(appInsights)
    .WithExternalHttpEndpoints(); // Public endpoint

builder.Build().Run();
```

### 7.5 Deploy to Azure Container Apps

**Simple Deployment:**

```bash
# From AppHost directory
aspire deploy
```

**Deployment Steps:**
1. Validates configuration
2. Generates deployment manifest
3. Provisions Azure resources (Container Apps, ACR, Environment)
4. Builds and pushes Docker images
5. Updates Container Apps with new images
6. Displays endpoint URLs

**Specify Parameters:**

```bash
# Inline parameters
aspire deploy \
    --deployment-param location=eastus \
    --deployment-param environment=production

# Or use parameters file
aspire deploy --deployment-params-file deployment-params.json
```

**deployment-params.json:**

```json
{
  "location": "eastus",
  "environment": "production",
  "resourceGroupName": "rg-aspire-agents",
  "containerRegistry": "cragentapp"
}
```

### 7.6 Monitor Deployment

**View Logs:**

```bash
# Follow container logs
az containerapp logs show \
    --name frontend \
    --resource-group rg-aspire-agents \
    --follow

# View all resources
az resource list \
    --resource-group rg-aspire-agents \
    --output table
```

**Azure Portal:**
- Navigate to Container Apps
- View metrics, logs, revisions
- Configure scaling rules
- Manage secrets and environment variables

### 7.7 Customize Bicep Infrastructure

For advanced scenarios, customize generated Bicep:

```csharp
// In AppHost
var cosmos = builder.AddAzureCosmosDB("cosmosdb")
    .ConfigureInfrastructure(infra =>
    {
        var account = infra.GetProvisionableResources()
            .OfType<CosmosDBAccount>()
            .Single();
        
        // Customize Cosmos DB
        account.Kind = CosmosDBAccountKind.MongoDB;
        account.ConsistencyPolicy = new()
        {
            DefaultConsistencyLevel = DefaultConsistencyLevel.Strong
        };
        account.Tags.Add("Project", "AgentApp");
        account.Tags.Add("Environment", "Production");
    });
```

### 7.8 CI/CD Pipeline Integration

**GitHub Actions Example:**

```yaml
name: Deploy to Azure Container Apps

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Install Aspire CLI
        run: dotnet tool install -g aspire-cli
      
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      - name: Deploy to Azure
        run: |
          cd src/AspireAgentApp.AppHost
          aspire deploy \
            --deployment-param location=eastus \
            --deployment-param environment=production
        env:
          DOTNET_ASPIRE_ENABLE_DEPLOY_COMMAND: true
```

### 7.9 Clean Up Resources

```bash
# Delete entire resource group
az group delete --name rg-aspire-agents --yes --no-wait
```

---

## 8. Best Practices for .NET 10

### 8.1 Project Structure

**Recommended Organization:**

```
AspireAgentApp/
├── src/
│   ├── AspireAgentApp.AppHost/          # Orchestration
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── AspireAgentApp.AppHost.csproj
│   │
│   ├── AspireAgentApp.ServiceDefaults/  # Shared defaults
│   │   ├── Extensions.cs
│   │   └── AspireAgentApp.ServiceDefaults.csproj
│   │
│   ├── AspireAgentApp.Api/              # API service
│   │   ├── Program.cs
│   │   ├── Controllers/
│   │   └── AspireAgentApp.Api.csproj
│   │
│   ├── AspireAgentApp.AgentService/     # Agent orchestration
│   │   ├── Program.cs
│   │   ├── Agents/
│   │   ├── Services/
│   │   └── AspireAgentApp.AgentService.csproj
│   │
│   ├── AspireAgentApp.Web/              # Frontend
│   │   ├── Program.cs
│   │   ├── Components/
│   │   └── AspireAgentApp.Web.csproj
│   │
│   └── AspireAgentApp.Shared/           # Shared models
│       ├── Models/
│       ├── Contracts/
│       └── AspireAgentApp.Shared.csproj
│
├── tests/
│   ├── AspireAgentApp.Api.Tests/
│   ├── AspireAgentApp.AgentService.Tests/
│   └── AspireAgentApp.IntegrationTests/
│
├── AspireAgentApp.sln
└── README.md
```

### 8.2 Service Defaults Best Practices

**Keep ServiceDefaults Minimal:**

```csharp
// Only include framework-agnostic defaults
// Don't add business logic or domain models here

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        
        return builder;
    }
}
```

**For Non-ASP.NET Projects:**

Create custom service defaults without `Microsoft.AspNetCore.App` dependency.

### 8.3 Configuration Best Practices

**1. Environment-Specific Settings:**

```json
// appsettings.Development.json
{
  "Agent": {
    "MaxConcurrentTasks": 5,
    "TimeoutSeconds": 60
  }
}

// appsettings.Production.json
{
  "Agent": {
    "MaxConcurrentTasks": 50,
    "TimeoutSeconds": 300
  }
}
```

**2. Never Hardcode Secrets:**

```csharp
// BAD
var apiKey = "sk-proj-abc123...";

// GOOD
var apiKey = builder.Configuration["OpenAI:ApiKey"];
```

**3. Use Strongly-Typed Configuration:**

```csharp
// Options class
public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
}

// Register
builder.Services.Configure<OpenAIOptions>(
    builder.Configuration.GetSection("OpenAI"));
    
// Validate on startup
builder.Services.AddOptions<OpenAIOptions>()
    .BindConfiguration("OpenAI")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### 8.4 Resource Management

**1. Use Persistent Lifetimes for Stateful Resources:**

```csharp
var mongo = builder.AddMongoDB("mongodb")
    .WithLifetime(ContainerLifetime.Persistent); // Survives restarts
```

**2. Always Use Data Volumes:**

```csharp
var postgres = builder.AddPostgres("db")
    .WithDataVolume(); // Data survives container recreation
```

**3. Implement WaitFor for Dependencies:**

```csharp
var db = builder.AddPostgres("db").AddDatabase("appdb");
var cache = builder.AddRedis("cache");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WithReference(cache)
    .WaitFor(db)      // Wait for database
    .WaitFor(cache);  // Wait for cache
```

### 8.5 Agent Framework Integration

**Example: Semantic Kernel Integration:**

```csharp
// In AgentService Program.cs
builder.Services.AddKernel()
    .AddOpenAIChatCompletion(
        modelId: builder.Configuration["OpenAI:DeploymentName"],
        apiKey: builder.Configuration["OpenAI:ApiKey"]);

// Register agent services
builder.Services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
builder.Services.AddScoped<IChatAgent, ChatAgent>();

// AgentOrchestrator implementation
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly Kernel _kernel;
    private readonly IMongoDatabase _database;
    private readonly ILogger<AgentOrchestrator> _logger;
    
    public AgentOrchestrator(
        Kernel kernel,
        IMongoDatabase database,
        ILogger<AgentOrchestrator> logger)
    {
        _kernel = kernel;
        _database = database;
        _logger = logger;
    }
    
    public async Task<AgentResponse> ProcessAsync(AgentRequest request)
    {
        using var activity = Activity.Current?.Source.StartActivity("ProcessAgent");
        activity?.SetTag("conversation.id", request.ConversationId);
        
        _logger.LogInformation(
            "Processing request for conversation {ConversationId}",
            request.ConversationId);
        
        // Load conversation history from MongoDB
        var history = await LoadHistoryAsync(request.ConversationId);
        
        // Execute agent
        var response = await _kernel.InvokePromptAsync(
            request.Message,
            new KernelArguments
            {
                ["history"] = history,
                ["context"] = request.Context
            });
        
        // Save to database
        await SaveResponseAsync(request.ConversationId, response);
        
        return new AgentResponse
        {
            Message = response.GetValue<string>(),
            ConversationId = request.ConversationId
        };
    }
}
```

### 8.6 Testing Strategies

**Integration Tests with Aspire:**

```csharp
// AspireAgentApp.IntegrationTests
public class AgentServiceTests : IClassFixture<AspireWebApplicationFactory<Program>>
{
    private readonly AspireWebApplicationFactory<Program> _factory;
    
    public AgentServiceTests(AspireWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task ProcessAgent_ReturnsValidResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new AgentRequest
        {
            ConversationId = "test-123",
            Message = "Hello, agent!"
        };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/agent/process", request);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Message);
    }
}

// WebApplicationFactory
public class AspireWebApplicationFactory<TProgram> 
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles
            services.RemoveAll<IMongoDatabase>();
            services.AddSingleton<IMongoDatabase>(
                new MockMongoDatabase());
        });
    }
}
```

### 8.7 Logging Best Practices

**Structured Logging:**

```csharp
// Good: Structured with named properties
_logger.LogInformation(
    "Agent task {TaskId} processed in {Duration}ms for conversation {ConversationId}",
    task.Id,
    duration,
    task.ConversationId);

// Bad: String interpolation
_logger.LogInformation($"Agent task {task.Id} processed");
```

**Log Levels:**

```csharp
// Trace: Very detailed, typically only in development
_logger.LogTrace("Entering method with parameters: {Params}", params);

// Debug: Detailed for debugging
_logger.LogDebug("Cache miss for key {Key}", key);

// Information: General flow
_logger.LogInformation("Agent task started: {TaskId}", taskId);

// Warning: Unexpected but recoverable
_logger.LogWarning("Retry attempt {Attempt} for task {TaskId}", attempt, taskId);

// Error: Operation failed
_logger.LogError(ex, "Task {TaskId} failed", taskId);

// Critical: Catastrophic failure
_logger.LogCritical("Database connection lost");
```

### 8.8 Performance Optimization

**1. Use Connection Pooling:**

```csharp
// MongoDB connection pooling
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
        ["ConnectionStrings:mongodb"];
    var settings = MongoClientSettings.FromConnectionString(connectionString);
    settings.MaxConnectionPoolSize = 100;
    settings.MinConnectionPoolSize = 10;
    return new MongoClient(settings);
});
```

**2. Implement Caching:**

```csharp
// In AppHost
var redis = builder.AddRedis("cache");

// In service
builder.AddRedisOutputCache("cache");
builder.AddRedisDistributedCache("cache");

// Usage
[OutputCache(Duration = 60)]
[HttpGet("agents")]
public async Task<IActionResult> GetAgents()
{
    // Cached for 60 seconds
    return Ok(await _agentService.GetAllAsync());
}
```

**3. Use HTTP/2 and gRPC:**

```csharp
// Enable HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});
```

### 8.9 Security Best Practices

**1. Use Managed Identities:**

```csharp
// In AppHost for Azure deployment
var storage = builder.AddAzureStorage("storage");
var keyVault = builder.AddAzureKeyVault("keyvault");

// Automatically uses Managed Identity in Azure Container Apps
```

**2. Disable Unauthenticated Endpoints in Production:**

```csharp
// Service defaults - only expose in development
public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
    }
    
    return app;
}
```

**3. Validate Input:**

```csharp
public class AgentRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
    
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9-_]+$")]
    public string ConversationId { get; set; } = string.Empty;
}
```

### 8.10 Deployment Checklist

**Pre-Deployment:**
- [ ] All secrets moved to Azure Key Vault or parameters
- [ ] Health checks configured for all services
- [ ] Telemetry and Application Insights configured
- [ ] Resource limits and scaling rules defined
- [ ] Integration tests pass
- [ ] Performance testing completed

**Post-Deployment:**
- [ ] Verify all services are healthy in Azure Portal
- [ ] Check Application Insights for errors
- [ ] Test public endpoints
- [ ] Verify service-to-service communication
- [ ] Monitor resource utilization
- [ ] Set up alerts for critical metrics

---

## Summary

.NET Aspire 13 provides a comprehensive, code-first approach to orchestrating distributed applications with agent frameworks. Key takeaways:

1. **AppHost is Central**: Define your entire application architecture in code
2. **Built-in Observability**: OpenTelemetry integration out of the box
3. **Local Development**: Seamless Docker orchestration with live dashboard
4. **Service Discovery**: Automatic configuration injection and DNS resolution
5. **Flexible Deployment**: Publish to Azure Container Apps, Kubernetes, or Docker Compose
6. **Production-Ready**: Health checks, resilience, secrets management included
7. **Extensible**: Build custom integrations for any service or framework

**Next Steps:**
1. Install Aspire CLI and .NET 10 SDK
2. Create your first Aspire project
3. Add agent framework integration (Semantic Kernel, LangChain, etc.)
4. Configure local databases and storage
5. Deploy to Azure Container Apps for production

**Resources:**
- [Official Aspire Documentation](https://aspire.dev)
- [Aspire GitHub Repository](https://github.com/dotnet/aspire)
- [Aspire Samples](https://github.com/dotnet/aspire-samples)
- [.NET 10 Documentation](https://learn.microsoft.com/dotnet/)

---

**Document Version**: 1.0  
**Last Updated**: December 30, 2024  
**Aspire Version**: 13.0  
**.NET Version**: 10.0
