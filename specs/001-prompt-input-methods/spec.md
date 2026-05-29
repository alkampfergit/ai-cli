# Feature Specification: Prompt Input Methods

**Feature Branch**: `001-prompt-input-methods`

**Created**: 2026-05-28

**Status**: Draft (reverse-engineered from existing implementation)

**Input**: User description: "Accept the prompt from inline `-p/--prompt`, a file via `-f/--file`, or stdin (mutually exclusive sources). Includes interactive stdin handling."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send a prompt inline from the command line (Priority: P1)

A user invokes the CLI and supplies the entire prompt text directly on the
command line so they can quickly send a one-off question to the AI service
without creating a file or piping input.

**Why this priority**: This is the fastest, most discoverable path to value
for ad-hoc prompts and is what new users try first based on `--help` output.
Without it, the tool is not usable as a quick command-line assistant.

**Independent Test**: Invoke the CLI with `--prompt "Hello, world!"`,
confirm the AI receives exactly that text as its prompt, and confirm a
response is returned through the normal output channel. Verifiable in
isolation from file input and stdin input.

**Acceptance Scenarios**:

1. **Given** the user has a valid model configuration, **When** they run
   the CLI with `-p "Hello"`, **Then** the AI service receives the literal
   text "Hello" as its prompt and the response is written to standard
   output.
2. **Given** the user prefers the long-form switch, **When** they run the
   CLI with `--prompt "Hello"`, **Then** behavior is identical to using
   the short alias `-p`.
3. **Given** the user is on Windows and uses the forward-slash convention,
   **When** they run the CLI with `/p "Hello"`, **Then** the prompt is
   accepted exactly as with `-p` or `--prompt`.

---

### User Story 2 - Send a prompt from a file (Priority: P1)

A user has a longer or pre-prepared prompt stored in a text file and wants
the CLI to read that file's contents and use it as the prompt to the AI
service.

**Why this priority**: Long, multi-line, or reusable prompts (templates,
code snippets, complete documents) cannot be cleanly expressed inline on
the command line. File input is essential for any non-trivial usage.

**Independent Test**: Create a temporary file with known content, invoke
the CLI with `--file <path>`, confirm the AI receives the file's contents
as its prompt, and confirm the response is returned. Verifiable
independently of inline and stdin input.

**Acceptance Scenarios**:

1. **Given** a readable text file containing prompt text exists at a path,
   **When** the user runs the CLI with `-f <path>`, **Then** the AI service
   receives the file's full contents as its prompt.
2. **Given** the path passed to `-f` does not exist, **When** the user
   invokes the CLI, **Then** the CLI reports a file-not-found error,
   does not contact the AI service, and exits with the file-error exit
   code.
3. **Given** a prompt was processed from a file, **When** the request
   completes (streaming or non-streaming), **Then** the source prompt
   file is deleted from disk so transient prompt files do not accumulate.
4. **Given** the source prompt file cannot be deleted after processing
   (e.g., permission error), **When** the CLI finishes, **Then** the
   failure is logged as a warning but does not change the exit code and
   does not lose the AI response.

---

### User Story 3 - Send a prompt by piping data through stdin (Priority: P1)

A user wants to compose ad-hoc command-line pipelines, e.g.
`cat notes.md | ai-cli`, so they can feed the output of any other tool
into the AI service as the prompt.

**Why this priority**: Pipe support is what makes the CLI a first-class
Unix-style citizen and unlocks integration with editors, scripts, and
other tools. It is core to the value proposition.

**Independent Test**: Pipe known content to the CLI without specifying
`--prompt` or `--file`, confirm the AI receives the piped content as its
prompt, and confirm the response is returned. Verifiable independently of
the other input modes.

**Acceptance Scenarios**:

1. **Given** the user pipes text to the CLI and provides no `--prompt`
   and no `--file`, **When** the CLI runs, **Then** the piped content is
   read in full from standard input and sent to the AI service as the
   prompt.
2. **Given** stdin is redirected, **When** the CLI reads it, **Then** it
   consumes the entire stream (including embedded newlines) before
   issuing the AI request.

---

### User Story 4 - Type a prompt interactively when no other source is given (Priority: P2)

A user runs the CLI in a terminal without `--prompt`, without `--file`,
and without piping anything. The CLI lets them type their prompt
interactively, line by line, and submits it when they signal end-of-input.

**Why this priority**: This is a fallback for users who launch the CLI
without thinking about input mode. It also enables a simple REPL-like
experience for multi-line prompts. It is a P2 rather than P1 because
power users typically prefer `-p`, `-f`, or pipes.

