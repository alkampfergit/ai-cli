namespace AiCli.Models;

/// <summary>
/// Configuration options for the CLI application
/// </summary>
public class CliOptions
{
    /// <summary>
    /// Inline prompt text
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Path to file containing the prompt
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Whether to read prompt from STDIN
    /// </summary>
    public bool UseStdin { get; set; }

    /// <summary>
    /// AI model to use
    /// </summary>
    public string Model { get; set; } = "gpt-3.5-turbo";

    /// <summary>
    /// Temperature for generation
    /// </summary>
    public float Temperature { get; set; } = 1.0f;

    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Output file path
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// Output format (text or json)
    /// </summary>
    public string Format { get; set; } = "text";

    /// <summary>
    /// Whether to stream the response
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// API key override
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the API
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Whether to enter configuration mode
    /// </summary>
    public bool Config { get; set; }
}