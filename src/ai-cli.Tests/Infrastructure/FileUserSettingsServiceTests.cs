using AiCli.Application;
using AiCli.Infrastructure;
using AiCli.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AiCli.Tests.Infrastructure;

public class FileUserSettingsServiceTests : IDisposable
{
    private readonly Mock<ILogger<FileUserSettingsService>> _mockLogger;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly string _tempSettingsPath;
    private readonly FileUserSettingsService _fileUserSettingsService;

    public FileUserSettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileUserSettingsService>>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _tempSettingsPath = Path.GetTempFileName();

        // Set up default encryption service behavior (generic for any encryption implementation)
        _mockEncryptionService.Setup(x => x.IsEncrypted(It.IsAny<string>())).Returns((string s) => s?.StartsWith("ENCRYPTED:") == true);
        _mockEncryptionService.Setup(x => x.Encrypt(It.IsAny<string>())).Returns<string>(s => string.IsNullOrEmpty(s) ? s : "ENCRYPTED:" + s);
        _mockEncryptionService.Setup(x => x.Decrypt(It.IsAny<string>())).Returns<string>(s =>
            s?.StartsWith("ENCRYPTED:") == true ? s.Substring("ENCRYPTED:".Length) : (s ?? String.Empty));

        _fileUserSettingsService = new FileUserSettingsService(_tempSettingsPath, _mockLogger.Object, _mockEncryptionService.Object);
    }

    [Fact]
    public void Load_WithMissingFile_ShouldReturnDefaults()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        var service = new FileUserSettingsService(nonExistentPath, _mockLogger.Object, _mockEncryptionService.Object);

        // Act
        var settings = service.Load();

        // Assert
        settings.ModelConfigurations.Should().BeEmpty();
        settings.DefaultModelConfigurationId.Should().Be(string.Empty);
        settings.RefreshInterval.Should().Be(30);
    }

    [Fact]
    public void ResetToDefault_ShouldReturnDefaultsAndSaveToFile()
    {
        // Act
        var settings = _fileUserSettingsService.ResetToDefault();

        // Assert
        settings.ModelConfigurations.Should().BeEmpty();
        settings.DefaultModelConfigurationId.Should().Be(string.Empty);
        settings.RefreshInterval.Should().Be(30);

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
                    Type = ModelType.Generic,
                    ApiKey = "test-api-key",
                    BaseUrl = "https://api.test.com",
                    Model = "gpt-4",
                    Temperature = 0.7f,
                    MaxTokens = 150,
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
              "type": 0,
              "apiKey": "loaded-api-key",
              "baseUrl": "https://api.loaded.com",
              "model": "gpt-4",
              "temperature": 0.6,
              "maxTokens": 200,
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
        settings.ModelConfigurations.Should().BeEmpty();
        settings.DefaultModelConfigurationId.Should().Be(string.Empty);
        settings.RefreshInterval.Should().Be(30);
    }

    [Fact]
    public void Load_WithEmptyFile_ShouldReturnDefaults()
    {
        // Arrange
        File.WriteAllText(_tempSettingsPath, "");

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().BeEmpty();
        settings.DefaultModelConfigurationId.Should().Be(string.Empty);
        settings.RefreshInterval.Should().Be(30);
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
        config.Format.Should().Be("text"); // Default
        config.Stream.Should().BeFalse(); // Default
    }

    [Fact]
    public void Load_WithEmptyModelConfigurations_ShouldKeepEmptyConfigurations()
    {
        // Arrange
        var jsonContent = """
        {
          "modelConfigurations": [],
          "defaultModelConfigurationId": "",
          "refreshInterval": 60
        }
        """;
        File.WriteAllText(_tempSettingsPath, jsonContent);

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        settings.ModelConfigurations.Should().BeEmpty();
        settings.DefaultModelConfigurationId.Should().Be(string.Empty);
        settings.RefreshInterval.Should().Be(60);
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
        var service = new FileUserSettingsService(settingsPath, _mockLogger.Object, _mockEncryptionService.Object);
        var settings = UserSettings.CreateDefault();

        // Act
        service.Save(settings);

        // Assert
        Directory.Exists(tempDir).Should().BeTrue();
        File.Exists(settingsPath).Should().BeTrue();

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void Save_WithEncryptedProperties_ShouldEncryptApiKeys()
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
                    Type = ModelType.Generic,
                    ApiKey = "secret-api-key-123",
                    Model = "gpt-4"
                }
            },
            DefaultModelConfigurationId = "test-config",
            RefreshInterval = 30
        };

        _mockEncryptionService.Setup(x => x.IsEncrypted("secret-api-key-123")).Returns(false);
        _mockEncryptionService.Setup(x => x.Encrypt("secret-api-key-123")).Returns("ENCRYPTED:secret-api-key-123");

        // Act
        _fileUserSettingsService.Save(settings);

        // Assert
        _mockEncryptionService.Verify(x => x.Encrypt("secret-api-key-123"), Times.Once);

        // Verify the file contains encrypted content
        var content = File.ReadAllText(_tempSettingsPath);
        content.Should().Contain("ENCRYPTED:secret-api-key-123");
        // The original settings object should remain unchanged
        settings.ModelConfigurations.First().ApiKey.Should().Be("secret-api-key-123");
    }

    [Fact]
    public void Load_WithEncryptedProperties_ShouldDecryptApiKeys()
    {
        // Arrange
        var jsonContent = """
        {
          "modelConfigurations": [
            {
              "id": "test-config",
              "name": "Test Configuration",
              "apiKey": "ENC:encrypted-secret-api-key-123",
              "model": "gpt-4"
            }
          ],
          "defaultModelConfigurationId": "test-config",
          "refreshInterval": 30
        }
        """;
        File.WriteAllText(_tempSettingsPath, jsonContent);

        _mockEncryptionService.Setup(x => x.IsEncrypted("ENC:encrypted-secret-api-key-123")).Returns(true);
        _mockEncryptionService.Setup(x => x.Decrypt("ENC:encrypted-secret-api-key-123")).Returns("secret-api-key-123");

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        _mockEncryptionService.Verify(x => x.Decrypt("ENC:encrypted-secret-api-key-123"), Times.Once);

        var config = settings.ModelConfigurations.First();
        config.ApiKey.Should().Be("secret-api-key-123");
    }

    [Fact]
    public void Load_WithMixedEncryptedAndPlainProperties_ShouldHandleBothCorrectly()
    {
        // Arrange
        var jsonContent = """
        {
          "modelConfigurations": [
            {
              "id": "encrypted-config",
              "name": "Encrypted Configuration",
              "apiKey": "ENC:encrypted-secret-api-key-123",
              "model": "gpt-4"
            },
            {
              "id": "plain-config",
              "name": "Plain Configuration",
              "apiKey": "plain-api-key-456",
              "model": "gpt-3.5-turbo"
            }
          ],
          "defaultModelConfigurationId": "encrypted-config",
          "refreshInterval": 30
        }
        """;
        File.WriteAllText(_tempSettingsPath, jsonContent);

        _mockEncryptionService.Setup(x => x.IsEncrypted("ENC:encrypted-secret-api-key-123")).Returns(true);
        _mockEncryptionService.Setup(x => x.IsEncrypted("plain-api-key-456")).Returns(false);
        _mockEncryptionService.Setup(x => x.Decrypt("ENC:encrypted-secret-api-key-123")).Returns("secret-api-key-123");

        // Act
        var settings = _fileUserSettingsService.Load();

        // Assert
        _mockEncryptionService.Verify(x => x.Decrypt("ENC:encrypted-secret-api-key-123"), Times.Once);
        _mockEncryptionService.Verify(x => x.Decrypt("plain-api-key-456"), Times.Never);

        var encryptedConfig = settings.ModelConfigurations.First(c => c.Id == "encrypted-config");
        var plainConfig = settings.ModelConfigurations.First(c => c.Id == "plain-config");

        encryptedConfig.ApiKey.Should().Be("secret-api-key-123");
        plainConfig.ApiKey.Should().Be("plain-api-key-456");
    }

    public void Dispose()
    {
        if (File.Exists(_tempSettingsPath))
        {
            File.Delete(_tempSettingsPath);
        }
    }
}