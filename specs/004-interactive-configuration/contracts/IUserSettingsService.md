# Contract: `IUserSettingsService`

## Type

**Namespace**: `AiCli.Application`
**File**: `src/ai-cli/Application/IUserSettingsService.cs`
**Implementation**: `AiCli.Infrastructure.FileUserSettingsService`
(`src/ai-cli/Infrastructure/FileUserSettingsService.cs`)

```csharp
public interface IUserSettingsService
{
    UserSettings Load();
    void Save(UserSettings settings);
    UserSettings ResetToDefault();
}
```

## Operations

### `UserSettings Load()`

Reads the on-disk settings file and returns a fully decrypted, validated
`UserSettings` instance suitable for immediate use.

**Behavior**:
- Missing file → returns `UserSettings.CreateDefault()` (no exception).
- Empty file → returns `UserSettings.CreateDefault()` (warning logged).
- Invalid JSON → returns `UserSettings.CreateDefault()` (error logged).
- Successful parse → applies defaults to missing required fields,
  self-heals a stale `DefaultModelConfigurationId`, then decrypts every
  `[EncryptedSetting]` string property on every `ModelConfiguration`.
- Decryption failure on a specific property → the property is left in
  encrypted form; the failure is logged at Error level. The remaining
  fields are still returned.

**Throws**: Never throws on a corrupted file. May throw on truly
exceptional I/O conditions (e.g., a path the OS refuses to read for
non-recoverable reasons) but those paths are caught at higher layers
and surface as exit code 3 / 4.

### `void Save(UserSettings settings)`

Persists `settings` to disk, encrypting `[EncryptedSetting]` properties
along the way.

**Behavior**:
- Creates the parent directory if it does not exist (logged).
- JSON-clones the input so the caller's in-memory object is not mutated.
- Walks every `ModelConfiguration` and encrypts each `[EncryptedSetting]`
  string property that is not already encrypted (idempotent).
- Serialises with `WriteIndented = true` and camelCase property naming.
- Writes the file via `File.WriteAllText`.
- On Unix-family hosts, sets file mode to `UserRead | UserWrite` (600).
  Permission failure is logged as a warning, not thrown.

**Throws**: Rethrows on I/O failures (the caller in `Program.cs` maps
this to `ExitCodes.FileError`).

### `UserSettings ResetToDefault()`

Convenience method: creates a default `UserSettings` via
`UserSettings.CreateDefault()`, calls `Save(...)`, and returns it.
Used in tests; not exposed through the interactive menu.

## Wire format

The on-disk file is JSON with camelCase property names. Example after a
typical save on Linux:

```json
{
  "modelConfigurations": [
    {
      "id": "openai-prod",
      "name": "OpenAI Production",
      "type": 0,
      "apiKey": "ENC:base64-IV-and-ciphertext-bytes",
      "baseUrl": "https://api.openai.com/v1",
      "model": "gpt-4o-mini",
      "temperature": 0.7,
      "maxTokens": null,
      "format": "text",
      "stream": false
    }
  ],
  "defaultModelConfigurationId": "openai-prod",
  "refreshInterval": 30
}
```

On Windows the `apiKey` value would be `DPAPI:base64-protected-bytes`.

## Construction

`FileUserSettingsService` is registered as a singleton bound to a known
settings path. The constructor signature is:

```csharp
public FileUserSettingsService(
    string settingsFilePath,
    ILogger<FileUserSettingsService> logger,
    IEncryptionService encryptionService)
```

The settings path is resolved by
`SettingsPathProvider.GetDefaultSettingsPath()` (with an opt-in custom
path via `GetSettingsPath(string?)`).

## Invariants

1. After `Save`, the in-memory `UserSettings` reference passed in by the
   caller MUST be unchanged. (`FileUserSettingsService` clones before
   encrypting.)
2. After `Load`, every `[EncryptedSetting]` string property is in
   plaintext form in memory (unless decryption failed and was logged).
3. After `Load`, every `ModelConfiguration` has non-null `Id`, `Name`,
   `Model`, `Format` — defaults are substituted when the JSON omitted
   them.
4. After `Load`, `DefaultModelConfigurationId` is either empty (no
   configurations) or matches an existing `ModelConfiguration.Id`.
5. The on-disk file never contains a plaintext copy of any
   `[EncryptedSetting]` property (encryption is unconditional on save
   when the value is non-empty).

## Test coverage references

- `src/ai-cli.Tests/Infrastructure/FileUserSettingsServiceTests.cs`
- `src/ai-cli.Tests/Models/UserSettingsTests.cs`
- `src/ai-cli.Tests/Configuration/SettingsPathProviderTests.cs`
