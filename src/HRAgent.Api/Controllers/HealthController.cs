using Microsoft.AspNetCore.Mvc;

namespace HRAgent.Api.Controllers;

/// <summary>
/// Health check endpoints for monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Basic health check - returns 200 OK if service is running
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTimeOffset.UtcNow,
            service = "HRAgent.Api",
        });
    }

    /// <summary>
    /// Readiness check - indicates if service is ready to handle requests
    /// </summary>
    [HttpGet("ready")]
    public IActionResult Ready()
    {
        // Add checks for required dependencies (database connections, etc.)
        // For now, return healthy if the service is running
        return Ok(new
        {
            status = "Ready",
            timestamp = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>
    /// Liveness check - indicates if service should be restarted
    /// </summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "Alive",
            timestamp = DateTimeOffset.UtcNow,
        });
    }
}
