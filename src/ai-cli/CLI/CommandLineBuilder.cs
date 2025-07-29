using AiCli.Models;
using System.CommandLine;

namespace AiCli.CLI;

/// <summary>
/// Builds the command line interface
/// </summary>
public static class CommandLineBuilder
{
    // Store option references for parsing
    private static Option<string?> _promptOption = null!;
    private static Option<string?> _fileOption = null!;
    private static Option<string> _modelOption = null!;
    private static Option<float> _temperatureOption = null!;
    private static Option<int?> _maxTokensOption = null!;
    private static Option<string?> _outputFileOption = null!;
    private static Option<string> _formatOption = null!;
    private static Option<bool> _streamOption = null!;
    private static Option<bool> _configOption = null!;

    /// <summary>
    /// Creates the root command with all options
    /// </summary>
    /// <returns>Configured root command</returns>
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("AI CLI - Send prompts to OpenAI-compatible APIs");

        // Prompt options (mutually exclusive)
        _promptOption = new Option<string?>(
            aliases: new[] { "--prompt", "-p", "/p" },
            description: "Prompt text to send to the AI service");

        _fileOption = new Option<string?>(
            aliases: new[] { "--file", "-f", "/f" },
            description: "Path to file containing the prompt");

        // Model and generation options
        _modelOption = new Option<string>(
            aliases: new[] { "--model", "-m", "/m" },
            getDefaultValue: () => "gpt-3.5-turbo",
            description: "AI model to use");

        _temperatureOption = new Option<float>(
            name: "--temperature",
            parseArgument: result =>
            {
                if (float.TryParse(result.Tokens.Single().Value, 
                    System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out var value))
                {
                    return value;
                }
                result.ErrorMessage = "Invalid temperature value. Must be a number.";
                return 0f;
            },
            description: "Temperature for generation (0.0 to 2.0)");
        _temperatureOption.SetDefaultValue(1.0f);

        _maxTokensOption = new Option<int?>(
            name: "--max-tokens",
            description: "Maximum number of tokens to generate");

        // Output options
        _outputFileOption = new Option<string?>(
            aliases: new[] { "--output-file", "-o", "/o" },
            description: "Save response to file");

        _formatOption = new Option<string>(
            name: "--format",
            getDefaultValue: () => "text",
            description: "Output format (text or json)");

        _streamOption = new Option<bool>(
            name: "--stream",
            description: "Stream response tokens as they arrive");

        _configOption = new Option<bool>(
            name: "--config",
            description: "Enter configuration mode to manage model settings")
        {
            Arity = ArgumentArity.Zero
        };

        // Add all options to the root command
        rootCommand.AddOption(_promptOption);
        rootCommand.AddOption(_fileOption);
        rootCommand.AddOption(_modelOption);
        rootCommand.AddOption(_temperatureOption);
        rootCommand.AddOption(_maxTokensOption);
        rootCommand.AddOption(_outputFileOption);
        rootCommand.AddOption(_formatOption);
        rootCommand.AddOption(_streamOption);
        rootCommand.AddOption(_configOption);

        // Add validation
        rootCommand.AddValidator(result =>
        {
            // Check if --config flag is present using the proper System.CommandLine method
            var isConfigMode = result.GetValueForOption(_configOption);

            // Skip validation if in config mode
            if (isConfigMode)
            {
                return;
            }

            var promptValue = result.GetValueForOption(_promptOption);
            var fileValue = result.GetValueForOption(_fileOption);

            // Only count explicit prompt sources (prompt and file options)
            // Don't pre-emptively count stdin - it's only used when neither prompt nor file are provided
            var sourceCount = 0;
            if (!string.IsNullOrEmpty(promptValue)) sourceCount++;
            if (!string.IsNullOrEmpty(fileValue)) sourceCount++;

            if (sourceCount > 1)
            {
                result.ErrorMessage = "Only one prompt source can be specified: --prompt, --file, or stdin";
            }
            // Allow no sources - will default to stdin in interactive mode

            var temperature = result.GetValueForOption(_temperatureOption);
            if (temperature < 0.0f || temperature > 2.0f)
            {
                result.ErrorMessage = "Temperature must be between 0.0 and 2.0";
            }

            var format = result.GetValueForOption(_formatOption);
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
        // Use the stored option references for parsing
        var promptValue = result.GetValueForOption(_promptOption);
        var fileValue = result.GetValueForOption(_fileOption);
        var modelValue = result.GetValueForOption(_modelOption);
        var temperatureValue = result.GetValueForOption(_temperatureOption);
        var maxTokensValue = result.GetValueForOption(_maxTokensOption);
        var outputFileValue = result.GetValueForOption(_outputFileOption);
        var formatValue = result.GetValueForOption(_formatOption);
        var streamValue = result.GetValueForOption(_streamOption);
        var configValue = result.GetValueForOption(_configOption);

        return new CliOptions
        {
            Prompt = promptValue,
            FilePath = fileValue,
            UseStdin = string.IsNullOrEmpty(promptValue) && 
                      string.IsNullOrEmpty(fileValue) && 
                      !Console.IsInputRedirected,
            Model = modelValue ?? "gpt-3.5-turbo",
            Temperature = temperatureValue,
            MaxTokens = maxTokensValue,
            OutputFile = outputFileValue,
            Format = formatValue ?? "text",
            Stream = streamValue,
            Config = configValue
        };
    }

}