# Settings Encryption

The AI CLI automatically encrypts sensitive settings when saving them to the configuration file.

## Encrypted Properties

The following properties are automatically encrypted when saved:

- `ModelConfiguration.ApiKey` - API keys for LLM providers

## How It Works

1. **Saving Settings**: When the application saves settings, properties marked with `[EncryptedSetting]` are automatically encrypted using AES-256 encryption.

2. **Loading Settings**: When loading settings, encrypted properties are automatically decrypted back to their original values.

3. **Encryption Key**: The encryption key is derived from machine-specific information (machine name, username, OS platform) to ensure that settings encrypted on one machine can only be decrypted on the same machine.

## Example

When you save a configuration with an API key:

```json
{
  "modelConfigurations": [
    {
      "id": "openai-config",
      "name": "OpenAI Configuration",
      "apiKey": "sk-1234567890abcdef...",
      "model": "gpt-3.5-turbo"
    }
  ]
}
```

The saved file will look like this:

```json
{
  "modelConfigurations": [
    {
      "id": "openai-config",
      "name": "OpenAI Configuration", 
      "apiKey": "ENC:AbCdEfGhIjKlMnOpQrStUvWxYz0123456789...",
      "model": "gpt-3.5-turbo"
    }
  ]
}
```

## Security Notes

- Encryption is transparent to the user - you work with plain text API keys in your code
- The encryption key is machine-specific, so settings cannot be shared between different machines
- The `ENC:` prefix indicates an encrypted value
- Only string properties marked with `[EncryptedSetting]` are encrypted
- If decryption fails, the application will log an error but continue to work with the encrypted value

## Platform Compatibility

The encryption works across all supported platforms (Windows, macOS, Linux) and uses the .NET AES implementation for consistency and security.