# Contract: CLI Prompt Options

This contract documents the user-visible command-line interface for
selecting a prompt source. It is implemented in
`src/ai-cli/CLI/CommandLineBuilder.cs` and exercised by
`src/ai-cli.Tests/CLI/CommandLineBuilderTests.cs`.

## Options

### `--prompt` (inline prompt)

| Property | Value |
|----------|-------|
| Aliases | `--prompt`, `-p`, `/p` |
| Argument | A single string. May contain spaces if quoted. |
| Required | No (only one of prompt / file / stdin is required for a real request) |
| Description (from `--help`) | "Prompt text to send to the AI service" |
| Backing CLI option type | `Option<string?>` |

**Behaviour**:

- When set with a non-empty value, this string is used verbatim as the
  prompt sent to the AI service.
- When set to an empty string (e.g. `--prompt ""`), the option is
  treated as not provided and the next source in the resolution order
  is attempted.

### `--file` (file-based prompt)

| Property | Value |
|----------|-------|
| Aliases | `--file`, `-f`, `/f` |
| Argument | Path to a readable text file. |
| Required | No |
| Description (from `--help`) | "Path to file containing the prompt" |
| Backing CLI option type | `Option<string?>` |

**Behaviour**:

- When set with a non-empty value, the referenced file is read in full
  and its contents are used as the prompt.
- The file is deleted by the application after the AI request has been
  processed (see `IPromptService` contract).

### Implicit stdin

There is no dedicated `--stdin` option. Stdin is consumed when both
`--prompt` and `--file` are absent (subject to the parsing-time
behaviour documented in `data-model.md`).

## Validation

The root command attaches a validator that fires after parsing:

1. **Config short-circuit**: If `--config` is present, skip prompt-
   source validation entirely.
2. **Source-count check**: Count non-empty `--prompt` and `--file`
   values. If the count is greater than 1, set the error message:

   > `Only one prompt source can be specified: --prompt, --file, or stdin`

3. **No-source allowance**: A count of 0 is allowed at parse time;
   resolution falls through to stdin or to a deferred error in
   `PromptService`.

## Exit codes touched by this contract

(`src/ai-cli/CLI/ExitCodes.cs` defines the underlying constants; values
shown for clarity.)

| Situation | Exit code |
|-----------|-----------|
| Successful invocation (any source) | `0` (Success) |
| Both `--prompt` and `--file` set | `1` (InvalidArguments) — surfaced by `Program.Main` after `parseResult.Errors.Count > 0` |
| `--file` path does not exist | `3` (FileError) — surfaced by `Program.HandleCommandAsync` catching `FileNotFoundException` |
| `--file` cannot be read (permissions) | `3` (FileError) — `UnauthorizedAccessException` catch |
| No source resolvable at runtime | `4` (UnknownError) — `InvalidOperationException` falls through generic catch |

## Examples (mirrored from tests)

```bash
# Inline, short alias
ai-cli -p "Hello, world!"

# Inline, long alias
ai-cli --prompt "Hello, world!"

# Inline, Windows-style alias
ai-cli /p "Hello, world!"

# File-based, mixing aliases
ai-cli /f prompt.txt --temperature 0.5

# Piped stdin
echo "Summarize this" | ai-cli

# Mixed syntax (allowed)
ai-cli /p "Test prompt" -m gpt-4 --output-file output.txt

# Conflict (rejected at parse time)
ai-cli --prompt "Hello" --file prompt.txt
#  -> "Only one prompt source can be specified: --prompt, --file, or stdin"
```
