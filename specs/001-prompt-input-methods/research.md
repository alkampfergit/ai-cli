# Research & Technical Decisions: Prompt Input Methods

This is a retrospective research log — decisions visible in the existing
code are documented here with the rationale that best fits the
implementation patterns observed, plus alternatives that were *not*
chosen.

## D1. Three sources, mutually exclusive

- **Decision**: Accept the prompt from inline `--prompt`, file
  `--file`, or stdin, and reject any invocation that explicitly
  combines `--prompt` and `--file`.
- **Rationale**: A single, well-defined input avoids surprising
  precedence rules. The validator in `CommandLineBuilder` makes the
  rejection explicit and gives a precise error message.
- **Alternatives considered**:
  - **Concatenate sources** (e.g. file contents followed by inline
    text). Rejected because users have no obvious way to control
    order or separator.
  - **First-set-wins / last-set-wins** silent precedence. Rejected
    because it hides user error.

## D2. Stdin is implicit, not flagged

- **Decision**: There is no `--stdin` option. Stdin is consumed when
  no other source is given.
- **Rationale**: Matches Unix tradition of "no args means read stdin"
  and keeps the CLI surface small. The derived `UseStdin` field on
  `CliOptions` captures this internally without exposing it to the
  user.
- **Alternatives considered**:
  - **Explicit `--stdin` flag**. Rejected — adds noise to the help
    text and creates a fourth way to fail validation.

## D3. Interactive stdin reads line-by-line

- **Decision**: When `UseStdin` is true and stdin is *not* redirected
  (i.e. attached to a TTY), `PromptService` reads via
  `Console.ReadLine()` in a loop until EOF, then `TrimEnd()`s the
  result.
- **Rationale**: This gives users line-buffered, echoing input
  behaviour they expect from interactive CLIs across all OSes. Using
  `StreamReader.ReadToEndAsync` against `Console.OpenStandardInput()`
  in a TTY can behave inconsistently (no echo, awkward Ctrl+D
  semantics on some terminals).
- **Alternatives considered**:
  - **Always read via `StreamReader.ReadToEndAsync`**. Rejected for
    interactive ergonomics (see above).
  - **Use `System.CommandLine` interactive prompting** (e.g. Spectre).
    Rejected because this code path is only a fallback when no other
    source is given; a richer UX belongs in `--config` mode, not in
    every silent invocation.

## D4. Redirected stdin reads as a single stream

- **Decision**: When stdin is redirected, read the whole stream with
  `StreamReader.ReadToEndAsync`.
- **Rationale**: A redirected stream has well-defined end-of-stream
  semantics and may contain binary-style data with embedded newlines.
  `ReadToEnd` preserves it verbatim.
- **Note**: As of the current implementation `UseStdin` is set to
  `false` whenever `Console.IsInputRedirected` is `true` (see
  `CommandLineBuilder.ParseOptions`). That means the redirected
  branch is reachable in practice only from unit tests that craft
  `CliOptions` directly. Production pipe-style usage
  (`echo … | ai-cli`) currently relies on the user *not* attaching a
  TTY and *not* setting `--prompt`/`--file`; this works because the
  System.CommandLine handler still invokes `PromptService`, which
  falls into the `InvalidOperationException("No prompt source specified")` branch — unless the host shell keeps stdin marked as a TTY. **Known
  limitation / follow-up**: this branch may need re-wiring so that
  piped invocations actually reach the `Stdin-Redirected` branch.
  The current tests do not cover that pipe-from-shell scenario.

## D5. Delete the source file after use

- **Decision**: After both `ProcessPromptAsync` and
  `ProcessStreamingPromptAsync` finish, `PromptService` calls
  `File.Delete(options.FilePath)` if the file still exists.
- **Rationale**: Treats `--file` inputs as transient scratchpads —
  upstream tools that drop a prompt into a temp file and shell out
  to `ai-cli` get automatic cleanup. Asserted in tests.
- **Alternatives considered**:
  - **Always leave the file in place**. Rejected — leaks potentially
    sensitive prompt content and burdens callers with cleanup.
  - **Opt-in `--keep-file` flag**. Rejected as gold-plating for the
    current scope; documented here as a possible future enhancement.

## D6. File-deletion failures are non-fatal warnings

- **Decision**: A failed `File.Delete` is caught and logged at warning
  level; the AI response is still returned.
- **Rationale**: Cleanup is best-effort. Losing the AI response over a
  permissions issue on the source file would be worse than leaving
  the file in place.

## D7. CLI option aliases include `/x` style

- **Decision**: Every short alias (`-p`, `-f`, `-m`, `-o`) also has
  a `/x` form.
- **Rationale**: Convenience for Windows-only users who are used to
  forward-slash switches. `System.CommandLine` supports declaring
  multiple aliases on one option. Tests parametrise on all three
  variants.
- **Alternatives considered**:
  - **Dash-only aliases**. Rejected for the documented Windows-user
    preference.

## D8. Validator skipped in config mode

- **Decision**: When `--config` is set, the root command's validator
  short-circuits before checking prompt sources.
- **Rationale**: `--config` is an entirely separate mode that does
  not produce an AI request. Forcing the user to also supply a
  dummy `--prompt` would be hostile.

## D9. Same resolution path for streaming and non-streaming

- **Decision**: Both `ProcessPromptAsync` and
  `ProcessStreamingPromptAsync` call into the same private
  `GetPromptTextAsync` helper.
- **Rationale**: Keeps the source-selection contract identical
  regardless of how the response is delivered, and means streaming
  tests cover the same source-resolution code as non-streaming
  tests.
