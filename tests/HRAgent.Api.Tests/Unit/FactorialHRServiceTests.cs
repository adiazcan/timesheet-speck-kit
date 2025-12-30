using FluentAssertions;
using HRAgent.Api.Services;
using HRAgent.Contracts.Factorial;
using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace HRAgent.Api.Tests.Unit;

/// <summary>
/// Unit tests for FactorialHRService
/// Tests API communication, retry logic, and error handling
/// </summary>
public class FactorialHRServiceTests
{
    private readonly Mock<ISecretsManager> _secretsManagerMock;
    private readonly Mock<ILogger<FactorialHRService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;

    public FactorialHRServiceTests()
    {
        _secretsManagerMock = new Mock<ISecretsManager>();
        _loggerMock = new Mock<ILogger<FactorialHRService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.factorialhr.com")
        };

        // Setup default API key retrieval
        _secretsManagerMock
            .Setup(x => x.GetSecretAsync("factorial-hr-api-key"))
            .ReturnsAsync("test-api-key-12345");
    }

    [Fact]
    public async Task ClockInAsync_SuccessfulRequest_ReturnsTimesheetResponse()
    {
        // Arrange
        var request = new ClockInRequest
        {
            EmployeeId = "emp-001",
            Timestamp = DateTime.UtcNow
        };

        var expectedResponse = new TimesheetResponse
        {
            Success = true,
            TimesheetId = "ts-12345",
            Message = "Clocked in successfully"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var service = new FactorialHRService(_httpClient, _secretsManagerMock.Object, _loggerMock.Object);

        // Act
        var result = await service.ClockInAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TimesheetId.Should().Be("ts-12345");
    }

    [Fact]
    public async Task ClockOutAsync_SuccessfulRequest_ReturnsTimesheetResponse()
    {
        // Arrange
        var request = new ClockOutRequest
        {
            EmployeeId = "emp-001",
            Timestamp = DateTime.UtcNow
        };

        var expectedResponse = new TimesheetResponse
        {
            Success = true,
            TimesheetId = "ts-12345",
            TotalHours = 8.5m,
            Message = "Clocked out successfully"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var service = new FactorialHRService(_httpClient, _secretsManagerMock.Object, _loggerMock.Object);

        // Act
        var result = await service.ClockOutAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TotalHours.Should().Be(8.5m);
    }

    [Fact]
    public async Task ClockInAsync_ApiKeyNotFound_ThrowsException()
    {
        // Arrange
        _secretsManagerMock
            .Setup(x => x.GetSecretAsync("factorial-hr-api-key"))
            .ThrowsAsync(new KeyNotFoundException("API key not found"));

        var service = new FactorialHRService(_httpClient, _secretsManagerMock.Object, _loggerMock.Object);
        var request = new ClockInRequest { EmployeeId = "emp-001", Timestamp = DateTime.UtcNow };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ClockInAsync(request));
    }

    [Fact]
    public async Task ClockInAsync_HttpRequestFails_ThrowsException()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal server error")
            });

        var service = new FactorialHRService(
            _httpClient, 
            _secretsManagerMock.Object, 
            _loggerMock.Object,
            new FactorialHRServiceOptions { ThrowOnFailure = true });

        var request = new ClockInRequest { EmployeeId = "emp-001", Timestamp = DateTime.UtcNow };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => service.ClockInAsync(request));
    }

    [Fact]
    public async Task ClockInAsync_CachesApiKey_OnlyCallsSecretsManagerOnce()
    {
        // Arrange
        var response = new TimesheetResponse { Success = true, TimesheetId = "ts-001" };
        
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(response))
            });

        var service = new FactorialHRService(_httpClient, _secretsManagerMock.Object, _loggerMock.Object);
        var request1 = new ClockInRequest { EmployeeId = "emp-001", Timestamp = DateTime.UtcNow };
        var request2 = new ClockInRequest { EmployeeId = "emp-002", Timestamp = DateTime.UtcNow };

        // Act
        await service.ClockInAsync(request1);
        await service.ClockInAsync(request2);

        // Assert - API key should only be fetched once
        _secretsManagerMock.Verify(
            x => x.GetSecretAsync("factorial-hr-api-key"), 
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_ReturnsEmployeeTimesheetStatus()
    {
        // Arrange
        var employeeId = "emp-001";
        var expectedStatus = new TimesheetStatusResponse
        {
            EmployeeId = employeeId,
            IsClockedIn = true,
            ClockInTime = DateTime.UtcNow.AddHours(-2),
            CurrentHours = 2.0m
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedStatus))
            });

        var service = new FactorialHRService(_httpClient, _secretsManagerMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetCurrentStatusAsync(employeeId);

        // Assert
        result.Should().NotBeNull();
        result.IsClockedIn.Should().BeTrue();
        result.CurrentHours.Should().Be(2.0m);
    }
}
