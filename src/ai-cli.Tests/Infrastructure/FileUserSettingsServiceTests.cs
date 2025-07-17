using AiCli.Infrastructure;
using AiCli.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AiCli.Tests.Infrastructure;

public class FileUserSettingsServiceTests : IDisposable
{
    private readonly Mock<ILogger<FileUserSettingsService>> _mockLogger;
    private readonly string _tempSettingsPath;
    private readonly FileUserSettingsService _fileUserSettingsService;

    public FileUserSettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileUserSettingsService>>();
        _tempSettingsPath = Path.GetTempFileName();
        _fileUserSettingsService = new FileUserSettingsService(_tempSettingsPath, _mockLogger.Object);
    }

    [Fact]
    public void Load_WithMissingFile_ShouldReturnDefaults()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        var service = new FileUserSettingsService(nonExistentPath, _mockLogger.Object);

        // Act
        var settings = service.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("default");
        settings.RefreshInterval.Should().Be(30);
        
        var defaultConfig = settings.ModelConfigurations.First();
        defaultConfig.ApiKey.Should().BeNull();
        defaultConfig.BaseUrl.Should().BeNull();
        defaultConfig.Model.Should().Be("gpt-3.5-turbo");
        defaultConfig.Temperature.Should().Be(1.0f);
        defaultConfig.MaxTokens.Should().BeNull();
        defaultConfig.TopP.Should().BeNull();
        defaultConfig.Format.Should().Be("text");
        defaultConfig.Stream.Should().BeFalse();
    }

    [Fact]
    public void ResetToDefault_ShouldReturnDefaultsAndSaveToFile()
    {
        // Act
        var settings = _fileUserSettingsService.ResetToDefault();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("default");
        settings.RefreshInterval.Should().Be(30);
        
        var defaultConfig = settings.ModelConfigurations.First();
        defaultConfig.ApiKey.Should().BeNull();
        defaultConfig.BaseUrl.Should().BeNull();
        defaultConfig.Model.Should().Be("gpt-3.5-turbo");
        defaultConfig.Temperature.Should().Be(1.0f);
        defaultConfig.MaxTokens.Should().BeNull();
        defaultConfig.TopP.Should().BeNull();
        defaultConfig.Format.Should().Be("text");
        defaultConfig.Stream.Should().BeFalse();

        // Verify file was created
        File.Exists(_tempSettingsPath).Should().BeTrue();
    }

    [Fact]
    public void Save_ShouldCreateFileWithCorrectContent()
    {
        // Arrange
        var settings = new UserSettings
        {
            ModelConfigurations = new List<ModelConfiguration>
            {
                new ModelConfiguration
                {
                    Id = "test-config",
                    Name = "Test Configuration",
                    ApiKey = "test-api-key",
                    BaseUrl = "https://api.test.com",
                    Model = "gpt-4",
                    Temperature = 0.7f,
                    MaxTokens = 150,
                    TopP = 0.8f,
                    Format = "json",
                    Stream = true
                }
            },
            DefaultModelConfigurationId = "test-config",
            RefreshInterval = 45
        };

        // Act
        _fileUserSettingsService.Save(settings);

        // Assert
        File.Exists(_tempSettingsPath).Should().BeTrue();
        var content = File.ReadAllText(_tempSettingsPath);
        content.Should().Contain("test-api-key");
        content.Should().Contain("https://api.test.com");
        content.Should().Contain("gpt-4");
        content.Should().Contain("0.7");
        content.Should().Contain("150");
        content.Should().Contain("0.8");
        content.Should().Contain("json");
        content.Should().Contain("true");
        content.Should().Contain("45");
        content.Should().Contain("test-config");
    }

    [Fact]
    public void Load_WithValidFile_ShouldLoadSettingsCorrectly()
    {
        // Arrange
        var jsonContent = """
        {
          "modelConfigurations": [
            {
              "id": "loaded-config",
              "name": "Loaded Configuration",
              "apiKey": "loaded-api-key",
              "baseUrl": "https://api.loaded.com",
              "model": "gpt-4",
              "temperature": 0.6,
              "maxTokens": 200,
              "topP": 0.9,
              "format": "json",
              "stream": true
            }
          ],
          "defaultModelConfigurationId": "loaded-config",
          "refreshInterval": 60
        }
        """;
        File.WriteAllText(_tempSettingsPath, jsonContent);

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("loaded-config");
        settings.RefreshInterval.Should().Be(60);
        
        var config = settings.ModelConfigurations.First();
        config.Id.Should().Be("loaded-config");
        config.Name.Should().Be("Loaded Configuration");
        config.ApiKey.Should().Be("loaded-api-key");
        config.BaseUrl.Should().Be("https://api.loaded.com");
        config.Model.Should().Be("gpt-4");
        config.Temperature.Should().Be(0.6f);
        config.MaxTokens.Should().Be(200);
        config.TopP.Should().Be(0.9f);
        config.Format.Should().Be("json");
        config.Stream.Should().BeTrue();
    }

    [Fact]
    public void Load_WithInvalidJson_ShouldReturnDefaults()
    {
        // Arrange
        File.WriteAllText(_tempSettingsPath, "invalid json content");

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("default");
        settings.RefreshInterval.Should().Be(30);
        
        var defaultConfig = settings.ModelConfigurations.First();
        defaultConfig.Model.Should().Be("gpt-3.5-turbo");
        defaultConfig.Format.Should().Be("text");
    }

    [Fact]
    public void Load_WithEmptyFile_ShouldReturnDefaults()
    {
        // Arrange
        File.WriteAllText(_tempSettingsPath, "");

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("default");
        settings.RefreshInterval.Should().Be(30);
        
        var defaultConfig = settings.ModelConfigurations.First();
        defaultConfig.Model.Should().Be("gpt-3.5-turbo");
        defaultConfig.Format.Should().Be("text");
    }

    [Fact]
    public void Load_WithPartialJson_ShouldLoadAvailableSettingsAndKeepDefaultsForMissing()
    {
        // Arrange
        var partialJsonContent = """
        {
          "modelConfigurations": [
            {
              "id": "partial-config",
              "apiKey": "partial-api-key",
              "model": "gpt-4",
              "temperature": 0.5
            }
          ],
          "defaultModelConfigurationId": "partial-config"
        }
        """;
        File.WriteAllText(_tempSettingsPath, partialJsonContent);

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("partial-config");
        settings.RefreshInterval.Should().Be(30); // Default

        var config = settings.ModelConfigurations.First();
        config.Id.Should().Be("partial-config");
        config.ApiKey.Should().Be("partial-api-key");
        config.Model.Should().Be("gpt-4");
        config.Temperature.Should().Be(0.5f);
        config.BaseUrl.Should().BeNull(); // Default
        config.MaxTokens.Should().BeNull(); // Default
        config.TopP.Should().BeNull(); // Default
        config.Format.Should().Be("text"); // Default
        config.Stream.Should().BeFalse(); // Default
    }

    [Fact]
    public void Load_WithEmptyModelConfigurations_ShouldCreateDefaultConfiguration()
    {
        // Arrange
        var jsonContent = """
        {
          "modelConfigurations": [],
          "defaultModelConfigurationId": "default",
          "refreshInterval": 60
        }
        """;
        File.WriteAllText(_tempSettingsPath, jsonContent);

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("default");
        
        var config = settings.ModelConfigurations.First();
        config.Id.Should().Be("default");
        config.Model.Should().Be("gpt-3.5-turbo");
    }

    [Fact]
    public void Load_WithInvalidDefaultModelConfigurationId_ShouldFixDefaultId()
    {
        // Arrange
        var jsonContent = """
        {
          "modelConfigurations": [
            {
              "id": "valid-config",
              "name": "Valid Configuration",
              "model": "gpt-4"
            }
          ],
          "defaultModelConfigurationId": "invalid-id",
          "refreshInterval": 60
        }
        """;
        File.WriteAllText(_tempSettingsPath, jsonContent);

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().HaveCount(1);
        settings.DefaultModelConfigurationId.Should().Be("valid-config");
        
        var config = settings.ModelConfigurations.First();
        config.Id.Should().Be("valid-config");
    }

    [Fact]
    public void Save_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var settingsPath = Path.Combine(tempDir, "settings.json");
        var service = new FileUserSettingsService(settingsPath, _mockLogger.Object);
        var settings = UserSettings.CreateDefault();

        // Act
        service.Save(settings);

        // Assert
        Directory.Exists(tempDir).Should().BeTrue();
        File.Exists(settingsPath).Should().BeTrue();

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    public void Dispose()
    {
        if (File.Exists(_tempSettingsPath))
        {
            File.Delete(_tempSettingsPath);
        }
    }
}