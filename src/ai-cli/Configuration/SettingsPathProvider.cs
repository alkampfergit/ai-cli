namespace AiCli.Configuration;

/// <summary>
/// Provides platform-specific paths for user settings
/// </summary>
public static class SettingsPathProvider
{
    /// <summary>
    /// Gets the default settings file path for the current platform
    /// </summary>
    /// <returns>Full path to the settings file</returns>
    public static string GetDefaultSettingsPath()
    {
        var appDataPath = GetAppDataPath();
        var settingsDir = Path.Combine(appDataPath, "ai-cli");
        return Path.Combine(settingsDir, "settings.json");
    }

    /// <summary>
    /// Gets the platform-specific application data directory
    /// </summary>
    /// <returns>Path to the application data directory</returns>
    private static string GetAppDataPath()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows: %APPDATA%\ai-cli
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: ~/Library/Application Support/ai-cli
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support");
        }
        else
        {
            // Linux/Unix: ~/.config/ai-cli (XDG Base Directory Specification)
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfigHome))
            {
                return xdgConfigHome;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config");
        }
    }

    /// <summary>
    /// Gets the settings file path, with fallback to default if not provided
    /// </summary>
    /// <param name="customPath">Custom settings file path (optional)</param>
    /// <returns>Full path to the settings file</returns>
    public static string GetSettingsPath(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            return Path.GetFullPath(customPath);
        }

        return GetDefaultSettingsPath();
    }
}