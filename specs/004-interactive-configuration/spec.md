# Feature Specification: Interactive Configuration

**Feature Branch**: `004-interactive-configuration`

**Created**: 2026-05-29

**Status**: Draft (reverse-engineered from existing implementation)

**Input**: User description: "Interactive configuration mode (`--config` flag)
that launches a Spectre.Console-based UI for managing model configurations.
Users can add, edit, delete, and select default model configurations including
API keys, base URLs, and model names. Settings persist to a cross-platform
user settings file via `FileUserSettingsService` with paths resolved by
`SettingsPathProvider`. API keys marked with `EncryptedSettingAttribute` are
encrypted at rest using `DpapiEncryptionService` on Windows and
`AesEncryptionService` on other platforms. The configuration UI also supports
discovering and importing models from a LiteLLM proxy endpoint."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - First-time setup of a model configuration (Priority: P1)

A new user installs the CLI and runs it with `--config`. They are dropped
into an interactive menu where they can create their first model
configuration by supplying an ID, a friendly name, an API key, an optional
base URL, the model name, a default temperature, optional max tokens, an
output format, and a streaming preference. After confirmation, the
configuration is saved, the API key is encrypted at rest, and the user can
immediately use the CLI without `--config` to send prompts to that model.

**Why this priority**: The CLI refuses to send any prompt when no model
configuration is present (it exits with an "Error: No model configuration
found in settings. Please configure at least one model using --config."
message). Without this first-run setup capability, the rest of the product
is unreachable.

**Independent Test**: Run the CLI with `--config` against a clean user
profile (no settings file). Step through the "Add Model Configuration"
menu entry. Exit the menu, then run the CLI with `--prompt "hello"` and
observe that the prompt is dispatched using the just-configured model.
The settings file exists on disk, the API key field is stored as an
encrypted blob (with a recognisable encryption prefix), and no plaintext
API key is present in the file.

**Acceptance Scenarios**:

1. **Given** no settings file exists, **When** the user launches `--config`
   and selects "Add Model Configuration", **Then** the menu prompts for
   ID, name, API key (masked input), base URL (with default), model name
   (with default), temperature (with default), max tokens (optional),
   format (selectable from `text`/`json`), and streaming preference, and
   on completion stores a new configuration and reports success.
2. **Given** a model configuration was just added, **When** the user exits
   the menu, **Then** the configuration file is created on disk with
   restrictive permissions on Unix (owner read/write only) and the API
   key field is stored in encrypted form.
3. **Given** at least one model configuration exists, **When** the user
   runs the CLI without `--config` and without specifying `--model`,
   **Then** the CLI uses the default configuration's API key, base URL,
   model name, temperature, max tokens, format, and stream preference.
4. **Given** the user attempts to add a configuration with an ID that
   already exists, **When** they submit the form, **Then** the menu
   rejects the operation with an error and the existing configuration is
   left unchanged.

---

### User Story 2 - Manage multiple configurations and pick a default (Priority: P1)

A power user maintains several model configurations (for example: one for
a cloud OpenAI endpoint, one for a local server, one for a different
provider) and wants to switch which one is used by default. They run
`--config`, list their existing configurations in a tabular view, choose
"Set Default Model Configuration", pick from the list, and the new default
is persisted.

**Why this priority**: The CLI consumes exactly one default model
configuration per invocation when no model is specified on the command
line. Once more than one configuration exists, the ability to designate
which is the default is the only way to control the routine behavior of
every subsequent prompt.

**Independent Test**: With two configurations already saved, launch
`--config`, choose "List Model Configurations" to confirm both appear in
the table with one marked as default, then choose "Set Default Model
Configuration" and pick the other one. Exit, then verify by listing again
that the selection moved.

**Acceptance Scenarios**:

1. **Given** two or more model configurations exist, **When** the user
   chooses "List Model Configurations", **Then** they see a table with
   columns ID, Name, Type, Model, Base URL, and Default, where the row
   marked Yes corresponds to the currently active default.
2. **Given** two or more model configurations exist, **When** the user
   chooses "Set Default Model Configuration" and selects an entry,
   **Then** the selected configuration becomes the default and persists
   across CLI invocations.
3. **Given** exactly one configuration exists, **When** the user chooses
   "Set Default Model Configuration", **Then** the menu reports that the
   single configuration is already the default and makes no change.
4. **Given** no configurations exist, **When** the user chooses "Set
   Default Model Configuration", "List Model Configurations", or "Remove
   Model Configuration", **Then** the menu reports the empty state and
   returns to the main menu without error.

