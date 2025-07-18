using AiCli.Application;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace AiCli.Infrastructure;

/// <summary>
/// AES-based encryption service for sensitive settings
/// </summary>
internal sealed class AesEncryptionService : IEncryptionService
{
    private readonly ILogger<AesEncryptionService> _logger;
    private readonly byte[] _key;
    private const string EncryptedPrefix = "ENC:";

    /// <summary>
    /// Initializes a new instance of the AesEncryptionService class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public AesEncryptionService(ILogger<AesEncryptionService> logger)
    {
        _logger = logger;
        _key = GetOrCreateKey();
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Combine IV and ciphertext
            var combined = new byte[aes.IV.Length + ciphertextBytes.Length];
            Array.Copy(aes.IV, 0, combined, 0, aes.IV.Length);
            Array.Copy(ciphertextBytes, 0, combined, aes.IV.Length, ciphertextBytes.Length);

            var base64 = Convert.ToBase64String(combined);
            return EncryptedPrefix + base64;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt string");
            throw;
        }
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext) || !IsEncrypted(ciphertext))
        {
            return ciphertext;
        }

        try
        {
            var base64 = ciphertext.Substring(EncryptedPrefix.Length);
            var combined = Convert.FromBase64String(base64);

            using var aes = Aes.Create();
            aes.Key = _key;

            // Extract IV and ciphertext
            var iv = new byte[aes.IV.Length];
            var ciphertextBytes = new byte[combined.Length - aes.IV.Length];
            Array.Copy(combined, 0, iv, 0, iv.Length);
            Array.Copy(combined, iv.Length, ciphertextBytes, 0, ciphertextBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt string");
            throw;
        }
    }

    /// <inheritdoc/>
    public bool IsEncrypted(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix);
    }

    /// <summary>
    /// Gets or creates an encryption key for this machine/user
    /// </summary>
    private byte[] GetOrCreateKey()
    {
        try
        {
            // Use machine-specific entropy to derive a consistent key
            var entropy = GetMachineEntropy();

            // Use PBKDF2 to derive a 256-bit key from the entropy
            using var pbkdf2 = new Rfc2898DeriveBytes(entropy, entropy, 10000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32); // 256 bits
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate encryption key");
            throw;
        }
    }

    /// <summary>
    /// Gets machine-specific entropy for key derivation
    /// </summary>
    private byte[] GetMachineEntropy()
    {
        var machineInfo = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion.Platform}";

        // Add additional entropy sources that are consistent across instances
        var additionalEntropy = new List<string>
        {
            Environment.ProcessorCount.ToString(),
            "ai-cli-encryption-service" // Fixed string for consistency
        };

        var combinedEntropy = machineInfo + "|" + string.Join("|", additionalEntropy);
        return Encoding.UTF8.GetBytes(combinedEntropy);
    }
}