using AiCli.Application;
using AiCli.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AiCli.Tests;

public class ProgramTests
{
    [Fact]
    public async Task PromptService_WhenIAIClientThrowsHttpRequestException_ShouldLogAndPropagateException()
    {
        // Arrange
        var mockAIClient = new Mock<IAIClient>();
        var mockLogger = new Mock<ILogger<PromptService>>();

        // Set up the AI client to throw an HttpRequestException (API error)
        var testException = new HttpRequestException("API service unavailable");
        mockAIClient.Setup(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        var promptService = new PromptService(mockAIClient.Object, mockLogger.Object);

        var cliOptions = new CliOptions
        {
            Prompt = "Test prompt",
            Model = "gpt-3.5-turbo",
            Temperature = 1.0f,
            Format = "text",
            Stream = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            promptService.ProcessPromptAsync(cliOptions));

        exception.Message.Should().Be("API service unavailable");

        // Verify the exception was thrown by the AI client
        mockAIClient.Verify(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify proper logging occurred before the exception
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing prompt with model gpt-3.5-turbo")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PromptService_WhenIAIClientThrowsTaskCanceledException_ShouldLogAndPropagateException()
    {
        // Arrange
        var mockAIClient = new Mock<IAIClient>();
        var mockLogger = new Mock<ILogger<PromptService>>();

        // Set up the AI client to throw a TaskCanceledException (timeout/cancellation)
        var testException = new TaskCanceledException("Operation was cancelled");
        mockAIClient.Setup(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        var promptService = new PromptService(mockAIClient.Object, mockLogger.Object);

        var cliOptions = new CliOptions
        {
            Prompt = "Test prompt",
            Model = "gpt-4",
            Temperature = 0.5f,
            Format = "json",
            Stream = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() =>
            promptService.ProcessPromptAsync(cliOptions));

        exception.Message.Should().Be("Operation was cancelled");

        // Verify the exception was thrown by the AI client
        mockAIClient.Verify(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify proper logging occurred before the exception
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing prompt with model gpt-4")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    [Fact]
    public async Task PromptService_WhenIAIClientThrowsException_ShouldLogExceptionAndPropagateException()
    {
        // Arrange
        var mockAIClient = new Mock<IAIClient>();
        var mockLogger = new Mock<ILogger<PromptService>>();

        // Set up the AI client to throw an exception
        var testException = new HttpRequestException("Connection timeout");
        mockAIClient.Setup(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        var promptService = new PromptService(mockAIClient.Object, mockLogger.Object);

        // Create CLI options that would trigger the exception
        var cliOptions = new CliOptions
        {
            Prompt = "Test prompt",
            Model = "gpt-3.5-turbo",
            Temperature = 1.0f,
            Format = "text",
            Stream = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            promptService.ProcessPromptAsync(cliOptions));

        exception.Message.Should().Be("Connection timeout");

        // Verify the exception was thrown by the AI client
        mockAIClient.Verify(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPromptAsync_WhenIAIClientThrowsException_ShouldPropagateException()
    {
        // Arrange
        var mockAIClient = new Mock<IAIClient>();
        var mockLogger = new Mock<ILogger<PromptService>>();

        // Set up the AI client to throw different types of exceptions
        var testException = new InvalidOperationException("API service unavailable");
        mockAIClient.Setup(x => x.SendRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        var promptService = new PromptService(mockAIClient.Object, mockLogger.Object);

        var cliOptions = new CliOptions
        {
            Prompt = "Test prompt",
            Model = "gpt-3.5-turbo"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            promptService.ProcessPromptAsync(cliOptions));

        exception.Message.Should().Be("API service unavailable");

        // Verify proper logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing prompt with model gpt-3.5-turbo")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessStreamingPromptAsync_WhenIAIClientThrowsException_ShouldPropagateException()
    {
        // Arrange
        var mockAIClient = new Mock<IAIClient>();
        var mockLogger = new Mock<ILogger<PromptService>>();

        // Set up the AI client to throw an exception during streaming
        var testException = new TaskCanceledException("Request cancelled");
        mockAIClient.Setup(x => x.SendStreamingRequestAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
            .Throws(testException);

        var promptService = new PromptService(mockAIClient.Object, mockLogger.Object);

        var cliOptions = new CliOptions
        {
            Prompt = "Test streaming prompt",
            Model = "gpt-3.5-turbo",
            Stream = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await foreach (var chunk in promptService.ProcessStreamingPromptAsync(cliOptions))
            {
                // This should not execute due to exception
            }
        });

        exception.Message.Should().Be("Request cancelled");

        // Verify proper logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing streaming prompt with model gpt-3.5-turbo")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}