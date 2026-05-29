# Contract: CLI Generation Options

**Source**: `src/ai-cli/CLI/CommandLineBuilder.cs`

This contract describes the command-line surface for selecting a model
and tuning generation behaviour.

## Switches

| Switch | Aliases | Argument | Default | Description |
|--------|---------|----------|---------|-------------|
| `--model` | `-m`, `/m` | `<model>` (string) | `gpt-3.5-turbo` | AI model identifier to use for the request. |
| `--temperature` | — | `<temperature>` (float) | `1.0` | Sampling temperature in the inclusive range `[0.0, 2.0]`. |
| `--max-tokens` | — | `<max-tokens>` (int) | unset (null) | Maximum number of tokens the provider may generate. |

## Parsing rules

1. **Model**: any string is accepted; no whitelist is enforced by the CLI.
2. **Temperature**: parsed with a custom `parseArgument` that uses
   `System.Globalization.CultureInfo.InvariantCulture` and
   `NumberStyles.Float`. Locale-specific forms (e.g. `0,5` on locales that
   use comma as decimal separator) are rejected with the parse error
   `"Invalid temperature value. Must be a number."`.
3. **Max tokens**: parsed by the default `System.CommandLine` `int?`
   binder. The CLI does not enforce positivity.

## Validation rules

Implemented as a `RootCommand.AddValidator(...)` callback.

| Rule | Error Message | Exit Code |
|------|---------------|-----------|
| Temperature must be `>= 0.0` and `<= 2.0` | `"Temperature must be between 0.0 and 2.0"` | `1` (Invalid arguments) |
| Non-numeric temperature | `"Invalid temperature value. Must be a number."` | `1` (Invalid arguments) |

Validation occurs **before** any API call. When validation fails,
`Program.Main` writes the parse errors to `stderr`, lists the offending
arguments, and exits with `ExitCodes.InvalidArguments` (`1`).

## Output of parsing

`CommandLineBuilder.ParseOptions(ParseResult)` returns a `CliOptions`
with:

```text
options.Model       = the parsed string, or "gpt-3.5-turbo" if not provided
options.Temperature = the parsed float (already range-checked)
options.MaxTokens   = the parsed int? (null if not provided)
```

## Examples

```
ai-cli --prompt "Hi"                                  # all defaults
ai-cli --prompt "Hi" -m gpt-4                         # model override (short)
ai-cli --prompt "Hi" --model gpt-4 --temperature 0.2  # deterministic
ai-cli --prompt "Hi" --max-tokens 200                 # cap output length
ai-cli --prompt "Hi" -m gpt-4 --temperature 0.7 --max-tokens 100   # all three
ai-cli --prompt "Hi" /m gpt-4                         # Windows forward-slash
```

## Test coverage

See `src/ai-cli.Tests/CLI/CommandLineBuilderTests.cs`:

- `ParseOptions_WithInlinePrompt_ShouldParseCorrectly` — `--model gpt-4` is captured.
- `ParseOptions_WithFilePrompt_ShouldParseCorrectly` — `--temperature 0.5` is captured.
- `ParseOptions_WithAllOptions_ShouldParseCorrectly` — model + temperature + max-tokens together.
- `CreateRootCommand_WithInvalidTemperature_ShouldFail` — rejects `3.0`.
- `ParseOptions_AllModelSyntaxVariations_ShouldParseCorrectly` — `-m`, `/m`, `--model` all parse identically.
