using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HRAgent.Api.Services;

/// <summary>
/// Service for audit logging to Azure Blob Storage
/// Wraps IAuditLogger with business logic
/// </summary>
public class AuditLogger
{
    private readonly IAuditLogger _logger;
    private readonly ILogger<AuditLogger> _appLogger;

    public AuditLogger(IAuditLogger logger, ILogger<AuditLogger> appLogger)
    {
        _logger = logger;
        _appLogger = appLogger;
    }

    /// <summary>
    /// Logs a timesheet action (clock-in, clock-out, query)
    /// </summary>
    public async Task LogActionAsync(
        string employeeId,
        string action,
        string conversationThreadId,
        string messageId,
        object? requestData = null,
        object? responseData = null,
        int? statusCode = null,
        string? sourceIp = null,
        string? userAgent = null,
        long? durationMs = null,
        AuditError? error = null)
    {
        var entry = new AuditLogEntry
        {
            EmployeeId = employeeId,
            Action = action,
            ConversationThreadId = conversationThreadId,
            MessageId = messageId,
            RequestData = requestData,
            ResponseData = responseData,
            StatusCode = statusCode,
            SourceIp = sourceIp ?? string.Empty,
            UserAgent = userAgent ?? string.Empty,
            DurationMs = durationMs,
            Error = error,
        };

        try
        {
            await _logger.LogAsync(entry);
            _appLogger.LogInformation(
                "Audit log created: Employee={EmployeeId}, Action={Action}, Status={StatusCode}", 
                employeeId, action, statusCode);
        }
#pragma warning disable CA1031 // Do not catch general exception types - Audit logging failure should not break main flow
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _appLogger.LogError(ex, 
                "Failed to write audit log for employee {EmployeeId}, action {Action}", 
                employeeId, action);
            // Don't throw - audit logging failure should not break the main flow
        }
    }

    /// <summary>
    /// Logs a clock-in action
    /// </summary>
    public async Task LogClockInAsync(
        string employeeId,
        string conversationThreadId,
        string messageId,
        DateTimeOffset timestamp,
        int statusCode,
        string? sourceIp = null,
        string? userAgent = null,
        long? durationMs = null,
        AuditError? error = null)
    {
        await LogActionAsync(
            employeeId,
            "clock-in",
            conversationThreadId,
            messageId,
            requestData: new { timestamp },
            responseData: new { timestamp, success = statusCode >= 200 && statusCode < 300 },
            statusCode,
            sourceIp,
            userAgent,
            durationMs,
            error);
    }

    /// <summary>
    /// Logs a clock-out action
    /// </summary>
    public async Task LogClockOutAsync(
        string employeeId,
        string conversationThreadId,
        string messageId,
        DateTimeOffset timestamp,
        decimal? totalHours,
        int statusCode,
        string? sourceIp = null,
        string? userAgent = null,
        long? durationMs = null,
        AuditError? error = null)
    {
        await LogActionAsync(
            employeeId,
            "clock-out",
            conversationThreadId,
            messageId,
            requestData: new { timestamp },
            responseData: new { timestamp, totalHours, success = statusCode >= 200 && statusCode < 300 },
            statusCode,
            sourceIp,
            userAgent,
            durationMs,
            error);
    }

    /// <summary>
    /// Logs a status query
    /// </summary>
    public async Task LogStatusQueryAsync(
        string employeeId,
        string conversationThreadId,
        string messageId,
        bool isClockedIn,
        int statusCode,
        string? sourceIp = null,
        string? userAgent = null,
        long? durationMs = null,
        AuditError? error = null)
    {
        await LogActionAsync(
            employeeId,
            "query-status",
            conversationThreadId,
            messageId,
            requestData: new { query = "current status" },
            responseData: new { isClockedIn, success = statusCode >= 200 && statusCode < 300 },
            statusCode,
            sourceIp,
            userAgent,
            durationMs,
            error);
    }

    /// <summary>
    /// Logs a historical timesheet query
    /// </summary>
    public async Task LogHistoricalQueryAsync(
        string employeeId,
        string conversationThreadId,
        string messageId,
        DateOnly startDate,
        DateOnly endDate,
        int resultCount,
        int statusCode,
        string? sourceIp = null,
        string? userAgent = null,
        long? durationMs = null,
        AuditError? error = null)
    {
        await LogActionAsync(
            employeeId,
            "query-historical",
            conversationThreadId,
            messageId,
            requestData: new { startDate, endDate },
            responseData: new { startDate, endDate, resultCount, success = statusCode >= 200 && statusCode < 300 },
            statusCode,
            sourceIp,
            userAgent,
            durationMs,
            error);
    }

    /// <summary>
    /// Gets audit logs for an employee for a specific date (for GDPR compliance)
    /// </summary>
    public async Task<List<AuditLogEntry>> GetLogsAsync(string employeeId, DateOnly date)
    {
        try
        {
            var result = await _logger.GetLogsAsync(employeeId, date);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to retrieve audit logs for employee {EmployeeId} on {Date}", employeeId, date);
            throw;
        }
    }

    /// <summary>
    /// Gets audit logs for an employee for a date range (for GDPR compliance)
    /// </summary>
    public async Task<List<AuditLogEntry>> GetLogsRangeAsync(string employeeId, DateOnly startDate, DateOnly endDate)
    {
        try
        {
            var allLogs = new List<AuditLogEntry>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var logs = await _logger.GetLogsAsync(employeeId, currentDate);
                allLogs.AddRange(logs);
                currentDate = currentDate.AddDays(1);
            }

            return allLogs;
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to retrieve audit logs for employee {EmployeeId} from {StartDate} to {EndDate}", 
                employeeId, startDate, endDate);
            throw;
        }
    }
}
