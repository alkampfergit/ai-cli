using AiCli.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AiCli.Tests.Infrastructure;

public class AesEncryptionServiceTests
{
    private readonly Mock<ILogger<AesEncryptionService>> _mockLogger;
    private readonly AesEncryptionService _encryptionService;

    public AesEncryptionServiceTests()
    {
        _mockLogger = new Mock<ILogger<AesEncryptionService>>();
        _encryptionService = new AesEncryptionService(_mockLogger.Object);
    }

    [Fact]
    public void Encrypt_WithValidString_ShouldReturnEncryptedString()
    {
        // Arrange
        var plaintext = "test-api-key-12345";

        // Act
        var encrypted = _encryptionService.Encrypt(plaintext);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plaintext);
        encrypted.Should().StartWith("ENC:");
    }

    [Fact]
    public void Encrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var plaintext = "";

        // Act
        var encrypted = _encryptionService.Encrypt(plaintext);

        // Assert
        encrypted.Should().Be("");
    }

    [Fact]
    public void Encrypt_WithNullString_ShouldReturnNull()
    {
        // Arrange
        string? plaintext = null;

        // Act
        var encrypted = _encryptionService.Encrypt(plaintext!);

        // Assert
        encrypted.Should().BeNull();
    }

    [Fact]
    public void Decrypt_WithValidEncryptedString_ShouldReturnOriginalString()
    {
        // Arrange
        var plaintext = "test-api-key-12345";
        var encrypted = _encryptionService.Encrypt(plaintext);

        // Act
        var decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Decrypt_WithPlaintextString_ShouldReturnAsIs()
    {
        // Arrange
        var plaintext = "test-api-key-12345";

        // Act
        var decrypted = _encryptionService.Decrypt(plaintext);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Decrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var plaintext = "";

        // Act
        var decrypted = _encryptionService.Decrypt(plaintext);

        // Assert
        decrypted.Should().Be("");
    }

    [Fact]
    public void Decrypt_WithNullString_ShouldReturnNull()
    {
        // Arrange
        string? ciphertext = null;

        // Act
        var decrypted = _encryptionService.Decrypt(ciphertext!);

        // Assert
        decrypted.Should().BeNull();
    }

    [Fact]
    public void IsEncrypted_WithEncryptedString_ShouldReturnTrue()
    {
        // Arrange
        var plaintext = "test-api-key-12345";
        var encrypted = _encryptionService.Encrypt(plaintext);

        // Act
        var result = _encryptionService.IsEncrypted(encrypted);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEncrypted_WithPlaintextString_ShouldReturnFalse()
    {
        // Arrange
        var plaintext = "test-api-key-12345";

        // Act
        var result = _encryptionService.IsEncrypted(plaintext);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEncrypted_WithEmptyString_ShouldReturnFalse()
    {
        // Arrange
        var plaintext = "";

        // Act
        var result = _encryptionService.IsEncrypted(plaintext);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEncrypted_WithNullString_ShouldReturnFalse()
    {
        // Arrange
        string? plaintext = null;

        // Act
        var result = _encryptionService.IsEncrypted(plaintext!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ShouldPreserveOriginalValue()
    {
        // Arrange
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
            var encrypted = _encryptionService.Encrypt(original);
            var decrypted = _encryptionService.Decrypt(encrypted);

            // Assert
            decrypted.Should().Be(original, $"Round-trip failed for value: {original}");
            encrypted.Should().NotBe(original, $"Encryption should change the value: {original}");
        }
    }

    [Fact]
    public void Encrypt_SameValueMultipleTimes_ShouldProduceDifferentResults()
    {
        // Arrange
        var plaintext = "test-api-key-12345";

        // Act
        var encrypted1 = _encryptionService.Encrypt(plaintext);
        var encrypted2 = _encryptionService.Encrypt(plaintext);
        var encrypted3 = _encryptionService.Encrypt(plaintext);

        // Assert
        encrypted1.Should().NotBe(encrypted2);
        encrypted2.Should().NotBe(encrypted3);
        encrypted1.Should().NotBe(encrypted3);

        // But all should decrypt to the same original value
        _encryptionService.Decrypt(encrypted1).Should().Be(plaintext);
        _encryptionService.Decrypt(encrypted2).Should().Be(plaintext);
        _encryptionService.Decrypt(encrypted3).Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_WithDifferentServiceInstances_ShouldProduceSameDecryptableResults()
    {
        // Arrange
        var plaintext = "test-api-key-12345";
        var service1 = new AesEncryptionService(_mockLogger.Object);
        var service2 = new AesEncryptionService(_mockLogger.Object);

        // Act
        var encrypted1 = service1.Encrypt(plaintext);
        var decrypted1 = service2.Decrypt(encrypted1);

        var encrypted2 = service2.Encrypt(plaintext);
        var decrypted2 = service1.Decrypt(encrypted2);

        // Assert
        decrypted1.Should().Be(plaintext);
        decrypted2.Should().Be(plaintext);
    }
}