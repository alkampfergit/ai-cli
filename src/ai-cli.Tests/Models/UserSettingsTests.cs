using AiCli.Models;
using FluentAssertions;

namespace AiCli.Tests.Models;

public class UserSettingsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var settings = new UserSettings();

        // Assert
        settings.ModelConfigurations.Should().NotBeNull();
        settings.ModelConfigurations.Should().BeEmpty();
        settings.DefaultModelConfigurationId.Should().Be("default");
        settings.RefreshInterval.Should().Be(30);
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstanceWithDefaults()
    {
        // Act
        var settings = UserSettings.CreateDefault();

        // Assert
        settings.ModelConfigurations.Should().NotBeNull();
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("default");
        settings.RefreshInterval.Should().Be(30);

        var defaultConfig = settings.ModelConfigurations.First();
        defaultConfig.Id.Should().Be("default");
        defaultConfig.Name.Should().Be("Default OpenAI Configuration");
        defaultConfig.Model.Should().Be("gpt-3.5-turbo");
    }

    [Fact]
    public void GetDefaultModelConfiguration_ShouldReturnCorrectConfiguration()
    {
        // Arrange
        var settings = new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
                new ModelConfiguration { Id = "config1", Name = "Config 1" },
                new ModelConfiguration { Id = "config2", Name = "Config 2" }
            },
            DefaultModelConfigurationId = "config2"
        };

        // Act
        var defaultConfig = settings.GetDefaultModelConfiguration();

        // Assert
        defaultConfig.Should().NotBeNull();
        defaultConfig!.Id.Should().Be("config2");
        defaultConfig.Name.Should().Be("Config 2");
    }

    [Fact]
    public void GetDefaultModelConfiguration_WithInvalidId_ShouldReturnFirstConfiguration()
    {
        // Arrange
        var settings = new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
                new ModelConfiguration { Id = "config1", Name = "Config 1" },
                new ModelConfiguration { Id = "config2", Name = "Config 2" }
            },
            DefaultModelConfigurationId = "nonexistent"
        };

        // Act
        var defaultConfig = settings.GetDefaultModelConfiguration();

        // Assert
        defaultConfig.Should().NotBeNull();
        defaultConfig!.Id.Should().Be("config1");
    }

    [Fact]
    public void GetModelConfiguration_ShouldReturnCorrectConfiguration()
    {
        // Arrange
        var settings = new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
                new ModelConfiguration { Id = "config1", Name = "Config 1" },
                new ModelConfiguration { Id = "config2", Name = "Config 2" }
            }
        };

        // Act
        var config = settings.GetModelConfiguration("config1");

        // Assert
        config.Should().NotBeNull();
        config!.Id.Should().Be("config1");
        config.Name.Should().Be("Config 1");
    }

    [Fact]
    public void GetModelConfiguration_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var settings = new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
                new ModelConfiguration { Id = "config1", Name = "Config 1" }
            }
        };

        // Act
        var config = settings.GetModelConfiguration("nonexistent");

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public void AddOrUpdateModelConfiguration_ShouldAddNewConfiguration()
    {
        // Arrange
        var settings = new UserSettings();
        var newConfig = new ModelConfiguration { Id = "new-config", Name = "New Config" };

        // Act
        settings.AddOrUpdateModelConfiguration(newConfig);

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.ModelConfigurations.First().Id.Should().Be("new-config");
    }

    [Fact]
    public void AddOrUpdateModelConfiguration_ShouldUpdateExistingConfiguration()
    {
        // Arrange
        var settings = new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
                new ModelConfiguration { Id = "config1", Name = "Old Name" }
            }
        };
        var updatedConfig = new ModelConfiguration { Id = "config1", Name = "New Name" };

        // Act
        settings.AddOrUpdateModelConfiguration(updatedConfig);

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.ModelConfigurations.First().Name.Should().Be("New Name");
    }
}

public class ModelConfigurationTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new ModelConfiguration();

        // Assert
        config.Id.Should().Be(string.Empty);
        config.Name.Should().Be(string.Empty);
        config.ApiKey.Should().BeNull();
        config.BaseUrl.Should().BeNull();
        config.Model.Should().Be("gpt-3.5-turbo");
        config.Temperature.Should().Be(1.0f);
        config.MaxTokens.Should().BeNull();
        config.Format.Should().Be("text");
        config.Stream.Should().BeFalse();
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstanceWithDefaults()
    {
        // Act
        var config = ModelConfiguration.CreateDefault();

        // Assert
        config.Id.Should().Be("default");
        config.Name.Should().Be("Default OpenAI Configuration");
        config.ApiKey.Should().BeNull();
        config.BaseUrl.Should().BeNull();
        config.Model.Should().Be("gpt-3.5-turbo");
        config.Temperature.Should().Be(1.0f);
        config.MaxTokens.Should().BeNull();
        config.Format.Should().Be("text");
        config.Stream.Should().BeFalse();
    }

    [Fact]
    public void Properties_ShouldBeSettableAndGettable()
    {
        // Arrange
        var config = new ModelConfiguration();

        // Act
        config.Id = "test-config";
        config.Name = "Test Configuration";
        config.ApiKey = "test-key";
        config.BaseUrl = "https://api.test.com";
        config.Model = "gpt-4";
        config.Temperature = 0.7f;
        config.MaxTokens = 100;
        config.Format = "json";
        config.Stream = true;

        // Assert
        config.Id.Should().Be("test-config");
        config.Name.Should().Be("Test Configuration");
        config.ApiKey.Should().Be("test-key");
        config.BaseUrl.Should().Be("https://api.test.com");
        config.Model.Should().Be("gpt-4");
        config.Temperature.Should().Be(0.7f);
        config.MaxTokens.Should().Be(100);
        config.Format.Should().Be("json");
        config.Stream.Should().BeTrue();
    }
}