---

### User Story 3 - Remove configurations safely (Priority: P2)

A user wants to remove a configuration they no longer use, or to wipe all
configurations and start over. They use the menu, confirm the destructive
action, and the system removes the selected entry (or all entries) and
keeps the default-id pointer in a consistent state.

**Why this priority**: Removing entries is essential for hygiene
(rotated keys, decommissioned providers) but is not blocking for first
use. A confirmation prompt makes the action recoverable from typos.

**Independent Test**: With three configurations, including one marked as
default, launch `--config`, choose "Remove Model Configuration", select
the current default, and confirm. After exit, list configurations and
observe (a) the chosen entry is gone, (b) a different remaining
configuration is now the default, and (c) the file on disk reflects the
new default pointer.

**Acceptance Scenarios**:

1. **Given** at least one model configuration exists, **When** the user
   chooses "Remove Model Configuration", selects one, and confirms,
   **Then** that configuration is removed from the settings file.
2. **Given** the removed configuration was the default and other
   configurations remain, **When** the removal is confirmed, **Then** the
   first remaining configuration becomes the new default and the menu
   reports the change.
3. **Given** the removed configuration was the last one, **When** the
   removal is confirmed, **Then** the default-configuration pointer is
   cleared.
4. **Given** any configurations exist, **When** the user chooses "Remove
   All Models" and confirms, **Then** every configuration is removed and
   the default pointer is cleared. The menu requires explicit confirmation
   before the destructive action.
5. **Given** any confirmation prompt is presented, **When** the user
   declines, **Then** no change is persisted and the menu returns to the
   main loop.

---

### User Story 4 - Discover and import models from a LiteLLM proxy (Priority: P2)

A user runs a local or remote LiteLLM proxy that exposes many model IDs.
They want to register all of them as configurations at once so they can
switch between them by ID without re-typing connection details. They
choose "LiteLLM Proxy", enter the proxy URL (with a default of
`http://localhost:4000`), and the menu queries the proxy's `/models`
endpoint, displays the discovered model list, asks for confirmation, and
on accept registers every new model as its own configuration with the
proxy URL as the base URL.

**Why this priority**: This is a productivity multiplier when a LiteLLM
proxy is available, but it is not on the critical path because users can
always add individual configurations manually (US1). It explicitly skips
models that already correspond to an existing configuration, so it is
safe to re-run.

**Independent Test**: With a reachable LiteLLM-style endpoint returning a
list of two models, choose "LiteLLM Proxy", enter the URL, confirm the
discovered models, and verify that two new configurations now exist with
IDs of the form `litellm-<model-id>`, type marked as LiteLLM Proxy, and
the proxy URL set as the base URL.

**Acceptance Scenarios**:

1. **Given** the proxy URL is valid and responds with a list of models,
   **When** the user accepts the import, **Then** the menu adds one
   configuration per model with ID `litellm-<model-id>`, Name
   `LiteLLM: <model-id>`, Type LiteLLM Proxy, base URL equal to the
   proxy URL, default temperature 1.0, format `text`, streaming
   disabled, and a null API key.
2. **Given** the import would create a configuration whose ID already
   exists, **When** the import runs, **Then** that model is skipped and
   the menu reports the count of skipped entries alongside the count of
   added entries.
3. **Given** the proxy URL is unreachable or returns an error, **When**
   the discovery attempt is made, **Then** the menu reports an error
   message including the proxy URL and returns to the main menu without
   modifying settings.
4. **Given** the proxy returns an empty model list, **When** the menu
   completes the fetch, **Then** no configurations are added and the
   user is informed that no models were found.

---

### User Story 5 - Apply a single API key to every LiteLLM-proxy configuration (Priority: P3)

After importing many configurations from a LiteLLM proxy, the user wants
to set the proxy's API key in one step rather than editing each entry. They
choose "Set API Key for LiteLLM Proxy Models", see a table of every
LiteLLM-proxy configuration with the current key status (Set or Not Set),
enter the key (masked, with the option to clear by pressing Enter),
confirm the bulk update, and the key is applied to every LiteLLM-proxy
configuration and encrypted at rest.

**Why this priority**: This is an ergonomic accelerator for the LiteLLM
flow but is not on the critical path because individual configurations
can still be edited by re-adding them.

**Independent Test**: Import two LiteLLM-proxy configurations (US4), then
choose "Set API Key for LiteLLM Proxy Models", enter a key, and confirm.
The settings file now has both configurations storing the same encrypted
API key blob. Re-run the menu and observe that both rows show "Set" under
"Current API Key Status".

