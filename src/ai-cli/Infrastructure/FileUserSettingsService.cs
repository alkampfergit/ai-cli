using AiCli.Application;
using AiCli.Attributes;
using AiCli.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace AiCli.Infrastructure;

/// <summary>
/// File-based implementation of user settings service
/// </summary>
internal sealed class FileUserSettingsService : IUserSettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<FileUserSettingsService> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the FileUserSettingsService class
    /// </summary>
    /// <param name="settingsFilePath">Path to the settings file</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="encryptionService">Encryption service for sensitive properties</param>
    public FileUserSettingsService(string settingsFilePath, ILogger<FileUserSettingsService> logger, IEncryptionService encryptionService)
    {
        _settingsFilePath = settingsFilePath;
        _logger = logger;
        _encryptionService = encryptionService;
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
            if (settings.ModelConfigurations.Count > 0)
            {
                if (string.IsNullOrEmpty(settings.DefaultModelConfigurationId) ||
                    !settings.ModelConfigurations.Any(m => m.Id == settings.DefaultModelConfigurationId))
                {
                    settings.DefaultModelConfigurationId = settings.ModelConfigurations.First().Id;
                }
            }
            else
            {
                settings.DefaultModelConfigurationId = "default";
            }

            // Decrypt encrypted properties
            DecryptEncryptedProperties(settings);

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

            // Create a copy for serialization and encrypt sensitive properties
            var settingsToSave = CloneSettings(settings);
            EncryptEncryptedProperties(settingsToSave);

            var json = JsonSerializer.Serialize(settingsToSave, _jsonOptions);
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

    /// <summary>
    /// Creates a deep copy of the settings for serialization
    /// </summary>
    private UserSettings CloneSettings(UserSettings settings)
    {
        // Simple JSON-based cloning
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        return JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions)!;
    }

    /// <summary>
    /// Encrypts properties marked with EncryptedSetting attribute
    /// </summary>
    private void EncryptEncryptedProperties(UserSettings settings)
    {
        foreach (var modelConfig in settings.ModelConfigurations)
        {
            EncryptEncryptedProperties(modelConfig);
        }
    }

    /// <summary>
    /// Encrypts properties marked with EncryptedSetting attribute on an object
    /// </summary>
    private void EncryptEncryptedProperties(object obj)
    {
        if (obj == null) return;

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var encryptedAttribute = property.GetCustomAttribute<EncryptedSettingAttribute>();
            if (encryptedAttribute != null && property.PropertyType == typeof(string))
            {
                var value = (string?)property.GetValue(obj);
                if (!string.IsNullOrEmpty(value) && !_encryptionService.IsEncrypted(value))
                {
                    try
                    {
                        var encryptedValue = _encryptionService.Encrypt(value);
                        property.SetValue(obj, encryptedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to encrypt property {PropertyName}", property.Name);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Decrypts properties marked with EncryptedSetting attribute
    /// </summary>
    private void DecryptEncryptedProperties(UserSettings settings)
    {
        foreach (var modelConfig in settings.ModelConfigurations)
        {
            DecryptEncryptedProperties(modelConfig);
        }
    }

    /// <summary>
    /// Decrypts properties marked with EncryptedSetting attribute on an object
    /// </summary>
    private void DecryptEncryptedProperties(object obj)
    {
        if (obj == null) return;

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var encryptedAttribute = property.GetCustomAttribute<EncryptedSettingAttribute>();
            if (encryptedAttribute != null && property.PropertyType == typeof(string))
            {
                var value = (string?)property.GetValue(obj);
                if (!string.IsNullOrEmpty(value) && _encryptionService.IsEncrypted(value))
                {
                    try
                    {
                        var decryptedValue = _encryptionService.Decrypt(value);
                        property.SetValue(obj, decryptedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to decrypt property {PropertyName}", property.Name);
                    }
                }
            }
        }
    }
}