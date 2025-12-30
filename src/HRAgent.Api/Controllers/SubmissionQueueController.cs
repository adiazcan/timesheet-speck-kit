using HRAgent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRAgent.Api.Controllers;

/// <summary>
/// Controller for checking submission queue status
/// </summary>
[ApiController]
[Route("api/submission-queue")]
public class SubmissionQueueController : ControllerBase
{
    private readonly SubmissionQueue _submissionQueue;
    private readonly ILogger<SubmissionQueueController> _logger;

    public SubmissionQueueController(
        SubmissionQueue submissionQueue,
        ILogger<SubmissionQueueController> logger)
    {
        _submissionQueue = submissionQueue;
        _logger = logger;
    }

    /// <summary>
    /// Gets queue status for an employee
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <returns>List of queued submissions</returns>
    [HttpGet("{employeeId}")]
    public async Task<IActionResult> GetEmployeeQueue(string employeeId)
    {
        var queueItems = await _submissionQueue.GetEmployeeQueueAsync(employeeId);
        
        return Ok(new
        {
            employeeId,
            queuedCount = queueItems.Count,
            items = queueItems.Select(item => new
            {
                id = item.Id,
                action = item.Action,
                timestamp = item.Timestamp,
                status = item.Status,
                retryCount = item.RetryCount,
                maxRetries = item.MaxRetries,
                nextRetryAt = item.NextRetryAt,
                lastError = item.LastError,
                createdAt = item.CreatedAt,
            }).ToList(),
        });
    }

    /// <summary>
    /// Gets overall queue statistics
    /// </summary>
    /// <returns>Queue statistics</returns>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _submissionQueue.GetStatisticsAsync();
        
        return Ok(new
        {
            pending = stats.PendingCount,
            processing = stats.ProcessingCount,
            completed = stats.CompletedCount,
            failed = stats.FailedCount,
            total = stats.TotalCount,
            timestamp = DateTimeOffset.UtcNow,
        });
    }
}
