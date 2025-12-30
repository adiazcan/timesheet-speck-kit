using HRAgent.Api.Services;
using HRAgent.Contracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRAgent.Api.Controllers;

/// <summary>
/// API endpoints for conversation management and GDPR compliance
/// </summary>
[ApiController]
[Route("api/conversation")]
[Authorize]
public class ConversationController : ControllerBase
{
    private readonly ConversationDeletionService _deletionService;
    private readonly EmailNotificationService _emailService;
    private readonly DeletionAuditLogger _deletionAuditLogger;
    private readonly ILogger<ConversationController> _logger;

    public ConversationController(
        ConversationDeletionService deletionService,
        EmailNotificationService emailService,
        DeletionAuditLogger deletionAuditLogger,
        ILogger<ConversationController> logger)
    {
        _deletionService = deletionService;
        _emailService = emailService;
        _deletionAuditLogger = deletionAuditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Submit a request to delete all conversation data (GDPR Right to be Forgotten)
    /// FR-014b: Employee self-service mechanism to request conversation data deletion
    /// FR-014c: Process deletion requests within 30 days
    /// </summary>
    /// <returns>Deletion request with scheduled deletion date</returns>
    [HttpPost("deletion-request")]
    [ProducesResponseType(typeof(ConversationDeletionRequest), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConversationDeletionRequest>> SubmitDeletionRequest(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract employee information from JWT claims
            var employeeId = User.FindFirstValue("employeeId") 
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Employee ID not found in token");

            var employeeEmail = User.FindFirstValue(ClaimTypes.Email) 
                ?? throw new UnauthorizedAccessException("Email not found in token");

            var employeeName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;

            var requestOriginIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            _logger.LogInformation("Employee {EmployeeId} submitting deletion request", employeeId);

            // Submit deletion request (30-day processing window)
            var deletionRequest = await _deletionService.SubmitDeletionRequestAsync(
                employeeId,
                employeeEmail,
                employeeName,
                requestOriginIp,
                cancellationToken);

            // Send confirmation email
            await _emailService.SendDeletionRequestConfirmationAsync(
                employeeEmail,
                employeeName ?? "User",
                deletionRequest.Id,
                deletionRequest.ScheduledDeletionDate,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetDeletionRequestStatus),
                new { requestId = deletionRequest.Id },
                deletionRequest);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deletion request conflict");
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Deletion request already pending",
                Detail = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized deletion request attempt");
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get status of a deletion request
    /// </summary>
    [HttpGet("deletion-request/{requestId}")]
    [ProducesResponseType(typeof(ConversationDeletionRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDeletionRequest>> GetDeletionRequestStatus(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var employeeId = User.FindFirstValue("employeeId") 
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Employee ID not found in token");

        var request = await _deletionService.GetDeletionRequestAsync(requestId, employeeId, cancellationToken);
        if (request == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Deletion request not found",
                Detail = $"No deletion request found with ID: {requestId}"
            });
        }

        return Ok(request);
    }

    /// <summary>
    /// Get pending deletion request for current employee (if any)
    /// </summary>
    [HttpGet("deletion-request")]
    [ProducesResponseType(typeof(ConversationDeletionRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ConversationDeletionRequest>> GetPendingDeletionRequest(
        CancellationToken cancellationToken = default)
    {
        var employeeId = User.FindFirstValue("employeeId") 
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Employee ID not found in token");

        var request = await _deletionService.GetPendingDeletionRequestAsync(employeeId, cancellationToken);
        if (request == null)
        {
            return NoContent();
        }

        return Ok(request);
    }

    /// <summary>
    /// Cancel a pending deletion request
    /// </summary>
    [HttpDelete("deletion-request/{requestId}")]
    [ProducesResponseType(typeof(ConversationDeletionRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDeletionRequest>> CancelDeletionRequest(
        string requestId,
        [FromBody] CancellationRequestDto cancellationRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var employeeId = User.FindFirstValue("employeeId") 
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Employee ID not found in token");

            var employeeEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var employeeName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;

            _logger.LogInformation("Employee {EmployeeId} cancelling deletion request {RequestId}", 
                employeeId, requestId);

            var cancelledRequest = await _deletionService.CancelDeletionRequestAsync(
                requestId,
                employeeId,
                cancellationRequest.Reason ?? "Cancelled by user",
                cancellationToken);

            // Send cancellation notification email
            await _emailService.SendDeletionCancelledNotificationAsync(
                employeeEmail,
                employeeName ?? "User",
                requestId,
                cancellationToken);

            return Ok(cancelledRequest);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel deletion request {RequestId}", requestId);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot cancel deletion request",
                Detail = ex.Message
            });
        }
    }
}

/// <summary>
/// DTO for cancellation request
/// </summary>
public record CancellationRequestDto(string? Reason);
