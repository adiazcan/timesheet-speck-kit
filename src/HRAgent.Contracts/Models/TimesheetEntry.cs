using System.Text.Json.Serialization;

namespace HRAgent.Contracts.Models;

/// <summary>
/// Domain model representing a timesheet entry from Factorial HR
/// </summary>
public class TimesheetEntry
{
    /// <summary>
    /// Factorial HR timesheet entry ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Employee ID (Factorial HR)
    /// </summary>
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Date of the timesheet entry (YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Clock-in timestamp
    /// </summary>
    [JsonPropertyName("clockIn")]
    public DateTimeOffset? ClockIn { get; set; }
    
    /// <summary>
    /// Clock-out timestamp (null if still clocked in)
    /// </summary>
    [JsonPropertyName("clockOut")]
    public DateTimeOffset? ClockOut { get; set; }
    
    /// <summary>
    /// Total hours worked (calculated from clock-in/out)
    /// </summary>
    [JsonPropertyName("totalHours")]
    public decimal? TotalHours { get; set; }
    
    /// <summary>
    /// Timesheet entry status (draft, submitted, approved)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "draft";
    
    /// <summary>
    /// Notes or comments (optional)
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Calculates total hours between clock-in and clock-out
    /// </summary>
    public decimal CalculateTotalHours()
    {
        if (ClockIn.HasValue && ClockOut.HasValue)
        {
            var duration = ClockOut.Value - ClockIn.Value;
            return (decimal)duration.TotalHours;
        }
        return 0;
    }
    
    /// <summary>
    /// Checks if this is an overnight shift (clock-out after midnight)
    /// </summary>
    public bool IsOvernightShift()
    {
        if (ClockIn.HasValue && ClockOut.HasValue)
        {
            return ClockOut.Value.Date > ClockIn.Value.Date;
        }
        return false;
    }
}
