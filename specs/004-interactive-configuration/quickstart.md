# Quickstart: Interactive Configuration

This guide walks through configuring `ai-cli` from first launch to a
working prompt against a remote model, using only the interactive
`--config` menu.

## Prerequisites

- A built `ai-cli` binary (`dotnet build src/ai-cli.sln`) or a
  published single-file executable (`dotnet publish
  src/ai-cli/ai-cli.csproj -c Release --self-contained -o publish`).
- A terminal that supports ANSI rendering (Spectre.Console requires a
  TTY for interactive prompts).
- An API key for at least one OpenAI-compatible service, or a reachable
  LiteLLM proxy.

## First-time setup

```bash
ai-cli --config
```

You are dropped into the interactive menu:

```
   ___  _____   _____  _    ____
  / _ \|_   _| |  ___|| |  |_   |
 ...
What would you like to configure?
> Add Model Configuration
  Remove Model Configuration
  Remove All Models
  List Model Configurations
  Set Default Model Configuration
  LiteLLM Proxy
  Set API Key for LiteLLM Proxy Models
  Exit
```

Pick "Add Model Configuration" and follow the prompts:

| Prompt | Example value |
|--------|---------------|
| Configuration ID | `openai-prod` |
| Configuration name | `OpenAI Production` |
| API key | `sk-...` (masked while typing) |
| Base URL | press Enter for default, or supply e.g. `https://api.openai.com/v1` |
| Model name | `gpt-4o-mini` |
| Temperature | `0.7` |
| Max tokens | press Enter for unlimited |
| Format | `text` |
| Stream | `n` (default) |

You should see:

```
Model configuration 'OpenAI Production' added successfully!
```

Pick "Exit" to leave the menu. A `settings.json` file is now present at:

| Platform | Location |
|----------|----------|
| Windows | `%APPDATA%\ai-cli\settings.json` |
| macOS | `~/Library/Application Support/ai-cli/settings.json` |
| Linux | `$XDG_CONFIG_HOME/ai-cli/settings.json`, or `~/.config/ai-cli/settings.json` |

Inspect the file (on Linux/macOS):

```bash
cat ~/.config/ai-cli/settings.json
```

You will see something like:

```json
{
  "modelConfigurations": [
    {
      "id": "openai-prod",
      "name": "OpenAI Production",
      "type": 0,
      "apiKey": "ENC:0FfQ...",
      "baseUrl": "https://api.openai.com/v1",
      "model": "gpt-4o-mini",
      ...
    }
  ],
  "defaultModelConfigurationId": "openai-prod",
  "refreshInterval": 30
}
```

Note that `apiKey` is opaque (`ENC:` prefix on Linux/macOS,
`DPAPI:` prefix on Windows) and the file permissions are `600` on
Unix-family hosts.

## Verify it works

```bash
ai-cli --prompt "Say hi in one sentence."
```

The CLI reads the default configuration, decrypts the API key in memory,
and dispatches the prompt. The response prints to stdout.

## Add a second configuration and switch defaults

```bash
ai-cli --config
```

- "Add Model Configuration" → e.g., `local-llama` with base URL
  `http://localhost:11434/v1` and model `llama3`.
- "Set Default Model Configuration" → pick `local-llama`.
- "List Model Configurations" → confirm `local-llama` is marked as
  the default.
- "Exit".

Now every subsequent `ai-cli --prompt "..."` invocation uses
`local-llama` until you change the default again. You can still
override on a per-invocation basis with `--model` on the command line.

## Importing models from a LiteLLM proxy

Assume a LiteLLM proxy is running at `http://localhost:4000` and
exposes a `/models` endpoint compatible with OpenAI's format.

```bash
ai-cli --config
```

- Select "LiteLLM Proxy".
- Enter the proxy URL (default `http://localhost:4000`).
- The menu prints a table of discovered models and asks for
  confirmation.
- Confirm.

The menu reports something like:

```
Added 12 new model configurations
```

If you re-run the same flow with no new models, the menu reports the
existing models as skipped (no duplicates are created).

Each imported configuration is:

- `Id` = `litellm-<model-id>`
- `Name` = `LiteLLM: <model-id>`
- `Type` = LiteLLM Proxy
- `BaseUrl` = the proxy URL you entered
- `ApiKey` = unset

## Apply a single API key across LiteLLM-imported configurations

Still in `--config`:

- Select "Set API Key for LiteLLM Proxy Models".
- The menu lists every LiteLLM-proxy configuration with the current
  Set / Not Set status.
- Enter the key once (masked input), confirm.
- Every LiteLLM-proxy configuration now stores the same encrypted key.

To clear the key from every LiteLLM-proxy configuration instead, press
Enter when prompted (leaving the key empty) and confirm.

## Cleaning up

- "Remove Model Configuration" → pick one, confirm. If it was the
  default, the next remaining configuration is promoted automatically.
- "Remove All Models" → confirms, then wipes every configuration and
  clears the default-id pointer.

## What to check if something doesn't work

| Symptom | Likely cause | Resolution |
|---------|--------------|------------|
| `Error: No model configuration found in settings. Please configure at least one model using --config.` | The file is empty or has no entries | Run `ai-cli --config` and add at least one configuration. |
| The CLI runs but the API call fails with an authentication error | The encrypted key was generated on a different machine | Re-enter the key via `--config` → "Add Model Configuration" (replacing the entry) or via "Set API Key for LiteLLM Proxy Models" for imported entries. |
| Logs show "Failed to decrypt property ApiKey" | Same as above, or the encryption scheme changed | Re-enter the key. |
| Settings file appears to lose entries after editing | Hand-editing without preserving the encrypted prefix can confuse the load path | Re-run the menu and re-add the configuration. Idempotent encryption means a re-save will leave existing encrypted values untouched. |

## Where to look in the code

- CLI flag: `src/ai-cli/CLI/CommandLineBuilder.cs` (`--config` option)
- Entry point: `src/ai-cli/Program.cs` → `HandleConfigModeAsync`
- Interactive menu: `src/ai-cli/Infrastructure/ConfigurationService.cs`
- Persistence: `src/ai-cli/Infrastructure/FileUserSettingsService.cs`
- Path resolution: `src/ai-cli/Configuration/SettingsPathProvider.cs`
- Models: `src/ai-cli/Models/UserSettings.cs`,
  `src/ai-cli/Models/LiteLLMModels.cs`
- Encryption marker: `src/ai-cli/Attributes/EncryptedSettingAttribute.cs`
- Crypto: `src/ai-cli/Infrastructure/{Dpapi,Aes}EncryptionService.cs`
