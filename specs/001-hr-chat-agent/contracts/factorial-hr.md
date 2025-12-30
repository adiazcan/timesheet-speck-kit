# Factorial HR API Integration Contract

**Feature**: HR Chat Agent for Timesheet Management  
**Version**: 1.0  
**Date**: 2025-12-30

## Overview

This document specifies the integration contract with Factorial HR API for timesheet management operations. Factorial HR is the external system of record for employee time tracking.

**Factorial HR API Documentation**: https://apidoc.factorialhr.com/

---

## Authentication

### API Key Authentication

**Method**: Bearer token authentication  
**Header**: `Authorization: Bearer <API_KEY>`

**Obtaining API Key**:
1. Log in to Factorial HR admin portal
2. Navigate to Settings > Integrations > API
3. Generate new API key with `timesheets:read` and `timesheets:write` scopes

**Environment Configuration**:
```bash
# Development (.env)
FACTORIAL_API_KEY=sk_test_1234567890abcdef
FACTORIAL_BASE_URL=https://api.factorialhr.com/v1

# Production (Azure Key Vault)
Secret Name: factorial-api-key
Secret Value: sk_prod_abcdef1234567890
```

**Security Requirements**:
- API keys MUST be stored in Azure Key Vault (production)
- API keys MUST be stored in user secrets (local development)
- API keys MUST be rotated every 90 days
- API keys MUST have minimum required scopes (principle of least privilege)

---

## Base Configuration

**Base URL**: `https://api.factorialhr.com/v1`  
**Content-Type**: `application/json`  
**Accept**: `application/json`  
**Rate Limit**: 100 requests per minute per API key  
**Timeout**: 10 seconds per request  
**Retry Strategy**: Exponential backoff (1s, 2s, 4s, max 3 retries)

---

## Endpoints

### 1. Clock In

**Endpoint**: `POST /timesheets/clock-in`

**Description**: Records employee clock-in timestamp for the current day.

**Request Headers**:
```
Authorization: Bearer {API_KEY}
Content-Type: application/json
```

**Request Body**:
```json
{
  "employee_id": "12345",
  "timestamp": "2024-01-01T08:00:00Z",
  "notes": "Starting work"
}
```

**Request Schema**:
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `employee_id` | string | Yes | Employee ID from Factorial HR |
| `timestamp` | string (ISO 8601) | Yes | Clock-in timestamp (UTC) |
| `notes` | string | No | Optional notes (max 500 chars) |

**Success Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "timesheet_id": "ts_abc123",
    "employee_id": "12345",
    "clock_in": "2024-01-01T08:00:00Z",
    "status": "active"
  }
}
```

**Error Responses**:

**400 Bad Request** - Invalid input
```json
{
  "success": false,
  "error": {
    "code": "INVALID_TIMESTAMP",
    "message": "Timestamp cannot be in the future",
    "details": {
      "field": "timestamp",
      "provided": "2025-01-01T08:00:00Z"
    }
  }
}
```

**409 Conflict** - Already clocked in
```json
{
  "success": false,
  "error": {
    "code": "ALREADY_CLOCKED_IN",
    "message": "Employee is already clocked in",
    "details": {
      "clock_in_time": "2024-01-01T07:30:00Z",
      "timesheet_id": "ts_xyz789"
    }
  }
}
```

**503 Service Unavailable** - API temporarily down
```json
{
  "success": false,
  "error": {
    "code": "SERVICE_UNAVAILABLE",
    "message": "Timesheet service is temporarily unavailable",
    "details": {
      "retry_after": 60
    }
  }
}
```

---

### 2. Clock Out

**Endpoint**: `POST /timesheets/clock-out`

**Description**: Records employee clock-out timestamp for the current day.

**Request Headers**:
```
Authorization: Bearer {API_KEY}
Content-Type: application/json
```

**Request Body**:
```json
{
  "employee_id": "12345",
  "timestamp": "2024-01-01T17:00:00Z",
  "notes": "End of day"
}
```

**Request Schema**:
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `employee_id` | string | Yes | Employee ID from Factorial HR |
| `timestamp` | string (ISO 8601) | Yes | Clock-out timestamp (UTC) |
| `notes` | string | No | Optional notes (max 500 chars) |

**Success Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "timesheet_id": "ts_abc123",
    "employee_id": "12345",
    "clock_in": "2024-01-01T08:00:00Z",
    "clock_out": "2024-01-01T17:00:00Z",
    "total_hours": 9.0,
    "status": "completed"
  }
}
```

