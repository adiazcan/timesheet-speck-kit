using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Factorial;

/// <summary>
/// Request payload for clocking in via Factorial HR API
/// </summary>
public class ClockInRequest
{
    /// <summary>
    /// Employee ID
    /// </summary>
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Clock-in timestamp (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Request payload for clocking out via Factorial HR API
/// </summary>
public class ClockOutRequest
{
    /// <summary>
    /// Employee ID
    /// </summary>
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Clock-out timestamp (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Query parameters for retrieving historical timesheet data
/// </summary>
public class TimesheetQuery
{
    /// <summary>
    /// Employee ID
    /// </summary>
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Start date (inclusive, YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("start_date")]
    public DateOnly StartDate { get; set; }
    
    /// <summary>
    /// End date (inclusive, YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("end_date")]
    public DateOnly EndDate { get; set; }
    
    /// <summary>
    /// Pagination: Page number (1-indexed)
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Pagination: Results per page
    /// </summary>
    [JsonPropertyName("page_size")]
    public int PageSize { get; set; } = 30;
}

/// <summary>
/// Response from Factorial HR API for timesheet operations
/// </summary>
public class TimesheetResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;
    
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
    
    [JsonPropertyName("clock_in")]
    public DateTimeOffset? ClockIn { get; set; }
    
    [JsonPropertyName("clock_out")]
    public DateTimeOffset? ClockOut { get; set; }
    
    [JsonPropertyName("total_hours")]
    public decimal? TotalHours { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Success indicator for timesheet operations
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    /// <summary>
    /// Timesheet entry ID returned after successful operation
    /// </summary>
    [JsonPropertyName("timesheet_id")]
    public string? TimesheetId { get; set; }
    
    /// <summary>
    /// Response message from the API
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response containing current timesheet status for an employee
/// </summary>
public class TimesheetStatusResponse
{
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;
    
    [JsonPropertyName("is_clocked_in")]
    public bool IsClockedIn { get; set; }
    
    [JsonPropertyName("clock_in_time")]
    public DateTimeOffset? ClockInTime { get; set; }
    
    [JsonPropertyName("current_hours")]
    public decimal CurrentHours { get; set; }
    
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
}
