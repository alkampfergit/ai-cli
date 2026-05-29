# Contract: CLI Output Options

**Source**: `src/ai-cli/CLI/CommandLineBuilder.cs`

This contract describes the command-line surface for rendering format,
streaming behavior, and persistent file output.

## Switches

| Switch | Aliases | Argument | Default | Description |
|--------|---------|----------|---------|-------------|
| `--format` | — | `<format>` (string) | `text` | Output format. MUST be `text` or `json`. |
| `--stream` | — | (boolean flag) | `false` | Stream response tokens as they arrive. |
| `--output-file` | `-o`, `/o` | `<output-file>` (string path) | unset (null) | Save the rendered response to a file. |

## Parsing rules

1. **Format**: parsed as `string` via `getDefaultValue: () => "text"`.
   Any non-empty token is accepted at parse time; the value is then
   range-checked by the root validator (see below).
2. **Stream**: parsed as `Option<bool>`, no argument value required —
   presence sets it to `true`, absence leaves it `false`.
3. **Output file**: parsed as `Option<string?>`; any path string is
   accepted. The CLI does **not** check the path's existence,
   writeability, or directory; failures surface at write time as I/O
   errors with exit code `3`.

## Validation rules

Implemented as a `RootCommand.AddValidator(...)` callback.

| Rule | Error Message | Exit Code |
|------|---------------|-----------|
| `--format` value MUST be `text` or `json` | `"Format must be 'text' or 'json'"` | `1` (Invalid arguments) |

Validation occurs **before** any API call. When validation fails,
`Program.Main` writes the parse errors to `stderr`, lists the offending
arguments, and exits with `ExitCodes.InvalidArguments` (`1`).

`--stream` and `--output-file` have no parse-time validation; semantic
failures (e.g. unwriteable path) surface at run time.

## Output of parsing

`CommandLineBuilder.ParseOptions(ParseResult)` returns a `CliOptions`
with:

```text
options.Format     = the parsed string ("text" if not provided; "text" or "json" after validation)
options.Stream     = the parsed boolean (false if not provided)
options.OutputFile = the parsed path (null if not provided)
```

## Resolution against active model configuration

`Program.HandleCommandAsync` resolves `Format` and `Stream` against the
active `ModelConfiguration` (CLI wins when it differs from the literal
default):

```text
effective.Format = (CLI.Format != "text") ? CLI.Format : config.Format
effective.Stream = (CLI.Stream != false)  ? CLI.Stream : config.Stream
```

`OutputFile` is not subject to configuration fallback — it is per-call.

## Examples

```
ai-cli --prompt "Hi"                                   # text, no streaming, stdout only
ai-cli --prompt "Hi" --format json                     # raw provider JSON on stdout
ai-cli --prompt "Hi" --stream                          # text chunks streamed to stdout
ai-cli --prompt "Hi" --stream --format json            # JSON-wrapped chunks streamed
ai-cli --prompt "Hi" -o ./out.txt                      # also saved to ./out.txt
ai-cli --prompt "Hi" /o C:\tmp\out.txt                 # Windows forward-slash alias
ai-cli --prompt "Hi" --output-file ./out.json --format json   # save raw JSON
ai-cli --prompt "Hi" --format xml                      # ERROR: exits with code 1
```

## Test coverage

See `src/ai-cli.Tests/CLI/CommandLineBuilderTests.cs`:

- `CreateRootCommand_ShouldCreateCommandWithAllOptions` — asserts
  `output-file`, `format`, `stream` are among the root command's
  options.
- `ParseOptions_WithAllOptions_ShouldParseCorrectly` — `--output-file`,
  `--format json`, `--stream` all parse together.
- `CreateRootCommand_WithInvalidFormat_ShouldFail` — rejects
  `--format xml` with the message `"Format must be 'text' or 'json'"`.
- `ParseOptions_AllOutputSyntaxVariations_ShouldParseCorrectly` — `-o`,
  `/o`, `--output-file` all parse identically (theory test).
- `ParseOptions_WithForwardSlashOutput_ShouldParseCorrectly` — `/o`
  alone.
