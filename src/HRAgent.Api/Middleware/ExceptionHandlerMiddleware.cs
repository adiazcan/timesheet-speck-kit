using System.Net;
using System.Text.Json;

namespace HRAgent.Api.Middleware;

/// <summary>
/// Global exception handler middleware
/// Catches unhandled exceptions and returns consistent error responses
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
#pragma warning disable CA1031 // Do not catch general exception types - This is a global exception handler
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Check if this is an SSE stream (AG-UI protocol)
        var isSSE = context.Response.ContentType?.Contains("text/event-stream") == true ||
                    context.Request.Headers.Accept.ToString().Contains("text/event-stream");

        if (isSSE)
        {
            // For SSE, send error as an event instead of closing the stream
            await HandleSSEErrorAsync(context, exception);
            return;
        }

        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow,
        };

        switch (exception)
        {
            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = "You are not authorized to access this resource";
                break;

            case ArgumentException argEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "BadRequest";
                errorResponse.Message = argEx.Message;
                break;

            case InvalidOperationException invEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "InvalidOperation";
                errorResponse.Message = invEx.Message;
                break;

            case KeyNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.Error = "NotFound";
                errorResponse.Message = "The requested resource was not found";
                break;

            case TimeoutException:
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = "Timeout";
                errorResponse.Message = "The request timed out";
                break;

            case HttpRequestException httpEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                errorResponse.Error = "ExternalServiceError";
                errorResponse.Message = "Failed to communicate with external service";
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = httpEx.Message;
                }
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Error = "InternalServerError";
                errorResponse.Message = "An unexpected error occurred";
                break;
        }

        // Include stack trace in development
        if (_environment.IsDevelopment())
        {
            errorResponse.StackTrace = exception.StackTrace;
        }

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Handles exceptions for Server-Sent Events (SSE) streams
    /// Sends error as an AG-UI error event instead of breaking the stream
    /// </summary>
    private async Task HandleSSEErrorAsync(HttpContext context, Exception exception)
    {
        var errorType = exception switch
        {
            UnauthorizedAccessException => "unauthorized",
            ArgumentException => "validation_error",
            InvalidOperationException => "invalid_operation",
            KeyNotFoundException => "not_found",
            TimeoutException => "timeout",
            HttpRequestException => "external_service_error",
            _ => "internal_error"
        };

        var errorMessage = _environment.IsDevelopment() 
            ? exception.Message 
            : "An error occurred while processing your request";

        // Send AG-UI error event
        var errorEvent = new
        {
            type = "error",
            error_type = errorType,
            message = errorMessage,
            timestamp = DateTimeOffset.UtcNow,
            trace_id = context.TraceIdentifier
        };

        var eventData = JsonSerializer.Serialize(errorEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Write SSE event format
        await context.Response.WriteAsync($"event: error\n");
        await context.Response.WriteAsync($"data: {eventData}\n\n");
        await context.Response.Body.FlushAsync();
    }
}

/// <summary>
/// Standard error response format
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// Extension methods for registering exception handler middleware
/// </summary>
public static class ExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlerMiddleware>();
    }
}
