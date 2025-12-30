using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRAgent.Infrastructure.Telemetry;

/// <summary>
/// Configuration for Azure Application Insights telemetry
/// </summary>
public static class ApplicationInsightsConfig
{
    /// <summary>
    /// Registers Application Insights with custom telemetry tracking
    /// </summary>
    public static IServiceCollection AddApplicationInsightsTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Application Insights is automatically configured via service defaults
        // This adds custom telemetry processors and initializers
        
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            options.EnableAdaptiveSampling = true;
            options.EnableQuickPulseMetricStream = true;
        });
        
        // Add custom telemetry initializer for HR Agent context
        services.AddSingleton<ITelemetryInitializer, HRAgentTelemetryInitializer>();
        
        // Register TelemetryClient wrapper for custom metrics
        services.AddScoped<ICustomTelemetry, CustomTelemetryService>();
        
        return services;
    }
}

/// <summary>
/// Custom telemetry initializer to add HR Agent-specific properties
/// </summary>
public class HRAgentTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = "HRAgent.Api";
        
        // Add custom properties
        if (telemetry is Microsoft.ApplicationInsights.DataContracts.RequestTelemetry requestTelemetry)
        {
            requestTelemetry.Properties["service"] = "hr-chat-agent";
            requestTelemetry.Properties["feature"] = "001-hr-chat-agent";
        }
    }
}

/// <summary>
/// Interface for custom telemetry operations
/// </summary>
public interface ICustomTelemetry
{
    void TrackIntentClassification(string intent, double confidence, long durationMs);
    void TrackFactorialHRCall(string operation, bool success, long durationMs, int? statusCode = null);
    void TrackAgentExecution(string agentName, bool success, long durationMs);
    void TrackConversationMetrics(int messageCount, int toolCallCount, long totalDurationMs);
}

/// <summary>
/// Service for tracking custom Application Insights metrics
/// </summary>
public class CustomTelemetryService : ICustomTelemetry
{
    private readonly TelemetryClient _telemetryClient;
    
    public CustomTelemetryService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }
    
    public void TrackIntentClassification(string intent, double confidence, long durationMs)
    {
        _telemetryClient.TrackEvent("IntentClassified", new Dictionary<string, string>
        {
            { "intent", intent },
            { "confidence", confidence.ToString("F2") }
        }, new Dictionary<string, double>
        {
            { "classificationDurationMs", durationMs },
            { "confidenceScore", confidence }
        });
        
        _telemetryClient.TrackMetric("IntentClassification.Duration", durationMs);
        _telemetryClient.TrackMetric($"IntentClassification.{intent}.Confidence", confidence);
    }
    
    public void TrackFactorialHRCall(string operation, bool success, long durationMs, int? statusCode = null)
    {
        _telemetryClient.TrackEvent("FactorialHRCall", new Dictionary<string, string>
        {
            { "operation", operation },
            { "success", success.ToString() },
            { "statusCode", statusCode?.ToString() ?? "N/A" }
        }, new Dictionary<string, double>
        {
            { "durationMs", durationMs }
        });
        
        _telemetryClient.TrackMetric($"FactorialHR.{operation}.Duration", durationMs);
        
        if (!success)
        {
            _telemetryClient.TrackMetric("FactorialHR.Failures", 1);
        }
    }
    
    public void TrackAgentExecution(string agentName, bool success, long durationMs)
    {
        _telemetryClient.TrackEvent("AgentExecution", new Dictionary<string, string>
        {
            { "agent", agentName },
            { "success", success.ToString() }
        }, new Dictionary<string, double>
        {
            { "executionDurationMs", durationMs }
        });
        
        _telemetryClient.TrackMetric($"Agent.{agentName}.Duration", durationMs);
    }
    
    public void TrackConversationMetrics(int messageCount, int toolCallCount, long totalDurationMs)
    {
        _telemetryClient.TrackEvent("ConversationCompleted", new Dictionary<string, string>
        {
            { "messageCount", messageCount.ToString() },
            { "toolCallCount", toolCallCount.ToString() }
        }, new Dictionary<string, double>
        {
            { "totalDurationMs", totalDurationMs },
            { "averageMessageDurationMs", messageCount > 0 ? (double)totalDurationMs / messageCount : 0 }
        });
        
        _telemetryClient.TrackMetric("Conversation.MessageCount", messageCount);
        _telemetryClient.TrackMetric("Conversation.ToolCallCount", toolCallCount);
        _telemetryClient.TrackMetric("Conversation.TotalDuration", totalDurationMs);
    }
}
