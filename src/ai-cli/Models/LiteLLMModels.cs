using System.Text.Json.Serialization;

namespace AiCli.Models;

/// <summary>
/// Response model for LiteLLM /models endpoint
/// </summary>
public class LiteLLMModelsResponse
{
    /// <summary>
    /// List of available models
    /// </summary>
    [JsonPropertyName("data")]
    public List<LiteLLMModel> Data { get; set; } = new();
}

/// <summary>
/// Individual model information from LiteLLM
/// </summary>
public class LiteLLMModel
{
    /// <summary>
    /// Model identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Object type (usually "model")
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; set; }

    /// <summary>
    /// Model owner
    /// </summary>
    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = string.Empty;
}