**Acceptance Scenarios**:

1. **Given** at least one LiteLLM-proxy configuration exists, **When** the
   user supplies an API key and confirms, **Then** every LiteLLM-proxy
   configuration is updated with that key and the change is persisted.
2. **Given** the user supplies an empty value, **When** they confirm,
   **Then** the API key is cleared on every LiteLLM-proxy configuration.
3. **Given** no LiteLLM-proxy configurations exist, **When** the user
   chooses this option, **Then** the menu reports the empty state and
   returns to the main menu without prompting for a key.
4. **Given** the user declines the bulk-update confirmation, **When** they
   decline, **Then** no LiteLLM-proxy configurations are modified.

---

### Edge Cases

- The settings file does not exist on first launch — the system MUST treat
  the absence of a file as an empty configuration set and continue without
  error.
- The settings file is empty or contains invalid JSON — the system MUST
  fall back to defaults rather than crash, and MUST log the parse failure.
- The settings file references a default configuration ID that no longer
  exists in the configurations list — the system MUST silently repair the
  pointer to point at the first available configuration on next load.
- A model configuration is loaded with missing required fields (e.g.,
  `id`, `name`, `model`, `format`) — the system MUST populate sensible
  defaults (`"default"`, `"Default Configuration"`, `"gpt-3.5-turbo"`,
  `"text"`) instead of failing.
- The user enters a temperature outside the 0.0-2.0 range during "Add
  Model Configuration" — the menu MUST reject the value, report the
  violation, and substitute the default 1.0.
- The user enters non-numeric input for max tokens — the menu MUST treat
  the value as unspecified (unlimited) rather than failing.
- An API key field is found already in encrypted form during a save —
  the system MUST NOT double-encrypt it. Symmetrically, a plaintext value
  found during a load MUST NOT be passed through the decryption routine.
- Settings are encrypted on one machine and read on another — the
  cross-platform encryption derives keys from machine-specific entropy,
  so values encrypted with the non-Windows scheme on machine A MAY NOT
  decrypt on machine B; the system MUST log decryption failures and
  return the encrypted form so other settings remain usable.
- The DPAPI scheme is requested on a non-Windows host — the platform
  check MUST prevent instantiation and surface a clear platform-not-
  supported error.

## Requirements *(mandatory)*

### Functional Requirements

#### CLI surface

- **FR-001**: The CLI MUST expose a `--config` switch that, when present,
  redirects execution into an interactive configuration session instead of
  dispatching a prompt.
- **FR-002**: When `--config` is supplied, the CLI MUST bypass the normal
  prompt-source validation (so `--prompt`, `--file`, and stdin are not
  required and not validated).
- **FR-003**: The interactive session MUST return exit code 0 on a clean
  exit from the menu, and exit code 4 (unknown error) if it terminates by
  unhandled exception.

#### Main menu

- **FR-010**: The configuration session MUST present a top-level menu that
  loops until the user selects "Exit", with the following choices:
  - Add Model Configuration
  - Remove Model Configuration
  - Remove All Models
  - List Model Configurations
  - Set Default Model Configuration
  - LiteLLM Proxy
  - Set API Key for LiteLLM Proxy Models
  - Exit
- **FR-011**: Selecting "Exit" MUST terminate the loop and report
  configuration saved.

#### Add / remove / list / set default

- **FR-020**: "Add Model Configuration" MUST prompt for ID, name, API key
  (masked input), base URL (with empty meaning "use the API client's
  default"), model name (default `gpt-3.5-turbo`), temperature (default
  1.0), optional max tokens, output format (selectable from `text` or
  `json`), and streaming preference (default off).
- **FR-021**: "Add Model Configuration" MUST reject an ID that already
  exists and leave the existing configuration unchanged.
- **FR-022**: "Add Model Configuration" MUST clamp invalid temperatures
  (outside 0.0-2.0) back to the default value of 1.0 and inform the user.
- **FR-023**: Newly added configurations MUST be of type "Generic".
- **FR-030**: "Remove Model Configuration" MUST require explicit
  confirmation before deletion.
- **FR-031**: When the removed entry was the default, the system MUST
  promote the first remaining configuration to default, or clear the
  default-id pointer if no configurations remain.
- **FR-040**: "Remove All Models" MUST require explicit confirmation,
  remove every configuration, and clear the default-id pointer.
