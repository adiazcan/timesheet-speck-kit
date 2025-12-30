using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;

namespace HRAgent.Api.Configuration;

/// <summary>
/// Configuration for Azure AI Foundry (Azure OpenAI) client
/// </summary>
public static class AzureAIFoundryConfig
{
    /// <summary>
    /// Registers Azure OpenAI client with dependency injection
    /// </summary>
    public static IServiceCollection AddAzureAIFoundry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["AzureAI:Endpoint"] 
            ?? throw new InvalidOperationException("AzureAI:Endpoint configuration is missing");
        
        var apiKey = configuration["AzureAI:ApiKey"];
        
        // Register AzureOpenAIClient as singleton
        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var endpointUri = new Uri(endpoint);
            
            // Use API key if provided (development), otherwise use Managed Identity (production)
            if (!string.IsNullOrEmpty(apiKey))
            {
                return new AzureOpenAIClient(endpointUri, new AzureKeyCredential(apiKey));
            }
            else
            {
                // Use DefaultAzureCredential for managed identity in production
                return new AzureOpenAIClient(endpointUri, new DefaultAzureCredential());
            }
        });
        
        // Register ChatClient for the deployment
        services.AddScoped(sp =>
        {
            var client = sp.GetRequiredService<AzureOpenAIClient>();
            var deploymentName = configuration["AzureAI:DeploymentName"] ?? "gpt-4o";
            return client.GetChatClient(deploymentName);
        });
        
        return services;
    }
}
