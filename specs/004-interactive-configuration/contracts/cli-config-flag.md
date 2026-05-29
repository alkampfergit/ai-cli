# Contract: `--config` CLI flag

## Surface

```
ai-cli --config
```

Declared in `src/ai-cli/CLI/CommandLineBuilder.cs`:

```csharp
_configOption = new Option<bool>(
    name: "--config",
    description: "Enter configuration mode to manage model settings")
{
    Arity = ArgumentArity.Zero
};
```

- **Name**: `--config`
- **Arity**: zero (a boolean switch; no value is parsed from the argument)
- **Type**: `Option<bool>` — present means `true`, absent means `false`
- **Default**: absent / `false`
- **Help text**: `Enter configuration mode to manage model settings`

## Behavior

1. Setting `--config` causes the root command validator to short-circuit
   the prompt-source check (`--prompt`, `--file`, stdin) and the
   `--temperature` / `--format` validators. Other options on the same
   command line are parsed but not validated.
2. `Program.HandleCommandAsync` reads the resulting `CliOptions.Config`
   boolean and, when `true`, dispatches to
   `Program.HandleConfigModeAsync` instead of the normal prompt flow.
3. `HandleConfigModeAsync`:
   - Resolves the settings path via
     `SettingsPathProvider.GetDefaultSettingsPath()`.
   - Builds a small DI graph containing logging, a platform-selected
     `IEncryptionService`, and a `FileUserSettingsService` bound to that
     path.
   - Constructs an `HttpClient` with a 30-second timeout for proxy
     discovery.
   - Instantiates `ConfigurationService` and calls
     `StartConfigurationAsync()`.
   - Returns `ExitCodes.Success` (0) on a clean exit and
     `ExitCodes.UnknownError` (4) on an unhandled exception (with the
     exception message written to stderr).

## Return codes

| Exit code | When |
|-----------|------|
| `0` (Success) | The user navigated the menu and chose "Exit". |
| `4` (UnknownError) | An unhandled exception escaped `StartConfigurationAsync`. |

`InvalidArguments` (1) is not raised by the config flow itself; the
validator bypass means any other options on the command line are ignored
rather than rejected.

## Interactive contract (the menu)

The configuration session is an infinite loop until the user selects
"Exit". Each iteration prompts via `SelectionPrompt<string>` with the
following choices, in order:

```
1. Add Model Configuration
2. Remove Model Configuration
3. Remove All Models
4. List Model Configurations
5. Set Default Model Configuration
6. LiteLLM Proxy
7. Set API Key for LiteLLM Proxy Models
8. Exit
```

After each handler completes, a blank line is written and the menu is
re-displayed.

### "Add Model Configuration" inputs

| Prompt | Type | Validation |
|--------|------|------------|
| Configuration ID | text | Rejected if duplicate; aborts the add. |
| Configuration name | text | Free-form. |
| API key | masked text (`.Secret()`) | Stored encrypted. |
| Base URL | text, default empty | Empty → null. |
| Model name | text, default `gpt-3.5-turbo` | Free-form. |
| Temperature | float, default 1.0 | Outside `[0.0, 2.0]` → clamped to 1.0 with a warning. |
| Max tokens | text, default empty | Non-numeric → null (unlimited). |
| Format | selection: `text`, `json` | Constrained. |
| Stream | confirm, default no | Boolean. |

### Confirmation flow

The following destructive or significant operations always require an
explicit confirmation:

- Remove Model Configuration (per-entry confirm)
- Remove All Models (one global confirm with the count)
- LiteLLM Proxy import (confirm after the discovery table is shown)
- Set API Key for LiteLLM Proxy Models (confirm before the bulk apply)

Declining the confirmation in any of these cases prints "Operation
cancelled." and returns to the main menu without persisting changes.

### Side effects

- Successful add / remove / set-default / import / bulk-key always calls
  `IUserSettingsService.Save(...)`.
- Save creates the parent directory on first use.
- Save sets Unix file mode 600 (best-effort; logged as warning on failure).
- Save encrypts every `[EncryptedSetting]` string property that is not
  already in encrypted form.

## Examples

### First-time setup

```
$ ai-cli --config
   ___  _____   _____  _    ____
  / _ \|_   _| |  ___|| |  |_   |
 | |_| | | |   | |__  | |    | |
 |  _  | | |   |  __| | |    | |
 |_| |_| |___|  |____||_|    |_|   AI CLI Config
What would you like to configure?
> Add Model Configuration
  Remove Model Configuration
  ...
```

### Subsequent prompt invocations

After the user creates a default configuration, `ai-cli --prompt "..."`
reads it transparently and applies its API key, base URL, model name,
temperature, max tokens, format, and stream preference unless overridden
on the command line.
