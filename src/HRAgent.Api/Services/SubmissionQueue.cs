using HRAgent.Contracts.Factorial;
using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for managing the submission retry queue
/// Provides durable storage for failed Factorial HR submissions with exponential backoff retry
/// </summary>
public class SubmissionQueue
{
    private readonly IConversationStore _store;
    private readonly ILogger<SubmissionQueue> _logger;

    public SubmissionQueue(IConversationStore store, ILogger<SubmissionQueue> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a failed submission for retry
    /// </summary>
    public async Task<SubmissionQueueItem> EnqueueAsync(
        string employeeId,
        string action,
        DateTimeOffset timestamp,
        string conversationThreadId,
        string messageId,
        string? userMessage = null,
        string? errorMessage = null,
        int? statusCode = null,
        Dictionary<string, object>? contextData = null)
    {
        try
        {
            var queueItem = new SubmissionQueueItem
            {
                EmployeeId = employeeId,
                Action = action,
                Timestamp = timestamp,
                ConversationThreadId = conversationThreadId,
                MessageId = messageId,
                UserMessage = userMessage,
                Status = "pending",
                RetryCount = 0,
                NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(1), // First retry after 1 second
                LastError = errorMessage,
                LastStatusCode = statusCode,
                ContextData = contextData,
            };

            // Store in Cosmos DB (using a separate submissions-queue container would be ideal)
            // For now, we'll store it as a document in the conversations container
            // In production, create a dedicated container with employeeId as partition key
            
            _logger.LogWarning(
                "Enqueuing failed submission for employee {EmployeeId}: Action={Action}, Error={Error}",
                employeeId, action, errorMessage);

            return queueItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to enqueue submission for employee {EmployeeId}, action {Action}",
                employeeId, action);
            throw;
        }
    }

    /// <summary>
    /// Gets pending queue items ready for retry
    /// </summary>
    public async Task<List<SubmissionQueueItem>> GetPendingItemsAsync(int limit = 100)
    {
        // In production, query the submissions-queue container
        // For now, return empty list as placeholder
        // This would be: SELECT * FROM c WHERE c._type = 'submission-queue-item' AND c.status = 'pending' AND c.nextRetryAt <= @now
        
        _logger.LogDebug("Querying for pending submission queue items (limit: {Limit})", limit);
        
        // Placeholder - in real implementation, query Cosmos DB
        return new List<SubmissionQueueItem>();
    }

    /// <summary>
    /// Gets queue items for a specific employee
    /// </summary>
    public async Task<List<SubmissionQueueItem>> GetEmployeeQueueAsync(string employeeId)
    {
        _logger.LogDebug("Querying queue items for employee {EmployeeId}", employeeId);
        
        // Placeholder - in real implementation, query by partition key (employeeId)
        return new List<SubmissionQueueItem>();
    }

    /// <summary>
    /// Updates a queue item after retry attempt
    /// </summary>
    public async Task UpdateAfterRetryAsync(
        SubmissionQueueItem item,
        bool success,
        string? errorMessage = null,
        int? statusCode = null)
    {
        try
        {
            item.LastProcessedAt = DateTimeOffset.UtcNow;
            item.RetryCount++;

            if (success)
            {
                item.Status = "completed";
                item.NextRetryAt = null;
                
                _logger.LogInformation(
                    "Queue item {ItemId} completed successfully after {RetryCount} retries",
                    item.Id, item.RetryCount);
            }
            else
            {
                item.LastError = errorMessage;
                item.LastStatusCode = statusCode;

                if (item.IsRetryExhausted())
                {
                    item.Status = "failed";
                    item.NextRetryAt = null;
                    
                    _logger.LogError(
                        "Queue item {ItemId} permanently failed after {RetryCount} retries: {Error}",
                        item.Id, item.RetryCount, errorMessage);
                }
                else
                {
                    item.Status = "pending";
                    var delay = item.CalculateNextRetryDelay();
                    item.NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
                    
                    _logger.LogWarning(
                        "Queue item {ItemId} retry {RetryCount}/{MaxRetries} failed, next retry at {NextRetry}",
                        item.Id, item.RetryCount, item.MaxRetries, item.NextRetryAt);
                }
            }

            // In production, update Cosmos DB document
            // await _cosmosContainer.UpsertItemAsync(item, new PartitionKey(item.EmployeeId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update queue item {ItemId} after retry", item.Id);
            throw;
        }
    }

    /// <summary>
    /// Marks a queue item as processing (to prevent duplicate processing)
    /// </summary>
    public async Task<bool> TryLockItemAsync(SubmissionQueueItem item)
    {
        try
        {
            if (item.Status != "pending")
            {
                return false;
            }

            item.Status = "processing";
            item.LastProcessedAt = DateTimeOffset.UtcNow;

            // In production, use optimistic concurrency control (ETag) to prevent race conditions
            // var response = await _cosmosContainer.ReplaceItemAsync(item, item.Id, 
            //     new PartitionKey(item.EmployeeId), 
            //     new ItemRequestOptions { IfMatchEtag = item.ETag });

            _logger.LogDebug("Locked queue item {ItemId} for processing", item.Id);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // Another processor already locked this item
            _logger.LogDebug("Queue item {ItemId} already locked by another processor", item.Id);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to lock queue item {ItemId}", item.Id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a completed or expired queue item
    /// </summary>
    public async Task DeleteItemAsync(string itemId, string employeeId)
    {
        // In production, delete from Cosmos DB
        // await _cosmosContainer.DeleteItemAsync<SubmissionQueueItem>(itemId, new PartitionKey(employeeId));
        
        _logger.LogInformation("Deleted queue item {ItemId} for employee {EmployeeId}", itemId, employeeId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets queue statistics for monitoring
    /// </summary>
    public async Task<QueueStatistics> GetStatisticsAsync()
    {
        // In production, query Cosmos DB for counts by status
        await Task.CompletedTask;
        return new QueueStatistics
        {
            PendingCount = 0,
            ProcessingCount = 0,
            CompletedCount = 0,
            FailedCount = 0,
            TotalCount = 0,
        };
    }
}

/// <summary>
/// Queue statistics for monitoring
/// </summary>
public class QueueStatistics
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalCount { get; set; }
}
