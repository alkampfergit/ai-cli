namespace AiCli.Models;

/// <summary>
/// Represents a response from an AI service
/// </summary>
public record AIResponse
{
    /// <summary>
    /// The generated text content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The model used for generation
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Raw JSON response from the service
    /// </summary>
    public required string RawResponse { get; init; }

    /// <summary>
    /// Whether the response was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if the request failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}