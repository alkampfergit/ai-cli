# Data Model: Prompt Input Methods

This document captures the data shapes involved in resolving a user's
prompt from one of three sources. Field-level descriptions are extracted
from `src/ai-cli/Models/CliOptions.cs`,
`src/ai-cli/CLI/CommandLineBuilder.cs`, and
`src/ai-cli/Application/PromptService.cs`.

## Entity: CliOptions (relevant subset)

C# type: `AiCli.Models.CliOptions` (`src/ai-cli/Models/CliOptions.cs`)

Only the fields that participate in prompt-source resolution are listed.
Other fields (`Model`, `Temperature`, `MaxTokens`, `OutputFile`,
`Format`, `Stream`, `Config`) are documented in their own features.

| Field | Type | Source | Required | Default | Notes |
|-------|------|--------|----------|---------|-------|
| `Prompt` | `string?` | `--prompt`, `-p`, `/p` | No | `null` | Inline prompt text as provided on the command line. Treated as "not provided" when null or empty. |
| `FilePath` | `string?` | `--file`, `-f`, `/f` | No | `null` | Filesystem path whose contents become the prompt. Treated as "not provided" when null or empty. |
| `UseStdin` | `bool` | derived in `CommandLineBuilder.ParseOptions` | No | `false` | True if `Prompt` and `FilePath` are both empty AND `Console.IsInputRedirected` is `false`, i.e. the user is in interactive mode. |

### Derivation rules

- `UseStdin` is **not** a user-facing flag — there is no `--stdin`
  option. It is computed at parse time.
- The conjunction means: `UseStdin` is true only when there is *no*
  inline prompt, *no* file, and stdin is attached to a terminal. This
  encodes "fall through to interactive mode" semantics.

### Validation rules (enforced in `CommandLineBuilder.CreateRootCommand`)

- If both `Prompt` and `FilePath` are non-empty, the parser produces an
  error: `"Only one prompt source can be specified: --prompt, --file, or stdin"`.
- Validation is skipped when `--config` is present (`isConfigMode` short-
  circuit), because configuration mode does not consume a prompt.
- Validation does *not* require any source to be present; absence of a
  source is allowed at parse time and resolved later by `PromptService`.

## Entity: Effective Prompt Source

Not a class — a logical concept used by `PromptService.GetPromptTextAsync`.

Conceptual values: `Inline | File | Stdin-Interactive | Stdin-Redirected | None`

Resolution algorithm (in order, first match wins):

1. `Inline` — `!string.IsNullOrEmpty(options.Prompt)` → return
   `options.Prompt`.
2. `File` — `!string.IsNullOrEmpty(options.FilePath)`. If the file does
   not exist, throw `FileNotFoundException`. Otherwise return
   `File.ReadAllTextAsync(options.FilePath)`.
3. `Stdin-Interactive` — `options.UseStdin` is `true` and
   `!Console.IsInputRedirected`. Read lines via `Console.ReadLine()` in
   a loop until null (EOF), accumulating into a `StringBuilder` and
   appending a line break per line; return `builder.ToString().TrimEnd()`.
4. `Stdin-Redirected` — `options.UseStdin` is `true` and
   `Console.IsInputRedirected` is `true`. Read the full stream via
   `StreamReader(Console.OpenStandardInput()).ReadToEndAsync` and
   return its content verbatim.
5. `None` — none of the above. Throw `InvalidOperationException("No prompt source specified")`.

> Note: in practice `UseStdin` will be `false` whenever `Console.IsInputRedirected` is `true` (see derivation rules above), so the `Stdin-Redirected` branch inside `PromptService` is reachable only when callers construct a `CliOptions` programmatically (e.g. tests) with `UseStdin = true` and a redirected stdin. The CLI happy path for piped input today relies on neither flag being set and the operating system delivering content on stdin while `PromptService` falls through to the `InvalidOperationException`. **Open question / known limitation** — see `research.md`.

## Entity: Prompt File (filesystem)

Not a class — an external resource.

- **Lifecycle**: created by the user (or an upstream tool), referenced
  via `--file`, read once at request time, deleted by `PromptService`
  after the AI response has been produced (non-streaming) or the
  streaming enumerator has been exhausted.
- **Failure modes**:
  - Missing at read time → `FileNotFoundException` (mapped to file-
    error exit code by `Program.HandleCommandAsync`).
  - Unreadable due to permissions → bubbles as I/O exception, mapped to
    the access-error / file-error exit code in `Program`.
  - Undeletable after read → `_logger.LogWarning` only; response is
    still returned to the user.

## Relationships

```
CliOptions ──drives──> PromptService.GetPromptTextAsync ──produces──> string prompt
       │                                                          │
       │                                                          └──> AIRequest.Prompt
       │
       ├──Prompt──────────────> (inline source)
       ├──FilePath─────────────> Prompt File ──(deleted after use)
       └──UseStdin (derived)───> Console standard input (line loop)
```

## State transitions

A `CliOptions` value transitions through three states during one
invocation:

1. **Parsed**: fields populated from arguments; `UseStdin` derived.
2. **Validated**: at most one of `Prompt`/`FilePath` non-empty; if
   neither is set and `UseStdin` is `false` and `--config` is not set,
   the invocation will fail at resolution time (deferred error).
3. **Resolved**: an effective prompt string has been produced (or a
   well-typed exception thrown).
