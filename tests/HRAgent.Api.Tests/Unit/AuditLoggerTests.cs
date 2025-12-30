using FluentAssertions;
using HRAgent.Api.Services;
using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;

namespace HRAgent.Api.Tests.Unit;

/// <summary>
/// Unit tests for AuditLogger
/// Tests audit log entry creation and error handling
/// </summary>
public class AuditLoggerTests
{
    private readonly Mock<IAuditLogger> _loggerMock;
    private readonly Mock<ILogger<AuditLogger>> _appLoggerMock;

    public AuditLoggerTests()
    {
        _loggerMock = new Mock<IAuditLogger>();
        _appLoggerMock = new Mock<ILogger<AuditLogger>>();
    }

    [Fact]
    public async Task LogActionAsync_ValidData_CreatesAuditEntry()
    {
        // Arrange
        AuditLogEntry? capturedEntry = null;
        _loggerMock.Setup(x => x.LogAsync(It.IsAny<AuditLogEntry>()))
            .Callback<AuditLogEntry>(entry => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var logger = new AuditLogger(_loggerMock.Object, _appLoggerMock.Object);

        // Act
        await logger.LogActionAsync(
            employeeId: "emp-001",
            action: "clock-in",
            conversationThreadId: "thread-123",
            messageId: "msg-456",
            requestData: new { timestamp = "2025-12-30T10:00:00Z" },
            responseData: new { success = true, timesheetId = "ts-001" },
            statusCode: 200,
            sourceIp: "192.168.1.1",
            userAgent: "Mozilla/5.0",
            durationMs: 250
        );

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.EmployeeId.Should().Be("emp-001");
        capturedEntry.Action.Should().Be("clock-in");
        capturedEntry.StatusCode.Should().Be(200);
        capturedEntry.DurationMs.Should().Be(250);
        capturedEntry.SourceIp.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task LogClockInAsync_CreatesSpecificAuditEntry()
    {
        // Arrange
        AuditLogEntry? capturedEntry = null;
        _loggerMock.Setup(x => x.LogAsync(It.IsAny<AuditLogEntry>()))
            .Callback<AuditLogEntry>(entry => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var logger = new AuditLogger(_loggerMock.Object, _appLoggerMock.Object);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await logger.LogClockInAsync(
            employeeId: "emp-001",
            conversationThreadId: "thread-123",
            messageId: "msg-456",
            timestamp: timestamp,
            statusCode: 200
        );

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Action.Should().Be("clock-in");
        capturedEntry.EmployeeId.Should().Be("emp-001");
        capturedEntry.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task LogClockOutAsync_WithTotalHours_CreatesAuditEntry()
    {
        // Arrange
        AuditLogEntry? capturedEntry = null;
        _loggerMock.Setup(x => x.LogAsync(It.IsAny<AuditLogEntry>()))
            .Callback<AuditLogEntry>(entry => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var logger = new AuditLogger(_loggerMock.Object, _appLoggerMock.Object);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await logger.LogClockOutAsync(
            employeeId: "emp-001",
            conversationThreadId: "thread-123",
            messageId: "msg-456",
            timestamp: timestamp,
            totalHours: 8.5m,
            statusCode: 200
        );

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Action.Should().Be("clock-out");
        capturedEntry.EmployeeId.Should().Be("emp-001");
        capturedEntry.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task LogActionAsync_LoggingFails_DoesNotThrow()
    {
        // Arrange
        _loggerMock.Setup(x => x.LogAsync(It.IsAny<AuditLogEntry>()))
            .ThrowsAsync(new InvalidOperationException("Blob storage unavailable"));

        var logger = new AuditLogger(_loggerMock.Object, _appLoggerMock.Object);

        // Act
        var act = async () => await logger.LogActionAsync(
            employeeId: "emp-001",
            action: "clock-in",
            conversationThreadId: "thread-123",
            messageId: "msg-456"
        );

        // Assert - Should not throw, audit logging is best-effort
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogActionAsync_WithError_CapturesErrorDetails()
    {
        // Arrange
        AuditLogEntry? capturedEntry = null;
        _loggerMock.Setup(x => x.LogAsync(It.IsAny<AuditLogEntry>()))
            .Callback<AuditLogEntry>(entry => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var logger = new AuditLogger(_loggerMock.Object, _appLoggerMock.Object);
        var error = new AuditError
        {
            Message = "Factorial HR API timeout",
            Code = "TIMEOUT",
            StackTrace = "at FactorialHRService.ClockInAsync..."
        };

        // Act
        await logger.LogActionAsync(
            employeeId: "emp-001",
            action: "clock-in",
            conversationThreadId: "thread-123",
            messageId: "msg-456",
            statusCode: 504,
            error: error
        );

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Error.Should().NotBeNull();
        capturedEntry.Error!.Message.Should().Be("Factorial HR API timeout");
        capturedEntry.Error.Code.Should().Be("TIMEOUT");
        capturedEntry.StatusCode.Should().Be(504);
    }

    [Fact]
    public async Task LogActionAsync_HandlesNullOptionalParameters()
    {
        // Arrange
        AuditLogEntry? capturedEntry = null;
        _loggerMock.Setup(x => x.LogAsync(It.IsAny<AuditLogEntry>()))
            .Callback<AuditLogEntry>(entry => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var logger = new AuditLogger(_loggerMock.Object, _appLoggerMock.Object);

        // Act
        await logger.LogActionAsync(
            employeeId: "emp-001",
            action: "clock-in",
            conversationThreadId: "thread-123",
            messageId: "msg-456"
            // All optional parameters omitted
        );

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.RequestData.Should().BeNull();
        capturedEntry.ResponseData.Should().BeNull();
        capturedEntry.Error.Should().BeNull();
        capturedEntry.SourceIp.Should().Be(string.Empty);
        capturedEntry.UserAgent.Should().Be(string.Empty);
    }
}