**Independent Test**: Run the CLI in an interactive terminal (stdin not
redirected) with no input flags, type one or more lines, signal
end-of-input, and confirm the typed text is sent as the prompt and a
response is returned.

**Acceptance Scenarios**:

1. **Given** stdin is attached to a terminal (not redirected) and no
   prompt source flag was supplied, **When** the user runs the CLI,
   **Then** the CLI waits for keyboard input and reads it line by line.
2. **Given** the user is typing interactively, **When** they signal
   end-of-input (e.g., Ctrl+D on Unix, Ctrl+Z then Enter on Windows),
   **Then** the accumulated text (with trailing whitespace trimmed) is
   submitted to the AI service.

---

### User Story 5 - Reject ambiguous combinations of prompt sources (Priority: P2)

A user mistakenly supplies more than one explicit prompt source (e.g.,
both `--prompt` and `--file`). The CLI refuses to guess and reports a
clear error.

**Why this priority**: Without this guard, behavior would silently depend
on argument order or implementation detail, leading to confusion and
silent loss of one of the inputs. Important for trustworthiness, but
secondary to the happy-path stories above.

**Independent Test**: Invoke the CLI with both `--prompt` and `--file`
specified, confirm parsing fails before any AI call is made, confirm
the error message names the conflicting sources, and confirm the
process exits with the invalid-arguments exit code.

**Acceptance Scenarios**:

1. **Given** the user passes both `--prompt` and `--file` on the same
   invocation, **When** the CLI parses the arguments, **Then** parsing
   fails with a message stating that only one prompt source can be
   specified, and no AI request is issued.
2. **Given** the user invokes configuration mode (`--config`), **When**
   the CLI parses arguments, **Then** prompt-source validation is
   skipped because the command does not produce an AI request.

---

### Edge Cases

- **Empty inline prompt**: If `--prompt ""` is provided (explicitly
  empty), the source is treated as not provided and the next source in
  the resolution order is attempted (file, then stdin).
- **Empty file**: A `--file` pointing at an existing but empty file is
  read successfully and an empty string is sent as the prompt (the AI
  service decides how to respond).
- **No source at all in non-interactive context**: If nothing is piped
  in, no flag is given, and stdin is redirected (e.g., from `/dev/null`),
  the CLI raises an error indicating no prompt source was specified.
- **File path with leading/trailing whitespace**: Path is used verbatim;
  the user is expected to quote paths correctly.
- **File deleted between validation and read**: Surfaces as a
  file-not-found error to the user.
- **Very large file or piped stream**: The full content is read into
  memory before issuing the request; there is no streaming of the
  prompt itself.
- **Mixed CLI syntax**: Short (`-p`), long (`--prompt`), and Windows-style
  (`/p`) aliases are equivalent for the same option. Mixing aliases on
  one invocation is allowed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CLI MUST accept a prompt provided inline as an argument
  to a dedicated option.
- **FR-002**: The CLI MUST accept a path to a text file whose contents
  become the prompt.
- **FR-003**: The CLI MUST accept a prompt streamed in on standard input
  when no explicit source flag is provided.
- **FR-004**: The CLI MUST treat the three prompt sources as mutually
  exclusive. If the user provides more than one explicit source (inline
  AND file), the CLI MUST refuse the invocation with a descriptive error
  before contacting the AI service.
- **FR-005**: The CLI MUST resolve sources in a deterministic order when
  validation has passed: inline prompt first, then file, then stdin.
- **FR-006**: When the source is a file, the CLI MUST report a clear,
  user-visible error if the file does not exist and MUST NOT contact the
  AI service.
- **FR-007**: When the source is a file, after the AI request completes
  the CLI MUST delete the prompt file from disk. Failure to delete the
  file MUST be logged but MUST NOT cause the AI response to be lost.
- **FR-008**: When stdin is the source AND stdin is attached to an
  interactive terminal, the CLI MUST collect input line by line until
  end-of-input is signalled, then submit the accumulated text (with
  trailing whitespace trimmed) as the prompt.
- **FR-009**: When stdin is the source AND stdin is redirected (piped or
  redirected from a file), the CLI MUST read the entire stream to
  completion before submitting it as the prompt.
- **FR-010**: The CLI MUST expose the inline prompt option as a stable
  short alias (`-p`), a long form (`--prompt`), and a Windows-style alias
  (`/p`), all equivalent.
- **FR-011**: The CLI MUST expose the file prompt option as a stable
  short alias (`-f`), a long form (`--file`), and a Windows-style alias
  (`/f`), all equivalent.
