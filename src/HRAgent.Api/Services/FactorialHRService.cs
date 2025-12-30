using HRAgent.Contracts.Factorial;
using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace HRAgent.Api.Services;

/// <summary>
/// Options for configuring FactorialHRService behavior
/// </summary>
public class FactorialHRServiceOptions
{
    /// <summary>
    /// Whether to enqueue failed requests for retry instead of throwing
    /// </summary>
    public bool EnqueueFailedRequests { get; set; } = true;

    /// <summary>
    /// Whether to throw exceptions immediately (for testing)
    /// </summary>
    public bool ThrowOnFailure { get; set; } = false;
}

/// <summary>
/// Service for interacting with Factorial HR API
/// Handles clock-in/out operations and timesheet queries
/// </summary>
public class FactorialHRService
{
    private readonly HttpClient _httpClient;
    private readonly ISecretsManager _secretsManager;
    private readonly ILogger<FactorialHRService> _logger;
    private readonly FactorialHRServiceOptions _options;
    private SubmissionQueue? _submissionQueue;
    private string? _apiKey;

    public FactorialHRService(
        HttpClient httpClient,
        ISecretsManager secretsManager,
        ILogger<FactorialHRService> logger,
        FactorialHRServiceOptions? options = null)
    {
        _httpClient = httpClient;
        _secretsManager = secretsManager;
        _logger = logger;
        _options = options ?? new FactorialHRServiceOptions();
    }

    /// <summary>
    /// Sets the submission queue for enqueuing failed requests
    /// (Injected separately to avoid circular dependency)
    /// </summary>
    public void SetSubmissionQueue(SubmissionQueue submissionQueue)
    {
        _submissionQueue = submissionQueue;
    }