- **FR-050**: "List Model Configurations" MUST display ID, Name, Type
  (rendered as either Generic or LiteLLM Proxy), Model, Base URL (rendered
  as "Default" when empty), and a visual marker of the default
  configuration.
- **FR-060**: "Set Default Model Configuration" MUST present the list of
  configurations, persist the user's selection, and short-circuit (with a
  friendly message) when only one configuration exists.

#### LiteLLM proxy integration

- **FR-070**: "LiteLLM Proxy" MUST ask for a proxy URL (offering the
  default `http://localhost:4000`), query the proxy's `/models` endpoint
  via HTTP GET, and parse the response as a list of model entries with
  `id` and `owned_by` fields.
- **FR-071**: The menu MUST present the discovered models in a table and
  require explicit confirmation before importing.
- **FR-072**: Each imported model MUST become a configuration with:
  - `Id` = `litellm-<model-id>`
  - `Name` = `LiteLLM: <model-id>`
  - `Type` = LiteLLM Proxy
  - `BaseUrl` = the proxy URL
  - `Model` = the model id
  - `Temperature` = 1.0, `MaxTokens` = null, `Format` = `text`,
    `Stream` = false, `ApiKey` = null
- **FR-073**: When a target ID already exists, the import MUST skip that
  model and report the skip count.
- **FR-074**: Connection failures, non-success HTTP status codes, and
  empty model lists MUST be handled gracefully without partial state
  changes.
- **FR-080**: "Set API Key for LiteLLM Proxy Models" MUST show the
  current Set/Not Set state for every LiteLLM-proxy configuration, accept
  a masked input (with an empty submission meaning "clear the key"),
  require explicit confirmation, and then apply the resulting value to
  every LiteLLM-proxy configuration.

#### Persistence

- **FR-090**: User settings MUST persist to a single JSON file located at
  a platform-appropriate path:
  - Windows: `%APPDATA%\ai-cli\settings.json`
  - macOS: `~/Library/Application Support/ai-cli/settings.json`
  - Linux/other Unix: `$XDG_CONFIG_HOME/ai-cli/settings.json` or, when
    `XDG_CONFIG_HOME` is unset, `~/.config/ai-cli/settings.json`
- **FR-091**: The persistence layer MUST create the containing directory
  on first save if it does not already exist.
- **FR-092**: On Unix-family hosts, the settings file MUST be written
  with owner-read / owner-write permissions only (mode 600).
- **FR-093**: A missing, empty, or unparseable settings file MUST yield
  default settings rather than an error.
- **FR-094**: On load, a default-id pointing at a non-existent
  configuration MUST be replaced with the first valid configuration's id
  (or cleared if none remain).
- **FR-095**: On load, missing required model-configuration fields
  (`Id`, `Name`, `Model`, `Format`) MUST be populated with defaults.

#### Encryption at rest

- **FR-100**: Properties marked as encrypted MUST be encrypted before
  serialization and decrypted after deserialization, automatically and
  uniformly across every model configuration entry in the file.
- **FR-101**: The property-level marker MUST be applicable to properties
  only and not allow duplicate application on the same property.
- **FR-102**: On Windows, encryption MUST use the OS-provided per-user
  data protection facility (DPAPI) with a known application-specific
  entropy, scoped to the current user. Ciphertext values MUST be
  identifiable by a `DPAPI:` prefix.
- **FR-103**: On non-Windows hosts, encryption MUST use AES with a
  256-bit key derived (via PBKDF2 with SHA-256, 10000 iterations) from
  machine- and user-specific entropy, with a fresh IV per encryption.
  Ciphertext values MUST be identifiable by an `ENC:` prefix.
- **FR-104**: The encryption mechanism MUST be idempotent: a value that
  is already in encrypted form (carries one of the recognised prefixes)
  MUST NOT be re-encrypted on save, and a value that is already in
  plaintext form MUST NOT be passed through decrypt on load.
- **FR-105**: Decryption failures MUST be logged and MUST NOT crash the
  application; the affected field MAY be returned in its encrypted form
  so other settings remain usable.
- **FR-106**: Instantiating the DPAPI-based service on a non-Windows
  host MUST fail fast with a clear platform-not-supported error.

#### Operational integration

- **FR-110**: When the CLI runs in normal (prompt) mode, the default
  model configuration's API key MUST be available decrypted at runtime
  for use by the AI client, and the API key MUST NEVER be written to logs
  at Information level.
- **FR-111**: When CLI options are provided alongside a default
  configuration, the CLI option values MUST override the corresponding
  configuration-supplied defaults for the duration of the invocation
  without altering the persisted configuration.
