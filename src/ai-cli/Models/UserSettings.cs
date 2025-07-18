using AiCli.Attributes;

namespace AiCli.Models;

/// <summary>
/// POCO class representing model configuration
/// </summary>
public class ModelConfiguration
{
    /// <summary>
    /// Unique identifier for the model configuration
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the model configuration
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// API key for the AI service
    /// </summary>
    [EncryptedSetting]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the API endpoint
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Model name to use for requests
    /// </summary>
    public string Model { get; set; } = "gpt-3.5-turbo";

    /// <summary>
    /// Default temperature for generation
    /// </summary>
    public float Temperature { get; set; } = 1.0f;

    /// <summary>
    /// Default maximum tokens to generate
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Default output format
    /// </summary>
    public string Format { get; set; } = "text";

    /// <summary>
    /// Default streaming preference
    /// </summary>
    public bool Stream { get; set; } = false;

    /// <summary>
    /// Creates a new instance with default values
    /// </summary>
    public static ModelConfiguration CreateDefault()
    {
        return new ModelConfiguration
        {
            Id = "default",
            Name = "Default OpenAI Configuration",
            ApiKey = null,
            BaseUrl = null,
            Model = "gpt-3.5-turbo",
            Temperature = 1.0f,
            MaxTokens = null,
            Format = "text",
            Stream = false
        };
    }
}

/// <summary>
/// POCO class representing user settings configuration
/// </summary>
public class UserSettings
{
    /// <summary>
    /// List of model configurations
    /// </summary>
    public List<ModelConfiguration> ModelConfigurations { get; set; } = new();

    /// <summary>
    /// Default model configuration ID to use
    /// </summary>
    public string DefaultModelConfigurationId { get; set; } = "default";

    /// <summary>
    /// Refresh interval for settings file monitoring (in seconds)
    /// </summary>
    public int RefreshInterval { get; set; } = 30;

    /// <summary>
    /// Creates a new instance with default values
    /// </summary>
    public static UserSettings CreateDefault()
    {
        return new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
            },
            DefaultModelConfigurationId = "default",
            RefreshInterval = 30
        };
    }

    /// <summary>
    /// Gets the default model configuration
    /// </summary>
    public ModelConfiguration? GetDefaultModelConfiguration()
    {
        if (ModelConfigurations.Count == 0)
        {
            return null;
        }

        return ModelConfigurations.FirstOrDefault(m => m.Id == DefaultModelConfigurationId)
               ?? ModelConfigurations.FirstOrDefault();
    }

    /// <summary>
    /// Gets a model configuration by ID
    /// </summary>
    public ModelConfiguration? GetModelConfiguration(string id)
    {
        return ModelConfigurations.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>
    /// Adds or updates a model configuration
    /// </summary>
    public void AddOrUpdateModelConfiguration(ModelConfiguration modelConfiguration)
    {
        var existing = ModelConfigurations.FirstOrDefault(m => m.Id == modelConfiguration.Id);
        if (existing != null)
        {
            ModelConfigurations.Remove(existing);
        }
        ModelConfigurations.Add(modelConfiguration);
    }
}