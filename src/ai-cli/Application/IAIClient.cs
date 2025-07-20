using AiCli.Models;

namespace AiCli.Application;

/// <summary>
/// Interface for AI service clients
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// Sends a request to the AI service and returns the response
    /// </summary>
    /// <param name="request">The AI request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The AI response</returns>
    Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a streaming request to the AI service
    /// </summary>
    /// <param name="request">The AI request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> SendStreamingRequestAsync(AIRequest request, CancellationToken cancellationToken = default);
}