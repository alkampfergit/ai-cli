using AiCli.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;

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

        var response = await _aiClient.SendRequestAsync(request, cancellationToken);

        // Delete the prompt file after processing if -f option was used
        if (!string.IsNullOrEmpty(options.FilePath) && File.Exists(options.FilePath))
        {
            try
            {
                File.Delete(options.FilePath);
                _logger.LogDebug("Deleted prompt file: {FilePath}", options.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete prompt file: {FilePath}", options.FilePath);
            }
        }

        return response;
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

        // Delete the prompt file after processing if -f option was used
        if (!string.IsNullOrEmpty(options.FilePath) && File.Exists(options.FilePath))
        {
            try
            {
                File.Delete(options.FilePath);
                _logger.LogDebug("Deleted prompt file: {FilePath}", options.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete prompt file: {FilePath}", options.FilePath);
            }
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
            // For interactive mode, read from Console.In which handles user input properly
            if (!Console.IsInputRedirected)
            {
                // Interactive mode - wait for user input
                var input = new StringBuilder();
                string? line;
                while ((line = Console.ReadLine()) != null)
                {
                    input.AppendLine(line);
                }
                return input.ToString().TrimEnd();
            }
            else
            {
                // Redirected input mode - read from stdin
                using var reader = new StreamReader(Console.OpenStandardInput());
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException("No prompt source specified");
    }
}