    /// <summary>
    /// Retrieves API key from Azure Key Vault (cached for performance)
    /// </summary>
    private async Task<string> GetApiKeyAsync()
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            return _apiKey;
        }

        _apiKey = await _secretsManager.GetSecretAsync("factorial-hr-api-key");
        return _apiKey;
    }

    /// <summary>
    /// Ensures Authorization header is set with API key
    /// </summary>
    private async Task EnsureAuthenticationAsync()
    {
        var apiKey = await GetApiKeyAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Submits a clock-in request to Factorial HR
    /// </summary>
    public async Task<TimesheetResponse> ClockInAsync(ClockInRequest request)
    {
        await EnsureAuthenticationAsync();

        try
        {
            _logger.LogInformation("Submitting clock-in for employee {EmployeeId} at {Timestamp}", 
                request.EmployeeId, request.Timestamp);

            var response = await _httpClient.PostAsJsonAsync("/api/v1/timesheet/clock-in", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>();
            if (result == null)
            {
                throw new InvalidOperationException("Factorial HR returned null response");
            }

            _logger.LogInformation("Clock-in successful for employee {EmployeeId}", request.EmployeeId);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during clock-in for employee {EmployeeId}", request.EmployeeId);
            
            // Enqueue for retry if configured
            if (_options.EnqueueFailedRequests && !_options.ThrowOnFailure && _submissionQueue != null)
            {
                await _submissionQueue.EnqueueAsync(
                    request.EmployeeId,
                    "clock-in",
                    request.Timestamp,
                    conversationThreadId: string.Empty, // Will be set by caller
                    messageId: string.Empty, // Will be set by caller
                    errorMessage: ex.Message,
                    statusCode: 502);
                
                throw new FactorialHRException("Request queued for retry", ex);
            }
            
            throw new FactorialHRException("Failed to submit clock-in to Factorial HR", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error during clock-in for employee {EmployeeId}", request.EmployeeId);
            throw new FactorialHRException("Invalid response from Factorial HR", ex);
        }
    }

    /// <summary>
    /// Submits a clock-out request to Factorial HR
    /// </summary>
    public async Task<TimesheetResponse> ClockOutAsync(ClockOutRequest request)
    {
        await EnsureAuthenticationAsync();

        try
        {
            _logger.LogInformation("Submitting clock-out for employee {EmployeeId} at {Timestamp}", 
                request.EmployeeId, request.Timestamp);

            var response = await _httpClient.PostAsJsonAsync("/api/v1/timesheet/clock-out", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>();
            if (result == null)
            {
                throw new InvalidOperationException("Factorial HR returned null response");
            }

            _logger.LogInformation("Clock-out successful for employee {EmployeeId}, Total hours: {TotalHours}", 
                request.EmployeeId, result.TotalHours);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during clock-out for employee {EmployeeId}", request.EmployeeId);
            
            // Enqueue for retry if configured
            if (_options.EnqueueFailedRequests && !_options.ThrowOnFailure && _submissionQueue != null)
            {
                await _submissionQueue.EnqueueAsync(
                    request.EmployeeId,
                    "clock-out",
                    request.Timestamp,
                    conversationThreadId: string.Empty, // Will be set by caller
                    messageId: string.Empty, // Will be set by caller
                    errorMessage: ex.Message,
                    statusCode: 502);
                
                throw new FactorialHRException("Request queued for retry", ex);
            }
            
            throw new FactorialHRException("Failed to submit clock-out to Factorial HR", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error during clock-out for employee {EmployeeId}", request.EmployeeId);
            throw new FactorialHRException("Invalid response from Factorial HR", ex);
        }
    }

    /// <summary>
    /// Gets current status (clocked in/out) for an employee
    /// </summary>
    public async Task<TimesheetResponse?> GetCurrentStatusAsync(string employeeId)
    {
        await EnsureAuthenticationAsync();

        try
        {
            _logger.LogInformation("Querying current status for employee {EmployeeId}", employeeId);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var response = await _httpClient.GetAsync($"/api/v1/timesheet/{employeeId}/current?date={today:yyyy-MM-dd}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("No timesheet entry found for employee {EmployeeId} today", employeeId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>();
            _logger.LogInformation("Current status retrieved for employee {EmployeeId}", employeeId);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during status query for employee {EmployeeId}", employeeId);
            throw new FactorialHRException("Failed to query current status from Factorial HR", ex);
        }
    }

    /// <summary>
    /// Queries historical timesheet entries
    /// </summary>
    public async Task<List<TimesheetResponse>> GetHistoryAsync(TimesheetQuery query)
    {
        await EnsureAuthenticationAsync();

        try
        {
            _logger.LogInformation("Querying historical timesheets for employee {EmployeeId} from {StartDate} to {EndDate}", 
                query.EmployeeId, query.StartDate, query.EndDate);

            var queryString = $"start_date={query.StartDate:yyyy-MM-dd}&end_date={query.EndDate:yyyy-MM-dd}&page={query.Page}&page_size={query.PageSize}";
            var response = await _httpClient.GetAsync($"/api/v1/timesheet/{query.EmployeeId}/history?{queryString}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<TimesheetResponse>>();
            if (result == null)
            {
                return new List<TimesheetResponse>();
            }

            _logger.LogInformation("Retrieved {Count} timesheet entries for employee {EmployeeId}", 
                result.Count, query.EmployeeId);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during historical query for employee {EmployeeId}", query.EmployeeId);
            throw new FactorialHRException("Failed to query historical timesheets from Factorial HR", ex);
        }
    }

    /// <summary>
    /// Converts Factorial HR response to domain model
    /// </summary>
    public TimesheetEntry ToDomainModel(TimesheetResponse response)
    {
        return new TimesheetEntry
        {
            Id = response.Id,
            EmployeeId = response.EmployeeId,
            Date = response.Date,
            ClockIn = response.ClockIn,
            ClockOut = response.ClockOut,
            TotalHours = response.TotalHours,
            Status = response.Status,
            Notes = response.Notes,
        };
    }
}

/// <summary>
/// Custom exception for Factorial HR API errors
/// </summary>
public class FactorialHRException : Exception
{
    public FactorialHRException(string message) : base(message) { }
    public FactorialHRException(string message, Exception innerException) : base(message, innerException) { }
}
