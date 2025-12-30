var builder = DistributedApplication.CreateBuilder(args);

// MongoDB for local development (matches DocumentDB API)
var mongodb = builder.AddMongoDB("mongodb")
    .WithDataVolume()
    .AddDatabase("hrapp");

// Azurite - Azure Storage Emulator (Blob, Queue, Table)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("audit-logs");

// Backend API
var api = builder.AddProject<Projects.HRAgent_Api>("api")
    .WithReference(mongodb)
    .WithReference(blobs);

// Frontend (Vite React app)
var frontend = builder.AddViteApp("frontend", "../../frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithEndpoint(endpointName: "http", endpoint =>
    {
        endpoint.Port = builder.ExecutionContext.IsRunMode ?
                        5173 : null;
    });

builder.Build().Run();
