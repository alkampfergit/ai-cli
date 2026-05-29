# Research: Interactive Configuration

Technical decisions inferred from the existing implementation. Each entry
explains *what* was chosen, *why* it was chosen (inferred from code
shape, comments, and conventional .NET practice), and *what reasonable
alternatives were not chosen*.

## Decision: Use Spectre.Console for the interactive UI

- **Chosen**: Spectre.Console `SelectionPrompt`, `TextPrompt.Secret`,
  `Confirm`, `Table`, and `FigletText` to render the configuration UI.
- **Rationale**: Provides masked input out of the box (`.Secret()`) so
  API keys are not echoed to the terminal, plus tabular rendering for
  "List Model Configurations" and the LiteLLM discovery table. It is
  already used elsewhere in this codebase as the standard rich-console
  library, keeping the dependency surface coherent.
- **Alternatives considered**:
  - Hand-rolled `Console.ReadLine` loops with manual masking — rejected
    because secret-masking is non-trivial to implement portably, and the
    feature requires multi-line decision tables.
  - A GUI configuration tool — out of scope for a console CLI.

## Decision: Property-level encryption via custom attribute + reflection

- **Chosen**: `EncryptedSettingAttribute` marks properties that must be
  encrypted at rest. `FileUserSettingsService` walks every
  `ModelConfiguration` via reflection, finds string properties with the
  attribute, and routes them through `IEncryptionService` on save / load.
- **Rationale**: Adding a new sensitive field is a one-line attribute
  change with no plumbing. The pipeline is uniform across every property
  and every configuration entry. Using reflection at startup-rare,
  save/load-rare boundaries (not per-request) keeps performance
  irrelevant.
- **Alternatives considered**:
  - Hard-coded `EncryptApiKey` / `DecryptApiKey` helpers in
    `FileUserSettingsService` — rejected because it scales poorly and
    silently misses new fields.
  - JSON converter that does encryption during serialization — rejected
    because it couples the encryption layer to a particular serializer
    and complicates testing.

## Decision: Two encryption schemes, platform-selected

- **Chosen**: `DpapiEncryptionService` on Windows (`DPAPI:` prefix),
  `AesEncryptionService` elsewhere (`ENC:` prefix). Selection happens
  via `OperatingSystem.IsWindows()` at DI registration time.
- **Rationale**: DPAPI provides per-user OS-managed key material on
  Windows with no need to store a key file. On Linux/macOS there is no
  equivalent ambient OS API in .NET, so a self-contained AES scheme is
  needed; PBKDF2-derived keys from machine+user entropy let the same
  process re-decrypt without persisting a key separately.
- **Alternatives considered**:
  - Universal AES on every platform — rejected because DPAPI is the
    platform-idiomatic choice on Windows and avoids the need to derive
    or store a symmetric key.
  - libsecret / Keychain bridging on Linux/macOS — rejected as out of
    scope for a single-file deployment and adds native dependencies.
  - Storing the AES key in a separate file — rejected because a derived
    key from environment entropy is sufficient for the threat model
    (defence-in-depth, not strong secrecy against a determined
    attacker with local read access).

## Decision: Cipher-text prefixes (`DPAPI:`, `ENC:`) as encryption markers

- **Chosen**: Each implementation tags its output with a fixed ASCII
  prefix and uses the prefix to gate `IsEncrypted`.
- **Rationale**: The persistence layer needs an O(1) way to decide
  whether a value should be sent through `Decrypt` (on load) or
  `Encrypt` (on save). A prefix in the stored string makes the check
  cheap, makes round-trips idempotent, and makes a settings file
  visibly distinguishable from a hand-edit.
- **Alternatives considered**:
  - A sibling JSON field (e.g. `apiKeyEncrypted: true`) — rejected
    because it complicates the model class and forces every consumer to
    keep two fields in sync.
  - Heuristic detection (try-decrypt, fall back to plaintext) —
    rejected because it complicates error reporting and risks
    misclassifying real plaintext that happens to be valid base64.

## Decision: Idempotent encrypt-on-save and decrypt-on-load

- **Chosen**: The save path encrypts only when `IsEncrypted` is false;
  the load path decrypts only when `IsEncrypted` is true.
- **Rationale**: Supports a settings file that has been partially edited
  by hand (where one field is in plaintext and another is encrypted)
  and supports re-saves without double-encryption. Makes the encryption
  contract safe to apply repeatedly.
- **Alternatives considered**:
  - Always encrypt on save / always decrypt on load — rejected because
    a re-save would corrupt an already-encrypted field and a load of a
    hand-edited plaintext field would crash with a decrypt failure.

## Decision: JSON-based deep clone before encrypting on save

- **Chosen**: `CloneSettings` round-trips through JSON to produce a
  copy that the encryption pass mutates, leaving the caller's in-memory
  `UserSettings` untouched.
