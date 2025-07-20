using AiCli.Infrastructure;
using AiCli.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace AiCli.Tests.Infrastructure;

public class OpenAIClientTests : IDisposable
{
    private readonly Mock<ILogger<OpenAIClient>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly OpenAIClient _openAIClient;

    public OpenAIClientTests()
    {
        _mockLogger = new Mock<ILogger<OpenAIClient>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _openAIClient = new OpenAIClient(_httpClient, _mockLogger.Object, "test-api-key");
    }

    [Fact]
    public async Task SendRequestAsync_WithValidRequest_ShouldReturnSuccessResponse()
    {
        // Arrange
        var request = new AIRequest
        {
            Prompt = "Hello, world!",
            Model = "gpt-3.5-turbo",
            Temperature = 0.7f
        };

        var responseJson = """
        {
            "choices": [
                {
                    "message": {
                        "content": "Hello! How can I help you today?"
                    }
                }
            ],
            "model": "gpt-3.5-turbo"
        }
        """;

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _openAIClient.SendRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Content.Should().Be("Hello! How can I help you today?");
        result.Model.Should().Be("gpt-3.5-turbo");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendRequestAsync_WithApiError_ShouldReturnErrorResponse()
    {
        // Arrange
        var request = new AIRequest
        {
            Prompt = "Hello, world!",
            Model = "gpt-3.5-turbo"
        };

        var errorResponse = """
        {
            "error": {
                "message": "Invalid API key",
                "type": "invalid_request_error"
            }
        }
        """;

        var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(errorResponse, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _openAIClient.SendRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API request failed with status Unauthorized");
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task SendRequestAsync_WithHttpException_ShouldReturnErrorResponse()
    {
        // Arrange
        var request = new AIRequest
        {
            Prompt = "Hello, world!",
            Model = "gpt-3.5-turbo"
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _openAIClient.SendRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Network error");
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task SendStreamingRequestAsync_WithValidRequest_ShouldReturnChunks()
    {
        // Arrange
        var request = new AIRequest
        {
            Prompt = "Hello, world!",
            Model = "gpt-3.5-turbo",
            Stream = true
        };

        var streamingResponse = """
        data: {"choices":[{"delta":{"content":"Hello"}}]}

        data: {"choices":[{"delta":{"content":"! How"}}]}

        data: {"choices":[{"delta":{"content":" can I help?"}}]}

        data: [DONE]

        """;

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(streamingResponse, Encoding.UTF8, "text/plain")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _openAIClient.SendStreamingRequestAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(3);
        chunks[0].Should().Be("Hello");
        chunks[1].Should().Be("! How");
        chunks[2].Should().Be(" can I help?");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
        }
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}