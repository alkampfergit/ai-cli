# Data Model: Interactive Configuration

This document captures the persisted entities and their constraints as
implemented in `src/ai-cli/Models/UserSettings.cs` and
`src/ai-cli/Models/LiteLLMModels.cs`.

## UserSettings (root persisted entity)

The root container serialised to `settings.json`.

**C# type**: `AiCli.Models.UserSettings`
**File**: `src/ai-cli/Models/UserSettings.cs`
**Lifetime**: One per OS user.

| Field                          | C# type                       | JSON name                      | Default        | Constraints / Behavior |
|--------------------------------|-------------------------------|--------------------------------|----------------|------------------------|
| `ModelConfigurations`          | `List<ModelConfiguration>`    | `modelConfigurations`          | empty list     | A missing field on load is replaced by an empty list. |
| `DefaultModelConfigurationId`  | `string`                      | `defaultModelConfigurationId`  | `""`           | On load: if empty OR not present in the configurations list, it is set to the first configuration's `Id`, or cleared when no configurations exist. |
| `RefreshInterval`              | `int` (seconds)               | `refreshInterval`              | `30`           | Reserved for future settings-file hot-reloading. Not actively consumed by this feature. |

**Helpers** (instance methods, no persisted state):
- `GetDefaultModelConfiguration()` — returns the entry whose `Id` matches
  `DefaultModelConfigurationId`, falling back to the first configuration
  if the pointer is stale, or `null` if the list is empty.
- `GetModelConfiguration(string id)` — linear lookup, `null` when missing.
- `AddOrUpdateModelConfiguration(ModelConfiguration)` — replaces an
  existing entry with the same `Id`, or appends a new one.

**Factory**:
- `UserSettings.CreateDefault()` — produces a settings object equivalent
  to the constructor defaults; used as the fallback whenever the file is
  missing, empty, or unparseable.

## ModelConfiguration

A single named provider+model preset.

**C# type**: `AiCli.Models.ModelConfiguration`
**File**: `src/ai-cli/Models/UserSettings.cs`

| Field        | C# type        | JSON name      | Default          | Encrypted | Notes |
|--------------|----------------|----------------|------------------|-----------|-------|
| `Id`         | `string`       | `id`           | `""`             | no        | Unique within the file. Mutating "Add" rejects duplicates. On load a null/missing value is replaced with `"default"`. |
| `Name`       | `string`       | `name`         | `""`             | no        | Human display label. On load a null/missing value is replaced with `"Default Configuration"`. |
| `Type`       | `ModelType`    | `type`         | `Generic` (`0`)  | no        | Distinguishes user-defined entries from auto-imported LiteLLM ones. |
| `ApiKey`     | `string?`      | `apiKey`       | `null`           | yes (`[EncryptedSetting]`) | Stored as `DPAPI:<base64>` on Windows, `ENC:<base64>` elsewhere. Idempotent encryption — already-encrypted values are passed through. |
| `BaseUrl`    | `string?`      | `baseUrl`      | `null`           | no        | `null` means "use the AI client's built-in default". |
| `Model`      | `string`       | `model`        | `"gpt-3.5-turbo"`| no        | On load a null/missing value is replaced with `"gpt-3.5-turbo"`. |
| `Temperature`| `float`        | `temperature`  | `1.0f`           | no        | Valid range `[0.0, 2.0]`. The "Add" menu clamps invalid input back to 1.0 and informs the user. |
| `MaxTokens`  | `int?`         | `maxTokens`    | `null`           | no        | `null` means unlimited. Non-numeric input in the menu is treated as null. |
| `Format`     | `string`       | `format`       | `"text"`         | no        | Restricted to `"text"` or `"json"` by the "Add" menu's selection prompt. On load a null/missing value is replaced with `"text"`. |
| `Stream`     | `bool`         | `stream`       | `false`          | no        | Default streaming preference. |

**Property-level encryption marker**: `[EncryptedSetting]` (defined in
`src/ai-cli/Attributes/EncryptedSettingAttribute.cs`,
`AttributeTargets.Property`, `AllowMultiple = false`). The persistence
layer scans every `ModelConfiguration` instance via reflection, finds
string-typed properties carrying this attribute, and routes them through
`IEncryptionService` on save and load.

## ModelType (enum)

**C# type**: `AiCli.Models.ModelType`
**Underlying type**: `int`

| Member        | Value | Meaning |
|---------------|-------|---------|
| `Generic`     | `0`   | A user-entered configuration. Subject to "Set Default" and "Remove" but not to the bulk "Set API Key for LiteLLM Proxy Models" command. |
| `LiteLlmProxy`| `1`   | An auto-imported configuration. Eligible for the bulk LiteLLM API-key update. |

The "List Model Configurations" command renders these as `Generic` and
`LiteLLM Proxy` respectively.

## LiteLLMModelsResponse / LiteLLMModel (transient DTOs)

These types are not persisted; they exist only to deserialise the proxy's
`/models` HTTP response.

**File**: `src/ai-cli/Models/LiteLLMModels.cs`

### LiteLLMModelsResponse

| Field   | C# type              | JSON name | Notes |
|---------|----------------------|-----------|-------|
| `Data`  | `List<LiteLLMModel>` | `data`    | List of available models. |

### LiteLLMModel

| Field     | C# type | JSON name  | Notes |
|-----------|---------|------------|-------|
| `Id`      | `string`| `id`       | Becomes both the `ModelConfiguration.Model` and the `Id` suffix (`litellm-<id>`). |
| `Object`  | `string`| `object`   | Discarded after parsing. |
| `Created` | `long`  | `created`  | Discarded after parsing. |
| `OwnedBy` | `string`| `owned_by` | Displayed in the discovery table. |

## Validation rules summary

| Where | Rule |
|-------|------|
| Add Model Configuration | Reject duplicate `Id`. Clamp `Temperature` to `[0.0, 2.0]` (substitute 1.0 on violation). Treat non-numeric `MaxTokens` as null. `Format` is selected from a fixed list `{"text","json"}`. |
| Remove Model Configuration | Confirm before deletion. Promote first remaining entry to default when removing the current default. |
| Remove All Models | Confirm before deletion. Clear `DefaultModelConfigurationId`. |
| Set Default | No-op message when only one configuration exists. |
| LiteLLM import | Skip entries whose target `Id` already exists. Report added/skipped counts. |
| LiteLLM bulk API-key | Confirm before applying. Empty input means clear. |
| File load | Missing → defaults. Empty → defaults. Invalid JSON → defaults (logged). |
| File load | Self-heal `DefaultModelConfigurationId` to first configuration when stale. |
| File save (Unix) | Set file mode `600` after write. |
| Encryption | Idempotent: skip Encrypt when `IsEncrypted` is true; skip Decrypt when `IsEncrypted` is false. |

## State transitions

`UserSettings` itself has no state machine — it is a flat document that
is replaced as a whole on every save. Within it, the
`DefaultModelConfigurationId` pointer transitions as follows:

```
no configs  ──add first──▶  [empty]      (config added, pointer left empty unless caller sets it)
                            ──set-default──▶  [config-id]
[config-id] ──remove current default & N remaining > 0──▶  [first-remaining-id]
[config-id] ──remove current default & N remaining = 0──▶  [empty]
[empty]     ──load file & configs non-empty──▶  [first-config-id]
[stale-id]  ──load file──▶  [first-config-id]  (self-heal)
```
