using HRAgent.Contracts.Factorial;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Jobs;

/// <summary>
/// Background service that processes the submission retry queue
/// Runs continuously checking for items ready to retry
/// </summary>
public class SubmissionRetryProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubmissionRetryProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    public SubmissionRetryProcessor(
        IServiceProvider serviceProvider,
        ILogger<SubmissionRetryProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Submission Retry Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
#pragma warning disable CA1031 // Do not catch general exception types - Background service must continue running
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex, "Error processing submission queue");
            }

            // Wait before next poll
            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Submission Retry Processor stopped");
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var submissionQueue = scope.ServiceProvider.GetRequiredService<Services.SubmissionQueue>();
        var factorialService = scope.ServiceProvider.GetRequiredService<Services.FactorialHRService>();
        var auditLogger = scope.ServiceProvider.GetRequiredService<Services.AuditLogger>();

        // Get pending items ready for retry
        var pendingItems = await submissionQueue.GetPendingItemsAsync(limit: 50);

        if (pendingItems.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} pending submission queue items", pendingItems.Count);

        foreach (var item in pendingItems)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Try to lock the item (prevent duplicate processing)
            if (!await submissionQueue.TryLockItemAsync(item))
            {
                continue;
            }

            // Check if ready for retry
            if (!item.IsReadyForRetry())
            {
                continue;
            }

            try
            {
                var startTime = DateTimeOffset.UtcNow;
                bool success = false;
                string? errorMessage = null;
                int? statusCode = null;

                // Retry the submission based on action type
                if (item.Action == "clock-in")
                {
                    var request = new ClockInRequest
                    {
                        EmployeeId = item.EmployeeId,
                        Timestamp = item.Timestamp,
                        Notes = item.UserMessage,
                    };

                    try
                    {
                        var response = await factorialService.ClockInAsync(request);
                        success = true;
                        statusCode = 200;

                        // Log successful retry
                        var duration = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                        await auditLogger.LogClockInAsync(
                            item.EmployeeId,
                            item.ConversationThreadId,
                            item.MessageId,
                            item.Timestamp,
                            statusCode.Value,
                            durationMs: duration);
                    }
#pragma warning disable CA1031 // Do not catch general exception types - Must continue processing queue items
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        errorMessage = ex.Message;
                        statusCode = ex is Services.FactorialHRException ? 502 : 500;
                    }
                }
                else if (item.Action == "clock-out")
                {
                    var request = new ClockOutRequest
                    {
                        EmployeeId = item.EmployeeId,
                        Timestamp = item.Timestamp,
                        Notes = item.UserMessage,
                    };

                    try
                    {
                        var response = await factorialService.ClockOutAsync(request);
                        success = true;
                        statusCode = 200;

                        // Log successful retry
                        var duration = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                        await auditLogger.LogClockOutAsync(
                            item.EmployeeId,
                            item.ConversationThreadId,
                            item.MessageId,
                            item.Timestamp,
                            response.TotalHours,
                            statusCode.Value,
                            durationMs: duration);
                    }
#pragma warning disable CA1031 // Do not catch general exception types - Must continue processing queue items
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        errorMessage = ex.Message;
                        statusCode = ex is Services.FactorialHRException ? 502 : 500;
                    }
                }

                // Update queue item after retry
                await submissionQueue.UpdateAfterRetryAsync(item, success, errorMessage, statusCode);

                if (success)
                {
                    _logger.LogInformation(
                        "Successfully retried {Action} for employee {EmployeeId} after {RetryCount} attempts",
                        item.Action, item.EmployeeId, item.RetryCount);
                }
                else if (item.IsRetryExhausted())
                {
                    _logger.LogError(
                        "Permanently failed {Action} for employee {EmployeeId} after {MaxRetries} retries: {Error}",
                        item.Action, item.EmployeeId, item.MaxRetries, errorMessage);

                    // TODO: Send notification to user about permanent failure (T028h)
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - Must continue processing remaining queue items
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogError(ex, "Error processing queue item {ItemId}", item.Id);
                
                // Update with error
                await submissionQueue.UpdateAfterRetryAsync(
                    item,
                    success: false,
                    errorMessage: ex.Message,
                    statusCode: 500);
            }
        }
    }
}
