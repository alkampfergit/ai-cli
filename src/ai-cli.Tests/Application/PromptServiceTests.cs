using AiCli.Application;
using AiCli.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AiCli.Tests.Application;

public class PromptServiceTests
{
    private readonly Mock<IAIClient> _mockAIClient;
    private readonly Mock<ILogger<PromptService>> _mockLogger;
    private readonly PromptService _promptService;

    public PromptServiceTests()
    {
        _mockAIClient = new Mock<IAIClient>();
        _mockLogger = new Mock<ILogger<PromptService>>();
        _promptService = new PromptService(_mockAIClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessPromptAsync_WithInlinePrompt_ShouldReturnResponse()
    {
        // Arrange
        var options = new CliOptions
        {
            Prompt = "Hello, world!",
            Model = "gpt-3.5-turbo"
        };

        var expectedResponse = new AIResponse
        {
            Content = "Hello! How can I help you today?",
            Model = "gpt-3.5-turbo",
            RawResponse = "{\"content\":\"Hello! How can I help you today?\"}",
            Success = true
        };

        _mockAIClient.Setup(x => x.SendRequestAsync(
            It.Is<AIRequest>(r => r.Prompt == "Hello, world!" && r.Model == "gpt-3.5-turbo"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _promptService.ProcessPromptAsync(options);

        // Assert
        result.Should().BeEquivalentTo(expectedResponse);
        _mockAIClient.Verify(x => x.SendRequestAsync(
            It.Is<AIRequest>(r => r.Prompt == "Hello, world!" && r.Model == "gpt-3.5-turbo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPromptAsync_WithFilePrompt_ShouldReadFileAndReturnResponse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "File content");

        var options = new CliOptions
        {
            FilePath = tempFile,
            Model = "gpt-3.5-turbo"
        };

        var expectedResponse = new AIResponse
        {
            Content = "Response to file content",
            Model = "gpt-3.5-turbo",
            RawResponse = "{\"content\":\"Response to file content\"}",
            Success = true
        };

        _mockAIClient.Setup(x => x.SendRequestAsync(
            It.Is<AIRequest>(r => r.Prompt == "File content" && r.Model == "gpt-3.5-turbo"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        try
        {
            // Act
            var result = await _promptService.ProcessPromptAsync(options);

            // Assert
            result.Should().BeEquivalentTo(expectedResponse);
            _mockAIClient.Verify(x => x.SendRequestAsync(
                It.Is<AIRequest>(r => r.Prompt == "File content" && r.Model == "gpt-3.5-turbo"),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessPromptAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var options = new CliOptions
        {
            FilePath = "nonexistent.txt",
            Model = "gpt-3.5-turbo"
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            _promptService.ProcessPromptAsync(options));
    }

    [Fact]
    public async Task ProcessPromptAsync_WithNoPromptSource_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new CliOptions
        {
            Model = "gpt-3.5-turbo"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _promptService.ProcessPromptAsync(options));
    }

    [Fact]
    public async Task ProcessStreamingPromptAsync_WithInlinePrompt_ShouldReturnChunks()
    {
        // Arrange
        var options = new CliOptions
        {
            Prompt = "Hello, world!",
            Model = "gpt-3.5-turbo"
        };

        var chunks = new[] { "Hello", "! How", " can I", " help?" };
        
        _mockAIClient.Setup(x => x.SendStreamingRequestAsync(
            It.Is<AIRequest>(r => r.Prompt == "Hello, world!" && r.Model == "gpt-3.5-turbo"),
            It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(chunks));

        // Act
        var result = new List<string>();
        await foreach (var chunk in _promptService.ProcessStreamingPromptAsync(options))
        {
            result.Add(chunk);
        }

        // Assert
        result.Should().BeEquivalentTo(chunks);
        _mockAIClient.Verify(x => x.SendStreamingRequestAsync(
            It.Is<AIRequest>(r => r.Prompt == "Hello, world!" && r.Model == "gpt-3.5-turbo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<string> CreateAsyncEnumerable(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}