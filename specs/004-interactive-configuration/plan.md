# Implementation Plan: Interactive Configuration

**Branch**: `004-interactive-configuration` | **Date**: 2026-05-29 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from
`/specs/004-interactive-configuration/spec.md`

**Note**: This plan was reverse-engineered from the existing implementation
on the `feature/nodejs` branch. Decisions visible in the code are recorded
as if they were the original design choices.

## Summary

The interactive-configuration capability gives users a `--config` flag that
opens a Spectre.Console terminal UI for managing model configurations.
Configurations are POCOs persisted to a single JSON file at a
platform-appropriate path; API keys are marked with a property-level
attribute and transparently encrypted at rest through a platform-selected
encryption service (Windows DPAPI or cross-platform AES). The UI also
discovers models from a LiteLLM proxy `/models` endpoint and bulk-imports
them as configurations. The technical approach favours plain .NET 9
abstractions, dependency injection of a single `IUserSettingsService`
implementation, reflection-driven encryption of `[EncryptedSetting]`
properties, and a strict separation between the configuration mode
(`HandleConfigModeAsync` in `Program.cs`) and the prompt-dispatching
pipeline.

## Technical Context

**Language/Version**: C# 12 on .NET 9.0 (`net9.0`), nullable reference
types enabled, "treat warnings as errors".

**Primary Dependencies**:
- `Spectre.Console` — interactive console UI (selection prompts, masked
  text prompts, confirms, tables, figlet text)
- `System.CommandLine` — `--config` flag and validator bypass
- `Microsoft.Extensions.DependencyInjection` — DI container for the
  config-mode service graph
- `Microsoft.Extensions.Http` — `HttpClient` (configuration service uses
  a direct `HttpClient`; the regular CLI path uses `IHttpClientFactory`)
- `Microsoft.Extensions.Logging` + `Serilog` — structured logging
- `System.Text.Json` — settings serialization
- `System.Security.Cryptography` (AES) — cross-platform encryption
- `System.Security.Cryptography.ProtectedData` — DPAPI on Windows

**Storage**: A single JSON file per OS user, written via `File.WriteAllText`
with `JsonNamingPolicy.CamelCase`. Location resolved by
`SettingsPathProvider`:
- Windows: `%APPDATA%\ai-cli\settings.json`
- macOS: `~/Library/Application Support/ai-cli/settings.json`
- Linux/other Unix: `$XDG_CONFIG_HOME/ai-cli/settings.json`, or
  `~/.config/ai-cli/settings.json` when `XDG_CONFIG_HOME` is unset

**Testing**: xUnit + FluentAssertions + Moq. Tests live in
`src/ai-cli.Tests/` and mirror production directory structure. DPAPI tests
self-skip off Windows via `OperatingSystem.IsWindows()`.

**Target Platform**: Cross-platform console app (Windows, Linux, macOS).
Single-file deployment is supported (the `--config` flow opens an
interactive TTY UI so it requires a terminal).

**Project Type**: Single-project CLI (production: `src/ai-cli/`, tests:
`src/ai-cli.Tests/`). No separate library boundary for the configuration
capability — it ships inside the CLI assembly.

**Performance Goals**: Not perf-sensitive. Settings file is small (kilobytes)
and read/written once per CLI invocation. Proxy discovery uses a 30-second
HTTP timeout.

**Constraints**:
- API keys MUST NOT appear in plaintext in the persisted file.
- The persisted file MUST be readable only by the owning user on Unix
  (mode 600).
- API keys MUST NOT be logged at Information level.
- The encryption pipeline MUST be idempotent (double-encrypt-safe and
  double-decrypt-safe) so settings round-trip cleanly across CLI runs.

**Scale/Scope**: One user per file. Number of configurations is bounded
by what fits in human-manageable menus — typically <100 entries even when
imported from LiteLLM.

## Constitution Check

`/.specify/memory/constitution.md` is the unfilled template, so no
ratified principles are in force at the time of this retrospec. The
implementation is recorded here as-built; no waivers required.

Implicit project conventions observed (and respected by this feature):

| Convention | How this feature complies |
|------------|---------------------------|
| Clean-architecture layering (CLI / Application / Infrastructure / Models) | Files are placed in the matching folders. The interactive menu is Infrastructure (touches Spectre.Console + HTTP); persistence and crypto are Infrastructure; interfaces (`IUserSettingsService`, `IEncryptionService`) live in Application. |
| Treat-warnings-as-errors + nullable reference types | All new types annotate nullability; reflection paths handle null. |
| Tests mirror production layout | `Configuration/`, `Infrastructure/`, `Attributes/`, `Models/` test folders all present and populated. |
| Sensitive data must be encrypted at rest | `[EncryptedSetting]` + reflection-driven crypto in `FileUserSettingsService`. |
| Sensitive data must not leak to logs at Info | Logger calls in `ConfigurationService` and `FileUserSettingsService` use `ConfigId`/`PropertyName`/file paths only — no key values. |

## Project Structure

### Documentation (this feature)

```text
specs/004-interactive-configuration/
├── plan.md              # This file
├── spec.md              # Business specification (reverse-engineered)
├── research.md          # Technical decisions captured from the code
├── data-model.md        # Persisted entities and their constraints
├── quickstart.md        # End-to-end usage walk-through
├── contracts/           # Public interfaces and CLI surface
│   ├── cli-config-flag.md
│   ├── IUserSettingsService.md
│   └── IEncryptionService.md
└── checklists/
    └── requirements.md  # Retrospec validation checklist
```

