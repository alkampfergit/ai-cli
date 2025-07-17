using AiCli.Application;
using AiCli.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiCli.Infrastructure;

/// <summary>
/// File-based implementation of user settings service
/// </summary>
internal sealed class FileUserSettingsService : IUserSettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<FileUserSettingsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the FileUserSettingsService class
    /// </summary>
    /// <param name="settingsFilePath">Path to the settings file</param>
    /// <param name="logger">Logger instance</param>
    public FileUserSettingsService(string settingsFilePath, ILogger<FileUserSettingsService> logger)
    {
        _settingsFilePath = settingsFilePath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc/>
    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInformation("Settings file not found at {FilePath}, using defaults", _settingsFilePath);
                return UserSettings.CreateDefault();
            }

            var json = File.ReadAllText(_settingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Settings file is empty at {FilePath}, using defaults", _settingsFilePath);
                return UserSettings.CreateDefault();
            }

            var settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);
            if (settings == null)
            {
                _logger.LogWarning("Failed to deserialize settings from {FilePath}, using defaults", _settingsFilePath);
                return UserSettings.CreateDefault();
            }

            // Ensure we have valid model configurations
            if (settings.ModelConfigurations == null || settings.ModelConfigurations.Count == 0)
            {
                settings.ModelConfigurations = new List<ModelConfiguration>
                {
                    ModelConfiguration.CreateDefault()
                };
            }

            // Ensure non-null values for required properties in model configurations
            foreach (var modelConfig in settings.ModelConfigurations)
            {
                modelConfig.Model ??= "gpt-3.5-turbo";
                modelConfig.Format ??= "text";
                modelConfig.Id ??= "default";
                modelConfig.Name ??= "Default Configuration";
            }

            // Ensure default model configuration ID is valid
            if (string.IsNullOrEmpty(settings.DefaultModelConfigurationId) || 
                !settings.ModelConfigurations.Any(m => m.Id == settings.DefaultModelConfigurationId))
            {
                settings.DefaultModelConfigurationId = settings.ModelConfigurations.First().Id;
            }

            _logger.LogInformation("Settings loaded successfully from {FilePath}", _settingsFilePath);
            return settings;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse settings file at {FilePath}, using defaults", _settingsFilePath);
            return UserSettings.CreateDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings from {FilePath}, using defaults", _settingsFilePath);
            return UserSettings.CreateDefault();
        }
    }

    /// <inheritdoc/>
    public void Save(UserSettings settings)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created settings directory: {Directory}", directory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);

            // Set restrictive permissions on Unix systems
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(_settingsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set file permissions on {FilePath}", _settingsFilePath);
                }
            }

            _logger.LogInformation("Settings saved successfully to {FilePath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings to {FilePath}", _settingsFilePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public UserSettings ResetToDefault()
    {
        var defaultSettings = UserSettings.CreateDefault();
        Save(defaultSettings);
        _logger.LogInformation("Settings reset to default values and saved");
        return defaultSettings;
    }
}