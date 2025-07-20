namespace AiCli.Application;

/// <summary>
/// Interface for encryption and decryption of sensitive settings
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext string
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt</param>
    /// <returns>The encrypted string</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts an encrypted string
    /// </summary>
    /// <param name="ciphertext">The encrypted string to decrypt</param>
    /// <returns>The decrypted plaintext string</returns>
    string Decrypt(string ciphertext);

    /// <summary>
    /// Checks if a string appears to be encrypted
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string appears to be encrypted, false otherwise</returns>
    bool IsEncrypted(string value);
}