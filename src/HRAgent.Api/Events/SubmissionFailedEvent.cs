namespace HRAgent.Api.Events;

/// <summary>
/// Event raised when a Factorial HR submission fails and needs to be queued for retry
/// </summary>
public class SubmissionFailedEvent
{
    public required string EmployeeId { get; init; }
    public required string Action { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ConversationThreadId { get; init; }
    public required string MessageId { get; init; }
    public string? UserMessage { get; init; }
    public required string ErrorMessage { get; init; }
    public int StatusCode { get; init; }
    public Dictionary<string, object>? ContextData { get; init; }
}

/// <summary>
/// Interface for handling submission failure events
/// </summary>
public interface ISubmissionFailedHandler
{
    Task HandleAsync(SubmissionFailedEvent @event, CancellationToken cancellationToken = default);
}
