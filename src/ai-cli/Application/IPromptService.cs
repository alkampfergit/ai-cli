using AiCli.Models;

namespace AiCli.Application;

/// <summary>
/// Service for handling prompt processing and AI interactions
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Processes a prompt request and returns the AI response
    /// </summary>
    /// <param name="options">CLI options containing the prompt and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The AI response</returns>
    Task<AIResponse> ProcessPromptAsync(CliOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a streaming prompt request
    /// </summary>
    /// <param name="options">CLI options containing the prompt and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> ProcessStreamingPromptAsync(CliOptions options, CancellationToken cancellationToken = default);
}