- **FR-012**: The CLI MUST skip prompt-source validation when invoked in
  configuration mode, because configuration mode does not issue an AI
  request.
- **FR-013**: When no prompt source can be resolved (no flag, redirected
  but empty stdin handling not triggered, configuration mode not
  selected), the CLI MUST surface a clear error rather than send an
  empty request to the AI service.
- **FR-014**: The chosen prompt text MUST be passed to both the
  non-streaming and the streaming AI request paths identically; the
  prompt-input feature MUST NOT depend on the response delivery mode.

### Key Entities *(include if feature involves data)*

- **CLI invocation**: A single user command line containing zero or one
  inline prompt, zero or one file reference, and an implicit stdin
  channel; carries other generation options (model, temperature, etc.)
  but those are orthogonal to prompt source selection.
- **Prompt source**: One of three logical sources (`Inline`, `File`,
  `Stdin`). At most one is the effective source for any given
  invocation.
- **Prompt text**: The final string handed to the AI request. Always
  derived from exactly one prompt source.
- **Prompt file**: A file on the local filesystem whose contents serve
  as the prompt text and which is deleted after a successful request.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of CLI invocations using `--prompt`, `-p`, or `/p`
  with a non-empty string deliver exactly that string as the prompt to
  the AI service.
- **SC-002**: 100% of CLI invocations using `--file`, `-f`, or `/f`
  with a readable file deliver exactly that file's contents as the
  prompt to the AI service.
- **SC-003**: 100% of CLI invocations that pipe data on stdin (no
  conflicting flag) deliver the full stdin payload as the prompt.
- **SC-004**: 100% of CLI invocations that combine `--prompt` and
  `--file` are rejected at argument-parse time with a non-zero exit
  code and a message naming the conflict; no AI request is sent.
- **SC-005**: 100% of `--file` invocations whose target file is missing
  fail with a file-not-found error and the file-error exit code; no AI
  request is sent.
- **SC-006**: After a successful `--file` run, the source prompt file is
  no longer present on disk in 100% of cases where the process has
  delete permission.
- **SC-007**: An interactive user who types a single line and ends input
  has that line delivered as the prompt with no leading/trailing blank
  lines beyond what they typed.

## Assumptions

- The CLI is a single-shot command-line tool; multi-turn conversation
  state is out of scope for this feature.
- The local filesystem is available and the process has read (and
  ideally delete) permission on any file referenced via `--file`.
- The full prompt fits comfortably in memory; no streaming or chunking
  of the prompt itself is required.
- Aliases follow the existing project convention of supporting `-x`,
  `--name`, and `/x` for every primary option.
- Configuration mode (`--config`) is the only first-class invocation
  that intentionally produces no AI request; all other invocations are
  expected to resolve to exactly one prompt source.
- Argument parsing is performed by the System.CommandLine library
  conventions already in use by the CLI; behavioral edge cases of that
  parser (e.g., handling of empty string values) are inherited.
- Deletion of the source prompt file after processing is intentional and
  considered a feature (treating `--file` inputs as transient
  scratchpads). This may surprise users; it is documented here as part
  of the contract.

## Retrospec Metadata

**Generated**: 2026-05-28
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 6 source files + 2 test files across the `ai-cli` and
`ai-cli.Tests` projects
**Reference implementation branch**: feature/nodejs

**Discovered feature map**:

- Core implementation
  - `src/ai-cli/Application/IPromptService.cs` — service contract
  - `src/ai-cli/Application/PromptService.cs` — resolves prompt text
    from the three sources and invokes the AI client
- CLI surface
  - `src/ai-cli/CLI/CommandLineBuilder.cs` — defines `--prompt`/`-p`/`/p`
    and `--file`/`-f`/`/f` options, validates mutual exclusivity, and
    sets `UseStdin` when neither flag is given and stdin is not
    redirected
  - `src/ai-cli/Models/CliOptions.cs` — `Prompt`, `FilePath`, `UseStdin`
    properties
- Entry point
  - `src/ai-cli/Program.cs` — wires options into `IPromptService` and
    handles `FileNotFoundException` -> file-error exit code
- Tests
  - `src/ai-cli.Tests/Application/PromptServiceTests.cs` — inline, file,
    missing-file, no-source, and streaming variants
  - `src/ai-cli.Tests/CLI/CommandLineBuilderTests.cs` — parsing of
    `--prompt`, `-p`, `/p`, `--file`, `-f`, `/f`, including mixed
    syntax
