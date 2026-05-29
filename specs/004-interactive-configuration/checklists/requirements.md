# Requirements Checklist: Interactive Configuration

**Purpose**: Confirm that every requirement captured in spec.md is
satisfied by the existing implementation. All items are pre-marked `[x]`
because the code already ships.

**Created**: 2026-05-29
**Feature**: [spec.md](../spec.md)

## CLI surface

- [x] CHK001 `--config` flag is declared and accepted
      *(satisfied by `CommandLineBuilder.cs` `_configOption`; spec FR-001)*
- [x] CHK002 `--config` bypasses prompt-source and validator checks
      *(satisfied by the `if (isConfigMode) return;` short-circuit in
      `CommandLineBuilder.AddValidator`; spec FR-002)*
- [x] CHK003 Clean menu exit returns exit code 0; unhandled exception
      returns 4
      *(satisfied by `HandleConfigModeAsync` returning
      `ExitCodes.Success` on normal flow and `ExitCodes.UnknownError`
      from the catch; spec FR-003)*

## Main menu

- [x] CHK010 Menu loops until "Exit" is chosen
      *(satisfied by the `while (true)` loop in
      `ConfigurationService.StartConfigurationAsync`; spec FR-010)*
- [x] CHK011 All eight menu entries present in declared order
      *(satisfied by the literal `AddChoices(new[] { ... })` in
      `StartConfigurationAsync`; spec FR-010)*
- [x] CHK012 "Exit" prints a success message and returns
      *(satisfied by the `case "Exit"` branch; spec FR-011)*

## Add / remove / list / set default

- [x] CHK020 "Add Model Configuration" collects ID, name, API key
      (masked), base URL, model, temperature, max tokens, format, stream
      *(satisfied by `AddModelConfigurationAsync`; spec FR-020)*
- [x] CHK021 Duplicate ID is rejected on add
      *(satisfied by the `GetModelConfiguration(id) != null` early
      return in `AddModelConfigurationAsync`; spec FR-021)*
- [x] CHK022 Temperature outside `[0.0, 2.0]` is clamped to 1.0
      *(satisfied by the bounds check in
      `AddModelConfigurationAsync`; spec FR-022)*
- [x] CHK023 New entries are created with `Type = Generic`
      *(satisfied by the `Type = ModelType.Generic` assignment in
      `AddModelConfigurationAsync`; spec FR-023)*
- [x] CHK030 Remove requires confirmation
      *(satisfied by `AnsiConsole.Confirm(...)` in
      `RemoveModelConfigurationAsync`; spec FR-030)*
- [x] CHK031 Removing the current default promotes the first remaining
      configuration (or clears the pointer)
      *(satisfied by the `DefaultModelConfigurationId` self-heal block in
      `RemoveModelConfigurationAsync`; spec FR-031)*
- [x] CHK040 Remove-all requires confirmation and clears the default
      *(satisfied by `RemoveAllModelConfigurationsAsync`; spec FR-040)*
- [x] CHK050 List displays ID, Name, Type (rendered), Model, Base URL,
      Default marker
      *(satisfied by `ListModelConfigurationsAsync`; spec FR-050)*
- [x] CHK060 Set-default short-circuits on one-config and prompts on
      multi-config
      *(satisfied by the `Count == 1` early return in
      `SetDefaultModelConfigurationAsync`; spec FR-060)*

## LiteLLM proxy integration

- [x] CHK070 Proxy URL prompt with default `http://localhost:4000` and
      HTTP GET of `<url>/models`
      *(satisfied by `ConfigureLiteLLMProxyAsync` +
      `FetchLiteLLMModelsAsync`; spec FR-070)*
- [x] CHK071 Discovered models are shown in a table with explicit
      confirmation before import
      *(satisfied by the table render + `AnsiConsole.Confirm` in
      `ConfigureLiteLLMProxyAsync`; spec FR-071)*
- [x] CHK072 Each imported model becomes a configuration with the
      documented field defaults
      *(satisfied by `ConfigureModelsFromLiteLLMAsync`; spec FR-072)*
- [x] CHK073 Duplicate target IDs are skipped and counted
      *(satisfied by the `GetModelConfiguration(configId) != null` skip
      branch in `ConfigureModelsFromLiteLLMAsync`; spec FR-073)*
- [x] CHK074 Empty model lists, unreachable proxies, and non-success
      status codes are surfaced as errors without partial state changes
      *(satisfied by the `try/catch` around `FetchLiteLLMModelsAsync` and
      the `models.Count == 0` early return; spec FR-074)*
- [x] CHK080 Bulk API-key update presents Set/Not Set status, masks
      input, confirms, and applies to every LiteLLM-proxy configuration
      *(satisfied by `SetLiteLLMProxyApiKeyAsync`; spec FR-080)*

## Persistence

- [x] CHK090 Settings file path is platform-appropriate
      *(satisfied by `SettingsPathProvider.GetAppDataPath`; spec FR-090)*
- [x] CHK091 Parent directory is created if absent on first save
      *(satisfied by the `Directory.CreateDirectory(directory)` block
      in `FileUserSettingsService.Save`; spec FR-091)*
