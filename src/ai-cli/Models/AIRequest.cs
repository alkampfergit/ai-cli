namespace AiCli.Models;

/// <summary>
/// Represents a request to an AI service
/// </summary>
public record AIRequest
{
    /// <summary>
    /// The prompt text to send to the AI service
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// The model to use for generation
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Controls randomness (0.0 to 2.0)
    /// </summary>
    public float Temperature { get; init; } = 1.0f;

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Whether to stream the response
    /// </summary>
    public bool Stream { get; init; }
}