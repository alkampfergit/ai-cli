using AiCli.Application;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AiCli.Infrastructure;

/// <summary>
/// Windows DPAPI-based encryption service for sensitive settings
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DpapiEncryptionService : IEncryptionService
{
    private readonly ILogger<DpapiEncryptionService> _logger;
    private const string EncryptedPrefix = "DPAPI:";

    /// <summary>
    /// Initializes a new instance of the DpapiEncryptionService class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public DpapiEncryptionService(ILogger<DpapiEncryptionService> logger)
    {
        _logger = logger;

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI encryption is only supported on Windows");
        }
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
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            // Use DPAPI with CurrentUser scope for user-specific encryption
            var encryptedBytes = ProtectedData.Protect(
                plaintextBytes,
                optionalEntropy: GetEntropy(),
                scope: DataProtectionScope.CurrentUser);

            var base64 = Convert.ToBase64String(encryptedBytes);
            return EncryptedPrefix + base64;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt string using DPAPI");
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
            var encryptedBytes = Convert.FromBase64String(base64);

            // Use DPAPI to decrypt with the same entropy
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: GetEntropy(),
                scope: DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt string using DPAPI");
            throw;
        }
    }

    /// <inheritdoc/>
    public bool IsEncrypted(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix);
    }

    /// <summary>
    /// Gets additional entropy for DPAPI encryption
    /// </summary>
    private byte[] GetEntropy()
    {
        // Use application-specific entropy for additional security
        var appEntropy = "ai-cli-dpapi-entropy-v1";
        return Encoding.UTF8.GetBytes(appEntropy);
    }
}
