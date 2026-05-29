# Quickstart: Model and Generation Parameters

This guide shows how to pick the AI model and shape its generation
behaviour from the command line using `ai-cli`.

## Prerequisites

- `ai-cli` is built or installed and on your `PATH`.
- You have configured at least one model configuration via `ai-cli --config`
  so that an API key and base URL are known. (This is required by
  `Program.HandleCommandAsync`; without it the CLI exits with
  `"No model configuration found in settings."`.)

## The three switches

```
  -m, --model <model>          AI model to use [default: gpt-3.5-turbo]
  --temperature <temperature>  Temperature for generation (0.0 to 2.0) [default: 1]
  --max-tokens <max-tokens>    Maximum number of tokens to generate
```

Each switch is independent and can be combined freely.

## Minimal working examples

```bash
# Use the default model (or whatever your active model configuration sets):
ai-cli --prompt "Hello"

# Pick a specific model with the long-form switch:
ai-cli --prompt "Summarise this paragraph" --model gpt-4

# Use the short alias:
ai-cli -p "Summarise this paragraph" -m gpt-4

# Windows forward-slash style works the same:
ai-cli /p "Summarise this paragraph" /m gpt-4

# Make the response more deterministic:
ai-cli -p "Translate to French: hello" --temperature 0.0

# Cap response length:
ai-cli -p "Write a haiku about debugging" --max-tokens 60

# Combine all three:
ai-cli -p "Explain monads" -m gpt-4 --temperature 0.2 --max-tokens 200
```

## Defaults and override order

For each parameter, the value used is the first non-empty in this list:

1. The value supplied on the command line.
2. The value from your active model configuration (set via `--config`).
3. The built-in default:
   - `gpt-3.5-turbo` for `--model`
   - `1.0` for `--temperature`
   - field omitted entirely for `--max-tokens`

> Implementation note: `--model` and `--temperature` use sentinel-default
> comparison to detect "user did not override", so passing the exact
> default value (`-m gpt-3.5-turbo` or `--temperature 1`) currently
> behaves the same as omitting the switch. See `research.md → Decision 5`.

## Validation

- `--temperature` must be in `[0.0, 2.0]`. Out-of-range values exit with
  code `1` and the message `"Temperature must be between 0.0 and 2.0"`.
- Non-numeric `--temperature` exits with code `1` and the message
  `"Invalid temperature value. Must be a number."`.
- `--temperature` parsing is locale-independent: write `0.5`, never
  `0,5`, regardless of your OS locale.
- `--max-tokens` accepts any integer; the provider validates the value.
- `--model` accepts any string; the provider validates the identifier.

## How to verify it works

1. **Verify model selection**:
   ```bash
   ai-cli -p "What model are you?" -m gpt-4
   ```
   Inspect the response (or the raw JSON with `--format json`) and
   confirm the `model` field echoes back `gpt-4` (or the alias your
   provider maps it to).

2. **Verify temperature validation**:
   ```bash
   ai-cli -p "Hi" --temperature 3.0
   # Expected: exit code 1, error "Temperature must be between 0.0 and 2.0",
   # no network request issued.
   ```

3. **Verify max-tokens cap**:
   ```bash
   ai-cli -p "Write a long essay about transformers" --max-tokens 30
   # Expected: response is visibly truncated at roughly 30 tokens.
   ```

4. **Verify streaming carries the parameters**:
   ```bash
   ai-cli -p "Count to ten slowly" --stream -m gpt-4 --temperature 0 --max-tokens 50
   ```
   Tokens stream incrementally; the same model/temperature/cap apply.

## Where the values live in the code

| Surface | Field | File |
|---------|-------|------|
| CLI options | `Model`, `Temperature`, `MaxTokens` | `src/ai-cli/Models/CliOptions.cs` |
| Request DTO | `Model`, `Temperature`, `MaxTokens` | `src/ai-cli/Models/AIRequest.cs` |
| HTTP payload | `model`, `temperature`, `max_tokens` (snake-case) | `src/ai-cli/Infrastructure/OpenAIClient.cs` |
| Persisted defaults | `Model`, `Temperature`, `MaxTokens` | `src/ai-cli/Models/UserSettings.cs` (`ModelConfiguration`) |
