using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HRAgent.Api.Controllers;

/// <summary>
/// Health check endpoints for monitoring
/// Integrates with ASP.NET Core Health Checks system
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly HealthCheckService _healthCheckService;

    public HealthController(
        ILogger<HealthController> logger,
        HealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Basic health check - returns 200 OK if service is running
    /// Checks all registered health checks (Cosmos DB, Blob Storage, etc.)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync();
        
        var response = new
        {
            status = healthReport.Status.ToString(),
            timestamp = DateTimeOffset.UtcNow,
            service = "HRAgent.Api",
            checks = healthReport.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };

        var statusCode = healthReport.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Readiness check - indicates if service is ready to handle requests
    /// Uses "ready" tag to filter readiness-specific health checks
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains("ready"));
        
        var response = new
        {
            status = healthReport.Status.ToString(),
            timestamp = DateTimeOffset.UtcNow,
        };

        var statusCode = healthReport.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Liveness check - indicates if service should be restarted
    /// Uses "live" tag to filter liveness-specific health checks
    /// </summary>
    [HttpGet("live")]
    public async Task<IActionResult> Live()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains("live"));
        
        var response = new
        {
            status = healthReport.Status.ToString(),
            timestamp = DateTimeOffset.UtcNow,
        };

        var statusCode = healthReport.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }
}
