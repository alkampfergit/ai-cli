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
        // Extract values by checking if the option token was provided
        var promptValue = GetOptionValue<string>(result, "--prompt", "-p");
        var fileValue = GetOptionValue<string>(result, "--file", "-f");
        var modelValue = GetOptionValue<string>(result, "--model", "-m") ?? "gpt-3.5-turbo";
        var temperatureValue = GetOptionValue<float?>(result, "--temperature") ?? 1.0f;
        var maxTokensValue = GetOptionValue<int?>(result, "--max-tokens");
        var topPValue = GetOptionValue<float?>(result, "--top-p");
        var outputFileValue = GetOptionValue<string>(result, "--output-file", "-o");
        var formatValue = GetOptionValue<string>(result, "--format") ?? "text";
        var streamValue = GetOptionValue<bool?>(result, "--stream") ?? false;
        var apiKeyValue = GetOptionValue<string>(result, "--api-key");
        var baseUrlValue = GetOptionValue<string>(result, "--base-url");

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

    /// <summary>
    /// Helper method to extract option values from parse result
    /// </summary>
    /// <typeparam name="T">Type of the option value</typeparam>
    /// <param name="result">Parse result</param>
    /// <param name="optionName">Option name</param>
    /// <param name="shortName">Optional short name</param>
    /// <returns>Option value if found, otherwise default</returns>
    private static T? GetOptionValue<T>(System.CommandLine.Parsing.ParseResult result, string optionName, string? shortName = null)
    {
        // Check if the option token was provided in the parsed tokens
        var tokens = result.Tokens.Select(t => t.Value).ToList();
        
        bool hasOption = tokens.Contains(optionName) || (shortName != null && tokens.Contains(shortName));
        
        if (!hasOption)
        {
            return default(T);
        }

        // Find the index of the option
        var optionIndex = tokens.IndexOf(optionName);
        if (optionIndex == -1 && shortName != null)
        {
            optionIndex = tokens.IndexOf(shortName);
        }

        if (optionIndex == -1 || optionIndex >= tokens.Count - 1)
        {
            // For bool options, presence means true
            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
            {
                return (T)(object)true;
            }
            return default(T);
        }

        // Get the value after the option
        var valueToken = tokens[optionIndex + 1];
        
        // For bool options, if the next token is also an option, then this is a flag
        if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
        {
            if (valueToken.StartsWith("-"))
            {
                return (T)(object)true;
            }
            return (T)(object)bool.Parse(valueToken);
        }

        // Try to convert to the target type
        if (typeof(T) == typeof(string))
        {
            return (T)(object)valueToken;
        }
        else if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
        {
            if (float.TryParse(valueToken, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
            {
                return (T)(object)floatValue;
            }
        }
        else if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
        {
            if (int.TryParse(valueToken, out var intValue))
            {
                return (T)(object)intValue;
            }
        }

        return default(T);
    }
}