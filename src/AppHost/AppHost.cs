var builder = DistributedApplication.CreateBuilder(args);

// MongoDB - Used as Cosmos DB emulator (DocumentDB API compatible) for local development
// In production, this connects to Azure Cosmos DB via connection string
var mongodb = builder.AddMongoDB("mongodb")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("hrapp-local");

// Azure Storage - Blob storage for audit logs with 7-year retention
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("audit-logs");

// Application Insights - Telemetry and monitoring
// Note: For production, configure via environment variables or appsettings.json
// APPLICATIONINSIGHTS_CONNECTION_STRING or ApplicationInsights:ConnectionString
var appInsights = builder.ExecutionContext.IsPublishMode
    ? builder.AddParameter("app-insights-connection-string", secret: true)
    : builder.AddParameter("app-insights-connection-string", secret: false); // Optional in dev

var azureAIFoundryEndpoint = builder.AddParameter("azure-ai-foundry-endpoint", secret: false);
var azureAIFoundryApiKey = builder.AddParameter("azure-ai-foundry-key", secret: true);

var factorialApiKey = builder.AddParameter("factorial-api-key", secret: true);

var api = builder.AddProject<Projects.HRAgent_Api>("api")
    .WithReference(mongodb)
    .WithReference(blobs)
    .WithEnvironment("AzureAI__Endpoint", azureAIFoundryEndpoint)
    .WithEnvironment("AzureAI__ApiKey", azureAIFoundryApiKey)
    .WithEnvironment("FactorialHR__ApiKey", factorialApiKey)
    .WithEnvironment("ApplicationInsights__ConnectionString", appInsights)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.ExecutionContext.IsPublishMode ? "Production" : "Development")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = builder.ExecutionContext.IsRunMode ? 5000 : null;
    });

var frontend = builder.AddViteApp("frontend", "../../frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("http"))
    .WithEndpoint(endpointName: "http", endpoint =>
    {
        endpoint.Port = builder.ExecutionContext.IsRunMode ? 5173 : null;
    });

builder.Build().Run();
