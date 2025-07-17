using AiCli.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace AiCli.Application;

/// <summary>
/// Service for handling prompt processing and AI interactions
/// </summary>
public class PromptService : IPromptService
{
    private readonly IAIClient _aiClient;
    private readonly ILogger<PromptService> _logger;

    /// <summary>
    /// Initializes a new instance of the PromptService class
    /// </summary>
    /// <param name="aiClient">The AI client to use for requests</param>
    /// <param name="logger">The logger instance</param>
    public PromptService(IAIClient aiClient, ILogger<PromptService> logger)
    {
        _aiClient = aiClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AIResponse> ProcessPromptAsync(CliOptions options, CancellationToken cancellationToken = default)
    {
        var prompt = await GetPromptTextAsync(options, cancellationToken);

        var request = new AIRequest
        {
            Prompt = prompt,
            Model = options.Model,
            Temperature = options.Temperature,
            MaxTokens = options.MaxTokens,
            Stream = false
        };

        _logger.LogInformation("Processing prompt with model {Model}", options.Model);

        return await _aiClient.SendRequestAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> ProcessStreamingPromptAsync(CliOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = await GetPromptTextAsync(options, cancellationToken);

        var request = new AIRequest
        {
            Prompt = prompt,
            Model = options.Model,
            Temperature = options.Temperature,
            MaxTokens = options.MaxTokens,
            Stream = true
        };

        _logger.LogInformation("Processing streaming prompt with model {Model}", options.Model);

        await foreach (var chunk in _aiClient.SendStreamingRequestAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }

    private async Task<string> GetPromptTextAsync(CliOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.Prompt))
        {
            return options.Prompt;
        }

        if (!string.IsNullOrEmpty(options.FilePath))
        {
            if (!File.Exists(options.FilePath))
            {
                throw new FileNotFoundException($"Prompt file not found: {options.FilePath}");
            }
            return await File.ReadAllTextAsync(options.FilePath, cancellationToken);
        }

        if (options.UseStdin)
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            return await reader.ReadToEndAsync(cancellationToken);
        }

        throw new InvalidOperationException("No prompt source specified");
    }
}