- **Rationale**: After `Save`, the caller can keep using the same
  `UserSettings` object and read plaintext API keys. Without the clone,
  the in-memory object would be left in encrypted form, requiring
  callers to immediately call `Load` to recover plaintext.
- **Alternatives considered**:
  - In-place encrypt + decrypt-after-write — rejected because a save
    failure between the encrypt step and the decrypt step would leave
    the caller's object in a broken state.
  - A manual deep-copy method — rejected as more code to maintain than
    the JSON-serializer round-trip given that the type is already
    JSON-serializable.

## Decision: Cross-platform path resolution honouring XDG

- **Chosen**: `SettingsPathProvider` returns `%APPDATA%\ai-cli` on
  Windows, `~/Library/Application Support/ai-cli` on macOS, and
  `$XDG_CONFIG_HOME/ai-cli` (or `~/.config/ai-cli`) on other Unix.
- **Rationale**: Matches each platform's standard convention for
  per-user application configuration. Honours the XDG Base Directory
  Specification on Linux so users who relocate config dirs are
  respected.
- **Alternatives considered**:
  - A single `~/.ai-cli` directory on every platform — rejected because
    it violates Windows and macOS conventions.
  - Reading the path from an environment variable on every platform —
    rejected because it shifts the burden onto users who would
    otherwise expect the standard location.

## Decision: 600 permissions on Unix-family hosts

- **Chosen**: `File.SetUnixFileMode(_, UserRead | UserWrite)` after
  every save, with failures logged but not thrown.
- **Rationale**: Even though the API key is encrypted, additional
  defence in depth keeps the file unreadable by other local users. The
  failure-as-warning policy keeps the feature usable on filesystems
  that don't support Unix modes (e.g., a mounted Windows share).
- **Alternatives considered**:
  - Refuse to write when the mode cannot be set — rejected because it
    would break legitimate users on shared filesystems.

## Decision: Two separate DI graphs (config-mode vs. prompt-mode)

- **Chosen**: `HandleConfigModeAsync` builds its own small
  `ServiceCollection` with only logging, `IEncryptionService`, and a
  manually-constructed `FileUserSettingsService` + `HttpClient`. The
  full prompt-mode DI graph (with `IAIClient`, `IPromptService`,
  `IHttpClientFactory`) is only constructed when not in config mode.
- **Rationale**: The configuration session has different timeout
  requirements (30 seconds, not 5 minutes) and does not need the
  prompt-dispatch services. Keeping the graphs separate avoids
  entangling lifetimes and clarifies which services are reachable from
  which path.
- **Alternatives considered**:
  - A single unified DI graph used by both paths — rejected because
    the prompt path eagerly loads default model configuration into the
    `IAIClient`, which would attempt to read settings that may not yet
    exist when the user is in config mode for the first time.

## Decision: LiteLLM proxy import skips entries with matching IDs

- **Chosen**: The import constructs a target ID
  `litellm-<model-id>` and silently skips entries whose ID already
  exists, reporting the skipped count alongside the added count.
- **Rationale**: Allows safe re-import after the proxy adds new models,
  without clobbering user-customised entries (e.g., API keys filled in
  via the bulk-update menu).
- **Alternatives considered**:
  - Overwrite on import — rejected because it would discard user
    customisations such as the API key set via "Set API Key for
    LiteLLM Proxy Models".
  - Prompt per duplicate — rejected for N=100 cases where the prompt
    fatigue would dwarf the value.

## Decision: Imported LiteLLM configurations start with `ApiKey = null`

- **Chosen**: Bulk import does not collect an API key. Users either
  fill it via the dedicated "Set API Key for LiteLLM Proxy Models"
  menu entry or via the `AI_API_KEY` environment variable
  (`Program.ConfigureServices` reads it as a fallback when the default
  configuration's `ApiKey` is null).
- **Rationale**: The proxy already authenticates upstream model calls
  for the user; some deployments do not require any client-side key.
  Forcing a key prompt for every imported entry creates friction in
  the common case.
- **Alternatives considered**:
  - Ask for a single key during import and broadcast — partially
    overlaps with the dedicated bulk-update entry. The chosen design
    keeps import strictly about discovery.

## Decision: Self-healing of `DefaultModelConfigurationId`

- **Chosen**: On load (and on remove), an empty or stale default-id is
  silently rewritten to the first available configuration's id, or
  cleared when no configurations exist.
- **Rationale**: Makes the file robust to manual edits and to the
  remove-default-then-add-replacement workflow. Avoids users seeing
  "no model configured" errors when a configuration does in fact exist.
- **Alternatives considered**:
  - Surface an explicit error for a stale pointer — rejected because
    the recovery path is unambiguous and silent self-healing is the
    least surprising behaviour.
