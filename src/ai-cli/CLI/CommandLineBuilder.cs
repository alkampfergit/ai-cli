using AiCli.Models;
using System.CommandLine;

namespace AiCli.CLI;

/// <summary>
/// Builds the command line interface
/// </summary>
public static class CommandLineBuilder
{
    /// <summary>
    /// Creates the root command with all options
    /// </summary>
    /// <returns>Configured root command</returns>
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("AI CLI - Send prompts to OpenAI-compatible APIs");

        // Prompt options (mutually exclusive)
        var promptOption = new Option<string?>(
            aliases: new[] { "--prompt", "-p" },
            description: "Prompt text to send to the AI service");

        var fileOption = new Option<string?>(
            aliases: new[] { "--file", "-f" },
            description: "Path to file containing the prompt");

        // Model and generation options
        var modelOption = new Option<string>(
            aliases: new[] { "--model", "-m" },
            getDefaultValue: () => "gpt-3.5-turbo",
            description: "AI model to use");

        var temperatureOption = new Option<float>(
            name: "--temperature",
            getDefaultValue: () => 1.0f,
            description: "Temperature for generation (0.0 to 2.0)");

        var maxTokensOption = new Option<int?>(
            name: "--max-tokens",
            description: "Maximum number of tokens to generate");

        var topPOption = new Option<float?>(
            name: "--top-p",
            description: "Top-p sampling parameter (0.0 to 1.0)");

        // Output options
        var outputFileOption = new Option<string?>(
            aliases: new[] { "--output-file", "-o" },
            description: "Save response to file");

        var formatOption = new Option<string>(
            name: "--format",
            getDefaultValue: () => "text",
            description: "Output format (text or json)");

        var streamOption = new Option<bool>(
            name: "--stream",
            description: "Stream response tokens as they arrive");

        // API configuration options
        var apiKeyOption = new Option<string?>(
            name: "--api-key",
            description: "API key (overrides AI_API_KEY environment variable)");

        var baseUrlOption = new Option<string?>(
            name: "--base-url",
            description: "Base URL for the API endpoint");

        // Add all options to the root command
        rootCommand.AddOption(promptOption);
        rootCommand.AddOption(fileOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(temperatureOption);
        rootCommand.AddOption(maxTokensOption);
        rootCommand.AddOption(topPOption);
        rootCommand.AddOption(outputFileOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(streamOption);
        rootCommand.AddOption(apiKeyOption);
        rootCommand.AddOption(baseUrlOption);

        // Add validation
        rootCommand.AddValidator(result =>
        {
            var promptValue = result.GetValueForOption(promptOption);
            var fileValue = result.GetValueForOption(fileOption);
            var hasStdin = !Console.IsInputRedirected;

            var sourceCount = 0;
            if (!string.IsNullOrEmpty(promptValue)) sourceCount++;
            if (!string.IsNullOrEmpty(fileValue)) sourceCount++;
            if (!hasStdin) sourceCount++;

            if (sourceCount > 1)
            {
                result.ErrorMessage = "Only one prompt source can be specified: --prompt, --file, or stdin";
            }
            else if (sourceCount == 0 && hasStdin)
            {
                result.ErrorMessage = "No prompt source specified. Use --prompt, --file, or provide input via stdin";
            }

            var temperature = result.GetValueForOption(temperatureOption);
            if (temperature < 0.0f || temperature > 2.0f)
            {
                result.ErrorMessage = "Temperature must be between 0.0 and 2.0";
            }

            var topP = result.GetValueForOption(topPOption);
            if (topP.HasValue && (topP < 0.0f || topP > 1.0f))
            {
                result.ErrorMessage = "Top-p must be between 0.0 and 1.0";
            }

            var format = result.GetValueForOption(formatOption);
            if (format != "text" && format != "json")
            {
                result.ErrorMessage = "Format must be 'text' or 'json'";
            }
        });

        return rootCommand;
    }

    /// <summary>
    /// Parses command line arguments into CLI options
    /// </summary>
    /// <param name="result">Parse result from System.CommandLine</param>
    /// <returns>Configured CLI options</returns>
    public static CliOptions ParseOptions(System.CommandLine.Parsing.ParseResult result)
    {
        var promptValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string?>>().First(o => o.HasAlias("--prompt")));
        var fileValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string?>>().First(o => o.HasAlias("--file")));
        var modelValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string>>().First(o => o.HasAlias("--model")));
        var temperatureValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<float>>().First(o => o.Name == "--temperature"));
        var maxTokensValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<int?>>().First(o => o.Name == "--max-tokens"));
        var topPValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<float?>>().First(o => o.Name == "--top-p"));
        var outputFileValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string?>>().First(o => o.HasAlias("--output-file")));
        var formatValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string>>().First(o => o.Name == "--format"));
        var streamValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<bool>>().First(o => o.Name == "--stream"));
        var apiKeyValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string?>>().First(o => o.Name == "--api-key"));
        var baseUrlValue = result.GetValueForOption(result.CommandResult.Command.Options.OfType<Option<string?>>().First(o => o.Name == "--base-url"));

        return new CliOptions
        {
            Prompt = promptValue,
            FilePath = fileValue,
            UseStdin = string.IsNullOrEmpty(promptValue) && 
                      string.IsNullOrEmpty(fileValue) && 
                      !Console.IsInputRedirected,
            Model = modelValue!,
            Temperature = temperatureValue,
            MaxTokens = maxTokensValue,
            TopP = topPValue,
            OutputFile = outputFileValue,
            Format = formatValue!,
            Stream = streamValue,
            ApiKey = apiKeyValue,
            BaseUrl = baseUrlValue
        };
    }
}