**Error Responses**:

**409 Conflict** - Not clocked in
```json
{
  "success": false,
  "error": {
    "code": "NOT_CLOCKED_IN",
    "message": "Employee is not currently clocked in",
    "details": {
      "last_clock_out": "2023-12-31T17:00:00Z"
    }
  }
}
```

**422 Unprocessable Entity** - Clock-out before clock-in
```json
{
  "success": false,
  "error": {
    "code": "INVALID_CLOCK_OUT",
    "message": "Clock-out time must be after clock-in time",
    "details": {
      "clock_in": "2024-01-01T08:00:00Z",
      "clock_out": "2024-01-01T07:00:00Z"
    }
  }
}
```

---

### 3. Get Current Timesheet Status

**Endpoint**: `GET /timesheets/current?employee_id={employeeId}`

**Description**: Retrieves current timesheet status for an employee (today's data).

**Request Headers**:
```
Authorization: Bearer {API_KEY}
Accept: application/json
```

**Query Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `employee_id` | string | Yes | Employee ID |

**Success Response (200 OK)** - Clocked in:
```json
{
  "success": true,
  "data": {
    "timesheet_id": "ts_abc123",
    "employee_id": "12345",
    "date": "2024-01-01",
    "clock_in": "2024-01-01T08:00:00Z",
    "clock_out": null,
    "status": "active",
    "current_duration_hours": 2.5
  }
}
```

**Success Response (200 OK)** - Clocked out:
```json
{
  "success": true,
  "data": {
    "timesheet_id": "ts_abc123",
    "employee_id": "12345",
    "date": "2024-01-01",
    "clock_in": "2024-01-01T08:00:00Z",
    "clock_out": "2024-01-01T17:00:00Z",
    "status": "completed",
    "total_hours": 9.0
  }
}
```

**Success Response (200 OK)** - No timesheet today:
```json
{
  "success": true,
  "data": null
}
```

**Error Responses**:

**404 Not Found** - Employee not found
```json
{
  "success": false,
  "error": {
    "code": "EMPLOYEE_NOT_FOUND",
    "message": "Employee with ID 12345 not found"
  }
}
```

---

### 4. Get Historical Timesheets

**Endpoint**: `GET /timesheets/history`

**Description**: Retrieves historical timesheet entries for an employee within a date range.

**Request Headers**:
```
Authorization: Bearer {API_KEY}
Accept: application/json
```

**Query Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `employee_id` | string | Yes | Employee ID |
| `start_date` | string (YYYY-MM-DD) | Yes | Start date (inclusive) |
| `end_date` | string (YYYY-MM-DD) | Yes | End date (inclusive) |
| `page` | integer | No | Page number (default: 1) |
| `page_size` | integer | No | Results per page (default: 30, max: 100) |

**Example Request**:
```
GET /timesheets/history?employee_id=12345&start_date=2024-01-01&end_date=2024-01-31&page=1&page_size=30
```

**Success Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "timesheets": [
      {
        "timesheet_id": "ts_001",
        "employee_id": "12345",
        "date": "2024-01-15",
        "clock_in": "2024-01-15T08:00:00Z",
        "clock_out": "2024-01-15T17:00:00Z",
        "total_hours": 9.0,
        "status": "approved"
      },
      {
        "timesheet_id": "ts_002",
        "employee_id": "12345",
        "date": "2024-01-14",
        "clock_in": "2024-01-14T08:30:00Z",
        "clock_out": "2024-01-14T17:30:00Z",
        "total_hours": 9.0,
        "status": "approved"
      }
    ],
    "pagination": {
      "page": 1,
      "page_size": 30,
      "total_count": 45,
      "total_pages": 2
    },
    "summary": {
      "total_hours": 180.0,
      "total_days": 20,
      "average_hours_per_day": 9.0
    }
  }
}
```

**Error Responses**:

**400 Bad Request** - Invalid date range
```json
{
  "success": false,
  "error": {
    "code": "INVALID_DATE_RANGE",
    "message": "End date must be after start date",
    "details": {
      "start_date": "2024-01-31",
      "end_date": "2024-01-01"
    }
  }
}
```

**400 Bad Request** - Date range too large
```json
{
  "success": false,
  "error": {
    "code": "DATE_RANGE_TOO_LARGE",
    "message": "Date range cannot exceed 90 days",
    "details": {
      "requested_days": 120,
      "max_days": 90
    }
  }
}
```

---

## Error Codes Reference

| Code | HTTP Status | Description | Retry? |
|------|-------------|-------------|--------|
| `INVALID_TIMESTAMP` | 400 | Timestamp format invalid or in future | No |
| `INVALID_DATE_RANGE` | 400 | Start date after end date | No |
| `DATE_RANGE_TOO_LARGE` | 400 | Date range exceeds 90 days | No |
| `EMPLOYEE_NOT_FOUND` | 404 | Employee ID not found in system | No |
| `ALREADY_CLOCKED_IN` | 409 | Employee already has active timesheet | No |
| `NOT_CLOCKED_IN` | 409 | Employee not currently clocked in | No |
| `INVALID_CLOCK_OUT` | 422 | Clock-out before clock-in | No |
| `UNAUTHORIZED` | 401 | Invalid or expired API key | No |
| `FORBIDDEN` | 403 | Insufficient API key permissions | No |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests | Yes (after retry_after) |
| `SERVICE_UNAVAILABLE` | 503 | API temporarily down | Yes (exponential backoff) |
| `INTERNAL_ERROR` | 500 | Factorial HR internal error | Yes (exponential backoff) |

---

## Integration Implementation (.NET)

### Service Interface

```csharp
public interface IFactorialHRService
{
    Task<ClockInResult> ClockInAsync(string employeeId, DateTimeOffset timestamp, string? notes = null, CancellationToken cancellationToken = default);
    Task<ClockOutResult> ClockOutAsync(string employeeId, DateTimeOffset timestamp, string? notes = null, CancellationToken cancellationToken = default);
    Task<TimesheetStatus?> GetCurrentStatusAsync(string employeeId, CancellationToken cancellationToken = default);
    Task<TimesheetHistoryResult> GetHistoryAsync(string employeeId, DateOnly startDate, DateOnly endDate, int page = 1, int pageSize = 30, CancellationToken cancellationToken = default);
}
```

### Service Implementation

```csharp
public class FactorialHRService : IFactorialHRService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FactorialHRService> _logger;
    
    public FactorialHRService(IHttpClientFactory httpClientFactory, ILogger<FactorialHRService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("factorial");
        _logger = logger;
    }
    
    public async Task<ClockInResult> ClockInAsync(
        string employeeId, 
        DateTimeOffset timestamp, 
        string? notes = null, 
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            employee_id = employeeId,
            timestamp = timestamp.ToString("O"),
            notes
        };
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/timesheets/clock-in", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FactorialResponse<ClockInData>>(cancellationToken);
                return new ClockInResult
                {
                    Success = true,
                    TimesheetId = result.Data.TimesheetId,
                    ClockInTime = result.Data.ClockIn
                };
            }
            
            var error = await response.Content.ReadFromJsonAsync<FactorialErrorResponse>(cancellationToken);
            return new ClockInResult
            {
                Success = false,
                ErrorCode = error.Error.Code,
                ErrorMessage = error.Error.Message
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling Factorial HR API");
            return new ClockInResult
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                ErrorMessage = "Failed to connect to Factorial HR"
            };
        }
    }
    
    // Implement other methods similarly...
}
```

### HTTP Client Configuration (Program.cs)

```csharp
builder.Services.AddHttpClient("factorial", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FactorialHR:BaseUrl"]);
    client.DefaultRequestHeaders.Add("Authorization", 
        $"Bearer {builder.Configuration["FactorialHR:ApiKey"]}");
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddStandardResilienceHandler(options =>
{
    // Retry configuration
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    
    // Circuit breaker
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    
    // Timeout
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## Contract Testing Strategy

### Mock Server (Development)

Use WireMock.Net for local development without Factorial HR API access:

```csharp
// tests/HRAgent.Api.Tests/Mocks/FactorialHRMock.cs
public class FactorialHRMock
{
    private readonly WireMockServer _server;
    
    public FactorialHRMock()
    {
        _server = WireMockServer.Start();
        SetupClockInEndpoint();
        SetupClockOutEndpoint();
        SetupCurrentStatusEndpoint();
    }
    
    private void SetupClockInEndpoint()
    {
        _server
            .Given(Request.Create()
                .WithPath("/timesheets/clock-in")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new
                {
                    success = true,
                    data = new
                    {
                        timesheet_id = "ts_mock_001",
                        employee_id = "12345",
                        clock_in = DateTimeOffset.UtcNow.ToString("O"),
                        status = "active"
                    }
                }));
    }
    
    public string BaseUrl => _server.Urls[0];
}
```

### Contract Tests

```csharp
public class FactorialHRContractTests
{
    [Fact]
    public async Task ClockIn_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var mock = new FactorialHRMock();
        var service = CreateService(mock.BaseUrl);
        
        // Act
        var result = await service.ClockInAsync("12345", DateTimeOffset.UtcNow);
        
        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimesheetId);
        Assert.NotNull(result.ClockInTime);
    }
    
    [Fact]
    public async Task ClockIn_AlreadyClockedIn_ReturnsConflict()
    {
        // Test error scenarios
    }
}
```

---

## Rate Limiting Handling

### Client-Side Rate Limiter

```csharp
public class RateLimitedFactorialHRService : IFactorialHRService
{
    private readonly IFactorialHRService _innerService;
    private readonly RateLimiter _rateLimiter;
    
    public RateLimitedFactorialHRService(IFactorialHRService innerService)
    {
        _innerService = innerService;
        _rateLimiter = new TokenBucketRateLimiter(new()
        {
            TokenLimit = 100,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 100,
            QueueLimit = 10
        });
    }
    
    public async Task<ClockInResult> ClockInAsync(
        string employeeId, 
        DateTimeOffset timestamp, 
        string? notes = null, 
        CancellationToken cancellationToken = default)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        
        if (!lease.IsAcquired)
        {
            return new ClockInResult
            {
                Success = false,
                ErrorCode = "RATE_LIMIT_EXCEEDED",
                ErrorMessage = "Too many requests to Factorial HR. Please try again later."
            };
        }
        
        return await _innerService.ClockInAsync(employeeId, timestamp, notes, cancellationToken);
    }
}
```

---

## Monitoring and Observability

### Application Insights Tracking

```csharp
public class TelemetryFactorialHRService : IFactorialHRService
{
    private readonly IFactorialHRService _innerService;
    private readonly TelemetryClient _telemetry;
    
    public async Task<ClockInResult> ClockInAsync(...)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await _innerService.ClockInAsync(...);
            
            _telemetry.TrackDependency(
                dependencyTypeName: "HTTP",
                dependencyName: "FactorialHR ClockIn",
                data: $"POST /timesheets/clock-in",
                startTime: DateTimeOffset.UtcNow.Subtract(stopwatch.Elapsed),
                duration: stopwatch.Elapsed,
                success: result.Success
            );
            
            if (!result.Success)
            {
                _telemetry.TrackEvent("FactorialHR_ClockIn_Failed", new Dictionary<string, string>
                {
                    ["ErrorCode"] = result.ErrorCode,
                    ["EmployeeId"] = employeeId
                });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex);
            throw;
        }
    }
}
```

---

## Security Considerations

### API Key Rotation

- API keys expire every 90 days
- Use Azure Key Vault key rotation policies
- Implement fallback to secondary API key during rotation
- Monitor for `401 Unauthorized` responses indicating expired key

### Data Privacy

- Never log full employee IDs in plain text (use hashing)
- Redact sensitive timesheet notes from logs
- Sanitize error responses before sending to frontend

### Network Security

- Use TLS 1.2+ for all API requests
- Validate SSL certificates
- Use Azure Private Endpoints for production (if available)

---

## Conclusion

This contract document defines:
- ✅ 4 core Factorial HR API endpoints
- ✅ Complete request/response schemas
- ✅ Error handling and retry strategies
- ✅ .NET service implementation patterns
- ✅ Contract testing approach
- ✅ Rate limiting and monitoring

**Contract Status**: ✅ Complete  
**Ready for Implementation**: ✅ Yes

**Next Steps**:
1. Obtain Factorial HR API key from admin
2. Implement service layer in .NET
3. Write contract tests
4. Configure rate limiting and resilience policies