### Source Code (repository root)

```text
src/ai-cli/
├── Program.cs                                    # --config branch lives here
├── CLI/
│   ├── CommandLineBuilder.cs                     # --config option declaration + validator bypass
│   ├── CliOptions.cs                             # Config flag
│   └── ExitCodes.cs                              # 0 success / 4 unknown error
├── Application/
│   ├── IUserSettingsService.cs                   # Load/Save/ResetToDefault
│   └── IEncryptionService.cs                     # Encrypt/Decrypt/IsEncrypted
├── Attributes/
│   └── EncryptedSettingAttribute.cs              # [EncryptedSetting] marker
├── Configuration/
│   └── SettingsPathProvider.cs                   # Platform-aware path resolution
├── Infrastructure/
│   ├── ConfigurationService.cs                   # Spectre.Console interactive menu
│   ├── FileUserSettingsService.cs                # JSON file + reflection-based encryption
│   ├── AesEncryptionService.cs                   # ENC:<base64> (PBKDF2 + AES + IV-per-encrypt)
│   └── DpapiEncryptionService.cs                 # DPAPI:<base64> (CurrentUser scope)
└── Models/
    ├── UserSettings.cs                           # Root + ModelConfiguration + ModelType
    ├── LiteLLMModels.cs                          # /models endpoint DTOs
    └── CliOptions.cs                             # Includes Config flag

src/ai-cli.Tests/
├── Configuration/SettingsPathProviderTests.cs
├── Attributes/EncryptedSettingAttributeTests.cs
├── Models/UserSettingsTests.cs
├── Infrastructure/
│   ├── FileUserSettingsServiceTests.cs
│   ├── AesEncryptionServiceTests.cs
│   └── DpapiEncryptionServiceTests.cs            # Self-skips off Windows
└── CLI/CommandLineBuilderTests.cs
```

**Structure Decision**: This is a single-project CLI. The configuration
capability does not warrant a separate library — it shares the
`ai-cli` assembly's DI graph, models, and logging configuration with the
prompt path. Tests live alongside production tests in `ai-cli.Tests`.

### Notable design decisions visible in the code

- **Two separate DI graphs.** `Program.HandleConfigModeAsync` builds a
  small, throwaway `ServiceCollection` containing only logging,
  encryption, the `FileUserSettingsService`, and a freshly-instantiated
  `HttpClient`. The full prompt-dispatch DI graph (with `IAIClient`,
  `IPromptService`, `IHttpClientFactory`) is only assembled when not in
  config mode. The config session and the prompt session are therefore
  truly disjoint, sharing only the encryption service contract.
- **Reflection-driven encryption.** `FileUserSettingsService` walks every
  `ModelConfiguration`'s public properties, reads the
  `[EncryptedSetting]` attribute, and routes string values through
  `IEncryptionService.Encrypt` / `Decrypt` only when
  `IsEncrypted` agrees with the current direction (idempotent). A
  shallow JSON-based clone is used on save so the in-memory user-facing
  object is never mutated by the encryption pass.
- **CommandLine validator bypass.** `--config` short-circuits the prompt-
  source and temperature/format validators. This is what makes
  `ai-cli --config` legal without `--prompt`, `--file`, or stdin.
- **Default-id self-healing.** On load, an unresolved
  `DefaultModelConfigurationId` is rewritten to the first available
  configuration's id; on a save that removes the current default, the
  same self-healing happens before persistence.
- **LiteLLM-proxy import does not store the API key.** `ApiKey` is left
  null and is intended to be filled in either through the dedicated
  "Set API Key for LiteLLM Proxy Models" menu entry or via the
  `AI_API_KEY` environment variable read by `Program.ConfigureServices`.

## Complexity Tracking

| Area | Choice | Why | Simpler alternative that was rejected |
|------|--------|-----|---------------------------------------|
| Two encryption services | Pick at startup based on `OperatingSystem.IsWindows()` | DPAPI provides OS-managed per-user keys on Windows; AES gives cross-platform parity without a key file | Universal AES on every platform — rejected because DPAPI is the platform-idiomatic choice on Windows and avoids storing a derived key locally |
| Reflection over the configuration POCOs | Generic encryption pipeline that any future `[EncryptedSetting]` property automatically inherits | Adding a new sensitive field needs only the attribute, not a new code path | Hard-coded `EncryptApiKey` / `DecryptApiKey` methods — rejected because it scales poorly and forgets new fields silently |
| JSON-based deep clone on save | Avoid mutating the caller's in-memory `UserSettings` while encrypting | Caller can keep using the same object after `Save` | In-place encrypt-then-decrypt — rejected because a save failure would leave the in-memory copy in encrypted form |
| Throwaway `HttpClient` in config mode | Configuration service is short-lived; full `IHttpClientFactory` machinery is unnecessary | Keeps `HandleConfigModeAsync` cheap and isolated from the prompt path | Sharing the prompt-mode `HttpClient` — rejected because the configuration session has different timeout requirements (30s vs 5min) and should not be entangled with the AI client lifecycle |
| Idempotent encrypt/decrypt via known prefixes (`ENC:`, `DPAPI:`) | Avoid double-encryption and crash-on-decrypt-of-plaintext when a settings file is partially edited by hand or upgraded from an older format | Robust to manual edits and migrations | Storing a version number in the JSON — rejected as overkill for a single sensitive field |
