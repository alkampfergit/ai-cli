using AiCli.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.Versioning;

namespace AiCli.Tests.Infrastructure;

[SupportedOSPlatform("windows")]
public class DpapiEncryptionServiceTests
{
    private readonly Mock<ILogger<DpapiEncryptionService>> _mockLogger;

    public DpapiEncryptionServiceTests()
    {
        _mockLogger = new Mock<ILogger<DpapiEncryptionService>>();
    }

    [Fact]
    public void Constructor_OnNonWindowsPlatform_ShouldThrowPlatformNotSupportedException()
    {
        // This test will only run on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            // Act & Assert
            var action = () => new DpapiEncryptionService(_mockLogger.Object);
            action.Should().Throw<PlatformNotSupportedException>()
                .WithMessage("DPAPI encryption is only supported on Windows");
        }
        else
        {
            // Skip this test on Windows - it's expected to work
            Assert.True(true, "Test skipped on Windows platform");
        }
    }

    [Fact]
    public void Constructor_OnWindowsPlatform_ShouldSucceed()
    {
        // This test will only run on Windows platforms
        if (OperatingSystem.IsWindows())
        {
            // Act
            var service = new DpapiEncryptionService(_mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
        }
        else
        {
            // Skip this test on non-Windows - constructor should throw
            Assert.True(true, "Test skipped on non-Windows platform");
        }
    }

    [Fact]
    public void Encrypt_WithValidString_ShouldReturnEncryptedString()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var plaintext = "test-api-key-12345";

        // Act
        var encrypted = service.Encrypt(plaintext);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plaintext);
        encrypted.Should().StartWith("DPAPI:");
    }

    [Fact]
    public void Encrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var plaintext = "";

        // Act
        var encrypted = service.Encrypt(plaintext);

        // Assert
        encrypted.Should().Be("");
    }

    [Fact]
    public void Encrypt_WithNullString_ShouldReturnNull()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        string? plaintext = null;

        // Act
        var encrypted = service.Encrypt(plaintext!);

        // Assert
        encrypted.Should().BeNull();
    }

    [Fact]
    public void Decrypt_WithValidEncryptedString_ShouldReturnOriginalString()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var plaintext = "test-api-key-12345";
        var encrypted = service.Encrypt(plaintext);

        // Act
        var decrypted = service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Decrypt_WithPlaintextString_ShouldReturnAsIs()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var plaintext = "test-api-key-12345";

        // Act
        var decrypted = service.Decrypt(plaintext);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void IsEncrypted_WithEncryptedString_ShouldReturnTrue()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var plaintext = "test-api-key-12345";
        var encrypted = service.Encrypt(plaintext);

        // Act
        var result = service.IsEncrypted(encrypted);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEncrypted_WithPlaintextString_ShouldReturnFalse()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var plaintext = "test-api-key-12345";

        // Act
        var result = service.IsEncrypted(plaintext);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ShouldPreserveOriginalValue()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);
        var originalValues = new[]
        {
            "simple-key",
            "sk-1234567890abcdef",
            "complex-key-with-special-chars!@#$%^&*()",
            "very-long-key-that-contains-many-characters-and-might-test-encryption-boundaries-1234567890",
            "unicode-test-αβγδε-日本語-🔐"
        };

        foreach (var original in originalValues)
        {
            // Act
            var encrypted = service.Encrypt(original);
            var decrypted = service.Decrypt(encrypted);

            // Assert
            decrypted.Should().Be(original, $"Round-trip failed for value: {original}");
            encrypted.Should().NotBe(original, $"Encryption should change the value: {original}");
        }
    }

    [Fact]
    public void Encrypt_WithDifferentServiceInstances_ShouldProduceSameDecryptableResults()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var plaintext = "test-api-key-12345";
        var service1 = new DpapiEncryptionService(_mockLogger.Object);
        var service2 = new DpapiEncryptionService(_mockLogger.Object);

        // Act
        var encrypted1 = service1.Encrypt(plaintext);
        var decrypted1 = service2.Decrypt(encrypted1);

        var encrypted2 = service2.Encrypt(plaintext);
        var decrypted2 = service1.Decrypt(encrypted2);

        // Assert
        decrypted1.Should().Be(plaintext);
        decrypted2.Should().Be(plaintext);
    }

    [Fact]
    public void IsEncrypted_WithDifferentPrefixes_ShouldDistinguishCorrectly()
    {
        // Skip test on non-Windows platforms
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Test skipped on non-Windows platform");
            return;
        }

        // Arrange
        var service = new DpapiEncryptionService(_mockLogger.Object);

        // Act & Assert
        service.IsEncrypted("DPAPI:someencryptedvalue").Should().BeTrue();
        service.IsEncrypted("ENC:someencryptedvalue").Should().BeFalse();
        service.IsEncrypted("plaintext").Should().BeFalse();
        service.IsEncrypted("").Should().BeFalse();
        service.IsEncrypted(null!).Should().BeFalse();
    }
}