using FluentAssertions;
using HRAgent.Api.Services;
using HRAgent.Contracts.Models;
using HRAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;

namespace HRAgent.Api.Tests.Unit;

/// <summary>
/// Unit tests for ConversationStore
/// Tests conversation persistence operations
/// </summary>
public class ConversationStoreTests
{
    private readonly Mock<IConversationStore> _storeMock;
    private readonly Mock<ILogger<ConversationStore>> _loggerMock;

    public ConversationStoreTests()
    {
        _storeMock = new Mock<IConversationStore>();
        _loggerMock = new Mock<ILogger<ConversationStore>>();
    }

    [Fact]
    public async Task CreateThreadAsync_SetsTimestamps_ReturnsThread()
    {
        // Arrange
        var thread = new ConversationThread
        {
            Id = "thread-001",
            EmployeeId = "emp-001",
            Messages = new List<ConversationMessage>()
        };

        _storeMock.Setup(x => x.CreateThreadAsync(It.IsAny<ConversationThread>()))
            .ReturnsAsync((ConversationThread t) => t);

        var store = new ConversationStore(_storeMock.Object, _loggerMock.Object);
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await store.CreateThreadAsync(thread);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().BeOnOrAfter(before);
        result.UpdatedAt.Should().BeOnOrAfter(before);
        result.CreatedAt.Should().Be(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateThreadAsync_UpdatesTimestamp_ReturnsThread()
    {
        // Arrange
        var originalTime = DateTimeOffset.UtcNow.AddHours(-1);
        var thread = new ConversationThread
        {
            Id = "thread-001",
            EmployeeId = "emp-001",
            CreatedAt = originalTime,
            UpdatedAt = originalTime,
            Messages = new List<ConversationMessage>()
        };

        _storeMock.Setup(x => x.UpdateThreadAsync(It.IsAny<ConversationThread>()))
            .ReturnsAsync((ConversationThread t) => t);

        var store = new ConversationStore(_storeMock.Object, _loggerMock.Object);
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await store.UpdateThreadAsync(thread);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().Be(originalTime); // Should not change
        result.UpdatedAt.Should().BeOnOrAfter(before); // Should be updated
        result.UpdatedAt.Should().BeAfter(result.CreatedAt);
    }

    [Fact]
    public async Task GetThreadAsync_ThreadExists_ReturnsThread()
    {
        // Arrange
        var expectedThread = new ConversationThread
        {
            Id = "thread-001",
            EmployeeId = "emp-001",
            Messages = new List<ConversationMessage>()
        };

        _storeMock.Setup(x => x.GetThreadAsync("thread-001", "emp-001"))
            .ReturnsAsync(expectedThread);

        var store = new ConversationStore(_storeMock.Object, _loggerMock.Object);

        // Act
        var result = await store.GetThreadAsync("thread-001", "emp-001");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("thread-001");
        result.EmployeeId.Should().Be("emp-001");
    }

    [Fact]
    public async Task GetThreadAsync_ThreadNotFound_ReturnsNull()
    {
        // Arrange
        _storeMock.Setup(x => x.GetThreadAsync("nonexistent", "emp-001"))
            .ReturnsAsync((ConversationThread?)null);

        var store = new ConversationStore(_storeMock.Object, _loggerMock.Object);

        // Act
        var result = await store.GetThreadAsync("nonexistent", "emp-001");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecentThreadsAsync_ReturnsOrderedThreads()
    {
        // Arrange
        var threads = new List<ConversationThread>
        {
            new ConversationThread { Id = "thread-003", EmployeeId = "emp-001", UpdatedAt = DateTimeOffset.UtcNow },
            new ConversationThread { Id = "thread-002", EmployeeId = "emp-001", UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1) },
            new ConversationThread { Id = "thread-001", EmployeeId = "emp-001", UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2) }
        };

        _storeMock.Setup(x => x.GetRecentThreadsAsync("emp-001", 10))
            .ReturnsAsync(threads);

        var store = new ConversationStore(_storeMock.Object, _loggerMock.Object);

        // Act
        var result = await store.GetRecentThreadsAsync("emp-001", 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be("thread-003"); // Most recent
        result[2].Id.Should().Be("thread-001"); // Oldest
    }

    [Fact]
    public async Task AppendMessageAsync_AddsMessageToThread()
    {
        // Arrange
        var message = new ConversationMessage
        {
            Id = "msg-001",
            Role = "user",
            Content = "I'm starting work now"
        };

        _storeMock.Setup(x => x.AppendMessageAsync("thread-001", "emp-001", It.IsAny<ConversationMessage>()))
            .ReturnsAsync(message);

        var store = new ConversationStore(_storeMock.Object, _loggerMock.Object);

        // Act
        var result = await store.AppendMessageAsync("thread-001", "emp-001", message);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("msg-001");
        result.Content.Should().Be("I'm starting work now");
    }
}
