# Contract: `IEncryptionService`

## Type

**Namespace**: `AiCli.Application`
**File**: `src/ai-cli/Application/IEncryptionService.cs`
**Implementations**:
- `AiCli.Infrastructure.DpapiEncryptionService`
  (`src/ai-cli/Infrastructure/DpapiEncryptionService.cs`) — Windows only
- `AiCli.Infrastructure.AesEncryptionService`
  (`src/ai-cli/Infrastructure/AesEncryptionService.cs`) — all other platforms

```csharp
public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    bool IsEncrypted(string value);
}
```

## Selection

`Program.cs` selects the implementation at DI registration time:

```csharp
if (OperatingSystem.IsWindows())
    services.AddSingleton<IEncryptionService, DpapiEncryptionService>();
else
    services.AddSingleton<IEncryptionService, AesEncryptionService>();
```

This selection happens both in `HandleConfigModeAsync` (for the
interactive menu) and in `ConfigureServices` (for the prompt path).

## Operation semantics

### `string Encrypt(string plaintext)`

- Null or empty input is returned unchanged.
- For non-empty input, the implementation produces a base64-encoded
  ciphertext blob prefixed by its scheme marker.

| Implementation | Algorithm | Output prefix | IV/entropy |
|----------------|-----------|---------------|------------|
| DPAPI | Windows DPAPI via `ProtectedData.Protect`, `DataProtectionScope.CurrentUser` | `DPAPI:` | Static application entropy `ai-cli-dpapi-entropy-v1` |
| AES | AES with a fresh random IV per call; IV is prepended to the ciphertext bytes before base64 | `ENC:` | 256-bit key derived via PBKDF2 (SHA-256, 10000 iterations) from `MachineName|UserName|OSPlatform|ProcessorCount|ai-cli-encryption-service` |

### `string Decrypt(string ciphertext)`

- Null/empty/non-encrypted input is returned unchanged (the
  `IsEncrypted` check gates the actual decryption call).
- For input that matches the implementation's prefix:
  - **DPAPI**: strips the `DPAPI:` prefix, base64-decodes, and calls
    `ProtectedData.Unprotect` with the same entropy and scope.
  - **AES**: strips the `ENC:` prefix, base64-decodes, splits the IV
    from the ciphertext (the first `Aes.IV.Length` bytes), and decrypts
    with the PBKDF2-derived key.

Decryption errors are logged at Error level and rethrown by the
implementation. `FileUserSettingsService` catches the throw, logs it, and
leaves the affected property in its encrypted form so other settings
remain usable.

### `bool IsEncrypted(string value)`

Returns `true` when the value is non-null/non-empty and starts with the
implementation's prefix:

| Implementation | True iff |
|----------------|----------|
| DPAPI | `value.StartsWith("DPAPI:")` |
| AES   | `value.StartsWith("ENC:")` |

**Note**: An implementation only recognises its own prefix. If a settings
file is moved between platforms, the prefixes will not be recognised by
the other implementation and the values will be returned as-is (effectively
opaque ciphertext from the perspective of the current platform).

## Invariants

1. `Encrypt` is non-deterministic for non-trivial input on both
   implementations (AES uses a fresh IV; DPAPI mixes per-call randomness
   into its ciphertext). The same plaintext encrypted twice yields two
   different ciphertexts. (`AesEncryptionServiceTests` asserts this
   explicitly.)
2. `Decrypt(Encrypt(x)) == x` for all reasonable strings (ASCII, special
   chars, unicode, long values). (`AesEncryptionServiceTests`,
   `DpapiEncryptionServiceTests`.)
3. `Encrypt("") == ""` and `Encrypt(null) == null`.
4. `IsEncrypted("")` and `IsEncrypted(null)` are both `false`.
5. `Decrypt(value_already_in_plaintext) == value_already_in_plaintext`
   (the `IsEncrypted` check short-circuits the call).
6. Instantiating `DpapiEncryptionService` on a non-Windows host throws
   `PlatformNotSupportedException`.

## Threading

Both implementations are stateless except for an immutable derived key
(AES) or no state at all (DPAPI). They are safe to register as
singletons and call from multiple threads.

## Test coverage references

- `src/ai-cli.Tests/Infrastructure/AesEncryptionServiceTests.cs`
  - Round-trip with ASCII, special chars, and unicode strings
  - Cross-instance compatibility (key is derived deterministically)
  - Non-determinism of `Encrypt` (different ciphertext each call)
  - Null/empty handling for all three methods
- `src/ai-cli.Tests/Infrastructure/DpapiEncryptionServiceTests.cs`
  - All tests self-skip on non-Windows hosts
  - `Constructor_OnNonWindowsPlatform_ShouldThrowPlatformNotSupportedException`
  - `IsEncrypted_WithDifferentPrefixes_ShouldDistinguishCorrectly`
- `src/ai-cli.Tests/Attributes/EncryptedSettingAttributeTests.cs`
  - Verifies the attribute usage targets properties only and is detected
    via reflection.
