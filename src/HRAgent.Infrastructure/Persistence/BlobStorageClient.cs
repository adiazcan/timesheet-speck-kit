using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using HRAgent.Contracts.Models;

namespace HRAgent.Infrastructure.Persistence;

/// <summary>
/// Configuration for Azure Blob Storage client for audit logging
/// </summary>
public static class BlobStorageConfig
{
    /// <summary>
    /// Registers Blob Storage client with dependency injection
    /// </summary>
    public static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        var isDevelopment = environment == "Development";
        
        if (isDevelopment)
        {
            // Local Azurite emulator
            var connectionString = configuration.GetConnectionString("storage")
                ?? "UseDevelopmentStorage=true";
            
            services.AddSingleton<BlobServiceClient>(_ =>
                new BlobServiceClient(connectionString));
        }
        else
        {
            // Production Azure Blob Storage
            var storageEndpoint = configuration["BlobStorage:Endpoint"]
                ?? throw new InvalidOperationException("BlobStorage:Endpoint configuration is missing");
            
            var storageKey = configuration["BlobStorage:Key"];
            
            services.AddSingleton<BlobServiceClient>(sp =>
            {
                var endpointUri = new Uri(storageEndpoint);
                
                // Use storage key if provided, otherwise use Managed Identity
                if (!string.IsNullOrEmpty(storageKey))
                {
                    return new BlobServiceClient(new Uri(storageEndpoint), new Azure.Storage.StorageSharedKeyCredential(
                        configuration["BlobStorage:AccountName"] ?? "hrapp",
                        storageKey));
                }
                else
                {
                    return new BlobServiceClient(endpointUri, new DefaultAzureCredential());
                }
            });
        }
        
        // Register audit logger service
        services.AddScoped<IAuditLogger, BlobStorageAuditLogger>();
        
        return services;
    }
}

/// <summary>
/// Interface for audit logging operations
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLogEntry>> GetLogsAsync(string employeeId, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Blob Storage implementation of audit logger
/// Stores logs with hierarchical path: {yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json
/// </summary>
public class BlobStorageAuditLogger : IAuditLogger
{
    private readonly BlobContainerClient _containerClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _containerInitialized = false;
    
    public BlobStorageAuditLogger(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        var containerName = configuration["BlobStorage:AuditLogsContainer"] ?? "audit-logs";
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
    
    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_containerInitialized)
        {
            return;
        }
        
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!_containerInitialized)
            {
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                _containerInitialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);
        
        // Generate blob path: {yyyy}/{MM}/{dd}/{employeeId}_{timestamp}_{guid}.json
        var timestamp = entry.Timestamp;
        var blobPath = $"{timestamp.Year:D4}/{timestamp.Month:D2}/{timestamp.Day:D2}/" +
                      $"{entry.EmployeeId}_{timestamp.ToUnixTimeMilliseconds()}_{entry.Id}.json";
        
        var blobClient = _containerClient.GetBlobClient(blobPath);
        
        // Serialize to JSON
        var json = JsonSerializer.Serialize(entry, _jsonOptions);
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        
        // Upload with metadata
        var metadata = new Dictionary<string, string>
        {
            { "employeeId", entry.EmployeeId },
            { "action", entry.Action },
            { "timestamp", timestamp.ToString("O") }
        };
        
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            Metadata = metadata,
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            }
        }, cancellationToken);
    }
    
    public async Task<IEnumerable<AuditLogEntry>> GetLogsAsync(string employeeId, DateOnly date, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);
        
        // List blobs in the date folder
        var prefix = $"{date.Year:D4}/{date.Month:D2}/{date.Day:D2}/{employeeId}_";
        var logs = new List<AuditLogEntry>();
        
        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            
            var json = response.Value.Content.ToString();
            var entry = JsonSerializer.Deserialize<AuditLogEntry>(json, _jsonOptions);
            
            if (entry != null)
            {
                logs.Add(entry);
            }
        }
        
        return logs.OrderBy(l => l.Timestamp);
    }
}