- [x] CHK092 Unix file mode 600 is applied after save
      *(satisfied by `File.SetUnixFileMode` in
      `FileUserSettingsService.Save`; spec FR-092)*
- [x] CHK093 Missing / empty / unparseable files fall back to defaults
      *(satisfied by the three early-return branches and the
      `catch (JsonException)` block in `FileUserSettingsService.Load`;
      spec FR-093)*
- [x] CHK094 Stale default-id is self-healed on load
      *(satisfied by the `DefaultModelConfigurationId` repair block in
      `FileUserSettingsService.Load`; spec FR-094)*
- [x] CHK095 Missing required model-configuration fields are filled with
      defaults on load
      *(satisfied by the `??=` block in `FileUserSettingsService.Load`;
      spec FR-095)*

## Encryption at rest

- [x] CHK100 `[EncryptedSetting]` properties are encrypted on save and
      decrypted on load uniformly
      *(satisfied by the reflection walk in
      `FileUserSettingsService.EncryptEncryptedProperties` /
      `DecryptEncryptedProperties`; spec FR-100)*
- [x] CHK101 The attribute is property-only and disallows duplicates
      *(satisfied by `[AttributeUsage(AttributeTargets.Property,
      AllowMultiple = false)]` on `EncryptedSettingAttribute`;
      spec FR-101)*
- [x] CHK102 DPAPI scheme on Windows with `CurrentUser` scope, app
      entropy, and `DPAPI:` prefix
      *(satisfied by `DpapiEncryptionService.Encrypt` /
      `IsEncrypted`; spec FR-102)*
- [x] CHK103 AES scheme on non-Windows with PBKDF2-derived 256-bit key,
      fresh IV per encryption, and `ENC:` prefix
      *(satisfied by `AesEncryptionService`; spec FR-103)*
- [x] CHK104 Encryption pipeline is idempotent in both directions
      *(satisfied by the `IsEncrypted` gate in
      `EncryptEncryptedProperties` and `DecryptEncryptedProperties`;
      spec FR-104)*
- [x] CHK105 Decryption failures are logged and do not crash the app
      *(satisfied by the per-property `try/catch` inside
      `DecryptEncryptedProperties`; spec FR-105)*
- [x] CHK106 DPAPI service refuses to instantiate off Windows
      *(satisfied by the `PlatformNotSupportedException` throw in
      `DpapiEncryptionService` constructor; spec FR-106)*

## Operational integration

- [x] CHK110 Default configuration's API key is decrypted in memory at
      runtime and not logged at Information level
      *(satisfied by `FileUserSettingsService.Load` decrypting before
      returning + log statements in `ConfigurationService` and
      `FileUserSettingsService` referencing only `ConfigId` /
      `PropertyName`, never the key value; spec FR-110)*
- [x] CHK111 CLI options override the default configuration's values for
      one invocation without persisting changes
      *(satisfied by the effective-value computation in
      `HandleCommandAsync`; spec FR-111)*
- [x] CHK112 Prompt-mode launch with no configurations exits with code 1
      and an instruction to use `--config`
      *(satisfied by the `defaultModelConfig == null` early return in
      `HandleCommandAsync`; spec FR-112)*

## Test coverage cross-checks

- [x] CHK200 `FileUserSettingsService` round-trip tests cover save,
      load, missing file, invalid JSON, empty file, partial JSON, stale
      default-id repair, directory creation, encrypted save, decrypted
      load, and mixed encrypted/plain entries
      *(`src/ai-cli.Tests/Infrastructure/FileUserSettingsServiceTests.cs`)*
- [x] CHK201 `AesEncryptionService` tests cover round-trip with ASCII,
      special chars, unicode, empty, and null inputs; cross-instance
      compatibility; and non-determinism of `Encrypt`
      *(`src/ai-cli.Tests/Infrastructure/AesEncryptionServiceTests.cs`)*
- [x] CHK202 `DpapiEncryptionService` tests cover the
      `PlatformNotSupportedException` on non-Windows hosts and the
      full Windows round-trip
      *(`src/ai-cli.Tests/Infrastructure/DpapiEncryptionServiceTests.cs`,
      self-skipping off Windows)*
- [x] CHK203 `EncryptedSettingAttribute` tests verify property-only
      target, no-duplicate, and reflection discovery
      *(`src/ai-cli.Tests/Attributes/EncryptedSettingAttributeTests.cs`)*
- [x] CHK204 `UserSettings` tests verify default factory, get-default
      with valid/invalid id, lookup, and add-or-update semantics
      *(`src/ai-cli.Tests/Models/UserSettingsTests.cs`)*
- [x] CHK205 `SettingsPathProvider` tests verify default path,
      custom-path override, and null/empty-fallback behaviour
      *(`src/ai-cli.Tests/Configuration/SettingsPathProviderTests.cs`)*

## Notes

- Items are numbered for cross-reference; ranges 001-199 mirror the
  functional-requirement groups in spec.md, and 200+ map to existing
  test coverage.
- The constitution file
  (`.specify/memory/constitution.md`) is still in template form, so no
  formal constitution gate applies; project-level conventions are
  documented in `plan.md` and have been complied with.
