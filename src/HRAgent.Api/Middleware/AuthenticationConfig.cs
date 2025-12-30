using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace HRAgent.Api.Middleware;

/// <summary>
/// Configuration for Microsoft Entra ID (Azure AD) authentication
/// </summary>
public static class AuthenticationConfig
{
    /// <summary>
    /// Registers Microsoft Entra ID authentication with JWT Bearer tokens
    /// </summary>
    public static IServiceCollection AddEntraIdAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Microsoft Entra ID authentication using Microsoft.Identity.Web
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();
        
        // Authorization policies
        services.AddAuthorization(options =>
        {
            // Require authenticated user for all endpoints by default
            options.FallbackPolicy = options.DefaultPolicy;
            
            // Custom policy: Require employee role
            options.AddPolicy("EmployeeOnly", policy =>
                policy.RequireClaim("extension_EmployeeId"));
        });
        
        return services;
    }
}
