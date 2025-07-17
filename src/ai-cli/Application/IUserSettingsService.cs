using AiCli.Models;

namespace AiCli.Application;

/// <summary>
/// Interface for user settings service management
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Load settings from storage
    /// </summary>
    /// <returns>The loaded user settings</returns>
    UserSettings Load();

    /// <summary>
    /// Save settings to storage
    /// </summary>
    /// <param name="settings">The settings to save</param>
    void Save(UserSettings settings);

    /// <summary>
    /// Reset all settings to default values and save them
    /// </summary>
    /// <returns>The reset settings</returns>
    UserSettings ResetToDefault();
}