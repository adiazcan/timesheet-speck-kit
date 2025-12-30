using HRAgent.Api.Events;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Services;

/// <summary>
/// Handles submission failure events by enqueueing items for retry
/// Implements the handler side of the event-driven pattern to break circular dependency
/// </summary>
public class SubmissionFailedEventHandler : ISubmissionFailedHandler
{
    private readonly SubmissionQueue _submissionQueue;
    private readonly ILogger<SubmissionFailedEventHandler> _logger;

    public SubmissionFailedEventHandler(
        SubmissionQueue submissionQueue,
        ILogger<SubmissionFailedEventHandler> logger)
    {
        _submissionQueue = submissionQueue;
        _logger = logger;
    }

    public async Task HandleAsync(SubmissionFailedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling submission failure for employee {EmployeeId}, action {Action}",
            @event.EmployeeId, @event.Action);

        await _submissionQueue.EnqueueAsync(
            @event.EmployeeId,
            @event.Action,
            @event.Timestamp,
            @event.ConversationThreadId,
            @event.MessageId,
            @event.UserMessage,
            @event.ErrorMessage,
            @event.StatusCode,
            @event.ContextData);

        _logger.LogInformation(
            "Successfully queued failed submission for retry: Employee {EmployeeId}, Action {Action}",
            @event.EmployeeId, @event.Action);
    }
}