- **FR-112**: When no model configuration exists at prompt-mode launch,
  the CLI MUST exit with the invalid-arguments code (1) and instruct the
  user to run `--config`.

### Key Entities

- **User Settings**: The root container persisted to disk. Holds the
  list of model configurations, the identifier of the default
  configuration, and a refresh-interval value reserved for future file
  monitoring.
- **Model Configuration**: A named provider+model preset. Carries an
  identifier, display name, type (Generic or LiteLLM Proxy), API key
  (encrypted at rest), base URL, model name, temperature, max-tokens,
  format (`text` or `json`), and streaming preference.
- **Model Type**: An enumeration distinguishing user-defined "Generic"
  configurations from those auto-imported from a LiteLLM proxy
  ("LiteLLM Proxy"). The type controls bulk-update eligibility for the
  "Set API Key for LiteLLM Proxy Models" command.
- **LiteLLM Model**: A model entry returned by a LiteLLM proxy's
  `/models` endpoint, carrying an id, object marker, creation timestamp,
  and owner string. Used only as an import source.
- **Encrypted Property Marker**: A property-level marker recognised by
  the persistence layer to indicate that the field must be transformed
  through the encryption service on save and load.
- **Encryption Service**: An abstraction over a string-in / string-out
  encryption pipeline with three operations: encrypt, decrypt, and
  is-encrypted detection. The system selects a platform-appropriate
  implementation at startup.
- **Settings Path Provider**: A pure function that maps "give me the
  settings file" to a platform-appropriate filesystem path, with an
  override for tests and advanced users.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a clean install, a new user can complete first-time
  configuration of a single model and dispatch their first prompt
  successfully within a single `--config` session, with no manual
  editing of any file on disk.
- **SC-002**: After a `--config` session that saves an API key, no
  plaintext copy of that API key exists in the user's settings file
  (verified by scanning the file content for the value that was typed
  in). The stored form carries either a `DPAPI:` or `ENC:` prefix.
- **SC-003**: An invocation in prompt mode immediately following a
  configuration change picks up the new default values (model, base
  URL, API key, temperature, max tokens, format, streaming) without
  process restart of the configuration step (i.e., a separate process
  invocation reads the just-saved file).
- **SC-004**: Importing N models from a reachable LiteLLM proxy yields
  exactly N new "LiteLLM Proxy" configurations on a clean install, and
  yields zero duplicates on a re-import (the second import reports N
  skipped, 0 added).
- **SC-005**: After the user chooses to clear the API key on all LiteLLM
  proxy configurations, the "Current API Key Status" column reads
  "Not Set" for every such entry on next view.
- **SC-006**: A corrupted or truncated settings file does not prevent
  the CLI from starting; the configuration session always opens to an
  empty (default) state in that case and writes a clean file on the
  first save.
- **SC-007**: On Unix-family hosts, the settings file is created with
  permissions equivalent to `0600` (owner read/write only), as observed
  by an external check.
- **SC-008**: 100% of automated tests covering the persistence and
  encryption layers pass on both Windows and non-Windows hosts (DPAPI
  tests self-skip off Windows; AES tests run everywhere).

## Assumptions

- The CLI runs on .NET 9 in a console environment with TTY input
  available; the interactive menu uses Spectre.Console and is not
  intended to be driven from a non-interactive pipe.
- There is exactly one settings file per OS user; multi-user shared
  configurations are not in scope.
- Encryption keys are derived from machine/user entropy, so encrypted
  settings are tied to a particular machine-user pair. Moving a settings
  file to another machine is not supported and will surface as decryption
  failures (logged, not fatal).
- The LiteLLM proxy is reachable on plain HTTP from the host running the
  CLI and exposes an OpenAI-compatible `/models` endpoint that returns a
  `data` array of model entries.
- The HTTP client used for proxy discovery applies a 30-second timeout
  and does not require authentication on the `/models` call.
- API keys captured in the menu are entered as plain strings; the menu
  takes responsibility for masking the entry in the terminal but does
  not validate the key against the remote provider.
- Configuration changes take effect on the next CLI invocation, not
  hot-reloaded mid-process. The `RefreshInterval` field is reserved on
  the model and not actively consumed by this feature.

## Retrospec Metadata

**Generated**: 2026-05-29

**Source**: Reverse-engineered from existing implementation

**Analyzed files**: 14 source files (8 production, 6 test) across the
`ai-cli` and `ai-cli.Tests` projects

**Reference implementation branch**: `feature/nodejs`
