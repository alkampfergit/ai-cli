using AiCli.Configuration;
using FluentAssertions;

namespace AiCli.Tests.Configuration;

public class SettingsPathProviderTests
{
    [Fact]
    public void GetDefaultSettingsPath_ShouldReturnValidPath()
    {
        // Act
        var settingsPath = SettingsPathProvider.GetDefaultSettingsPath();

        // Assert
        settingsPath.Should().NotBeNullOrEmpty();
        settingsPath.Should().EndWith("settings.json");
        settingsPath.Should().Contain("ai-cli");
    }

    [Fact]
    public void GetSettingsPath_WithCustomPath_ShouldReturnCustomPath()
    {
        // Arrange
        var customPath = "/custom/path/settings.json";

        // Act
        var settingsPath = SettingsPathProvider.GetSettingsPath(customPath);

        // Assert
        settingsPath.Should().NotBeNullOrEmpty();
        // The path should be the full path of the custom path
        settingsPath.Should().Contain("settings.json");
    }

    [Fact]
    public void GetSettingsPath_WithoutCustomPath_ShouldReturnDefaultPath()
    {
        // Act
        var settingsPath = SettingsPathProvider.GetSettingsPath();
        var defaultPath = SettingsPathProvider.GetDefaultSettingsPath();

        // Assert
        settingsPath.Should().Be(defaultPath);
    }

    [Fact]
    public void GetSettingsPath_WithEmptyCustomPath_ShouldReturnDefaultPath()
    {
        // Act
        var settingsPath = SettingsPathProvider.GetSettingsPath("");
        var defaultPath = SettingsPathProvider.GetDefaultSettingsPath();

        // Assert
        settingsPath.Should().Be(defaultPath);
    }

    [Fact]
    public void GetSettingsPath_WithNullCustomPath_ShouldReturnDefaultPath()
    {
        // Act
        var settingsPath = SettingsPathProvider.GetSettingsPath(null);
        var defaultPath = SettingsPathProvider.GetDefaultSettingsPath();

        // Assert
        settingsPath.Should().Be(defaultPath);
    }
}