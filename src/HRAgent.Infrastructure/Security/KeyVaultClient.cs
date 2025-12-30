using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRAgent.Infrastructure.Security;

/// <summary>
/// Configuration for Azure Key Vault client
/// </summary>
public static class KeyVaultConfig
{
    /// <summary>
    /// Registers Azure Key Vault client with dependency injection
    /// </summary>
    public static IServiceCollection AddKeyVault(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keyVaultEndpoint = configuration["KeyVault:Endpoint"];
        
        if (string.IsNullOrEmpty(keyVaultEndpoint))
        {
            // Key Vault not configured - skip registration (development scenario)
            return services;
        }
        
        services.AddSingleton<SecretClient>(sp =>
        {
            var vaultUri = new Uri(keyVaultEndpoint);
            return new SecretClient(vaultUri, new DefaultAzureCredential());
        });
        
        services.AddScoped<ISecretsManager, KeyVaultSecretsManager>();
        
        return services;
    }
}

/// <summary>
/// Interface for managing secrets from Key Vault
/// </summary>
public interface ISecretsManager
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Key Vault implementation of secrets manager
/// </summary>
public class KeyVaultSecretsManager : ISecretsManager
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultSecretsManager> _logger;
    
    public KeyVaultSecretsManager(SecretClient secretClient, ILogger<KeyVaultSecretsManager> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }
    
    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving secret {SecretName} from Key Vault", secretName);
            
            var secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
            throw;
        }
    }
    
    public async Task SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Setting secret {SecretName} in Key Vault", secretName);
            
            await _secretClient.SetSecretAsync(secretName, secretValue, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret {SecretName} in Key Vault", secretName);
            throw;
        }
    }
}
