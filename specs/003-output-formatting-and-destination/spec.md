# Feature Specification: Output Formatting and Destination

**Feature Branch**: `003-output-formatting-and-destination`

**Created**: 2026-05-29

**Status**: Draft (reverse-engineered from existing implementation)

**Input**: User description: "Output formatting and destination control: the
`--format` option (text or json, default text) selects the response rendering
format, `--stream` enables real-time token streaming for both formats, and
`-o/--output-file` saves the response to a file. File output includes ANSI
escape sequence stripping for security and applies restrictive Unix file
permissions (600). The streaming pipeline runs through `OpenAIClient`. Capture
the rendering, streaming, and secure file output behavior as a single
capability."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the answer in human-friendly plain text on the terminal (Priority: P1)

A user runs a prompt from the terminal and wants the AI's reply rendered as
plain text in their console — no envelope, no quoting, no JSON braces — so
they can read it immediately and, if they like, pipe it into another shell
command.

**Why this priority**: Plain-text terminal output is the default rendering
and the most common usage of the CLI. Without a reliable, render-as-typed
text mode, every other format-related feature is unusable.

**Independent Test**: Invoke the CLI with `--prompt "Hello"` (no `--format`,
no `--stream`, no `-o`). The exact text of the assistant's reply is written
to standard output, with no JSON envelope and no trailing newline added
beyond what the model produced. Verifiable without any other switch.

**Acceptance Scenarios**:

1. **Given** the user runs the CLI without specifying `--format`, **When**
   the response is rendered, **Then** the assistant text appears on stdout
   as plain characters (no JSON wrapper).
2. **Given** the user explicitly passes `--format text`, **When** the
   response is rendered, **Then** the output is identical to the default
   (text mode).
3. **Given** the user pipes the output to another process, **When** the
   command completes, **Then** the downstream process receives only the
   assistant text, with no diagnostic chatter on stdout.

---

### User Story 2 - Get the full raw JSON response for programmatic consumption (Priority: P1)

A script author wants the raw provider response (including model metadata,
finish reason, token usage, and any provider-specific fields) so the
script can parse it, persist it, or branch on it.

**Why this priority**: Automation depends on a stable, structured response.
The CLI is only useful in pipelines if it can emit a machine-parseable
form of the full response, not just the visible text.

**Independent Test**: Invoke the CLI with `--prompt "Say hi" --format json`
and confirm the entire stdout is the verbatim provider JSON response (i.e.
parseable by any JSON consumer and containing the same fields the upstream
API returned). Verifiable independently of streaming and file output.

**Acceptance Scenarios**:

1. **Given** the user passes `--format json`, **When** the response is
   rendered, **Then** stdout receives the raw provider JSON response body
   (the same bytes the HTTP layer received).
2. **Given** the user passes an unrecognized format such as `--format xml`,
   **When** the CLI parses the arguments, **Then** the invocation fails
   with the message `"Format must be 'text' or 'json'"` and exit code `1`,
   with no API call issued.
3. **Given** the user passes `--format json` together with `--stream`,
   **When** the response is streamed, **Then** each token chunk is wrapped
   in a small JSON object of the shape `{"content": "<chunk>"}` and written
   to stdout incrementally.

---

### User Story 3 - Watch the answer appear token-by-token as it is generated (Priority: P2)

A user posing a longer prompt wants to start reading the response while it
is still being generated, instead of waiting for the full reply, so they
can decide to cancel early if the answer is going off-track.

**Why this priority**: Streaming is a major usability win for interactive
use and a hard requirement for some pipeline workflows (e.g. piping into
a terminal viewer). It is P2 only because the non-streaming code path is
the default and is independently sufficient as an MVP.

**Independent Test**: Invoke the CLI with `--prompt "Count from 1 to 10
slowly" --stream` and observe that text chunks appear on stdout
incrementally, not all at once after the full response has been received.

**Acceptance Scenarios**:

1. **Given** the user passes `--stream` (without `--format`), **When** the
   response arrives, **Then** plain-text chunks are written to stdout in
   the order received, with no buffering until completion.
2. **Given** the user passes `--stream --format json`, **When** the
   response arrives, **Then** each chunk is emitted as a self-contained
   JSON object `{"content": "<chunk>"}` on stdout as it arrives.
3. **Given** the user cancels the invocation with Ctrl+C during streaming,
   **When** the cancellation propagates, **Then** the CLI stops streaming
   gracefully, the process exits, and no partial output is corrupted on
   stdout.
4. **Given** the upstream service returns a non-success HTTP status for a
   streaming request, **When** the streaming pipeline observes the
   failure, **Then** the CLI yields no chunks for the failing request and
   the failure is logged.

---

### User Story 4 - Save the response to a file for archiving or post-processing (Priority: P2)

A user wants to capture the AI's reply into a file so it can be reviewed
later, attached to a ticket, or fed into another tool, without manually
copy-pasting from the terminal.

**Why this priority**: File output unlocks asynchronous workflows
(archiving runs, building corpora, attaching to PRs). P2 because the
terminal-only modes already satisfy interactive use; file capture is an
incremental convenience.

**Independent Test**: Invoke `ai-cli --prompt "Explain X" -o ./answer.txt`
and confirm that after the run, `./answer.txt` exists and contains the
assistant's text (matching what would have been on stdout). Verifiable
without streaming or JSON mode.

**Acceptance Scenarios**:

1. **Given** the user passes `-o ./answer.txt`, **When** the response is
   rendered in text mode, **Then** the assistant text is written to
   `./answer.txt` in addition to being displayed on stdout.
2. **Given** the user passes `--output-file ./answer.json --format json`,
   **When** the response is rendered, **Then** the raw provider JSON body
   is written to `./answer.json` and to stdout.
3. **Given** the user passes `-o ./answer.txt --stream`, **When** the
   response streams, **Then** chunks are echoed to stdout incrementally
   AND the concatenated final text is written to the file once the
   stream completes.
4. **Given** the user supplies the Windows alias `/o ./answer.txt`,
   **When** the CLI parses the arguments, **Then** the behavior is
   identical to `-o ./answer.txt`.

---

### User Story 5 - Trust that saved files are safe to share and have restrictive permissions (Priority: P3)

A user pasting AI output that may contain ANSI escape sequences (e.g. from
a model that imitates terminal output, or from a malicious prompt
injection) wants the saved file to be free of escape sequences and to be
created with permissions that prevent other users on a shared host from
reading it by default.

**Why this priority**: Security hardening is essential for shared
machines and CI runners but does not block first-use, hence P3. The
hardening must happen automatically — users will not opt-in.

**Independent Test**: Invoke the CLI with a prompt that elicits ANSI
escape sequences (e.g. asking the model to "render red text") and an
`-o ./out.txt` switch on Linux/macOS. Confirm that `./out.txt`:
(a) contains no `ESC[…m` / `ESC[…G` / `ESC[…K` sequences, and
(b) has mode `0600` (owner read+write only).

**Acceptance Scenarios**:

1. **Given** the AI response contains characters matching the pattern
   `\x1B[<digits and semicolons>[m|G|K]`, **When** the response is
   written to the output file, **Then** all such ANSI control sequences
   are removed before the bytes are written. The stdout rendering may
   still contain them (terminal-native), but the file does not.
2. **Given** the host is a Unix-like system (Linux, macOS), **When** the
   output file is written, **Then** the file mode is set to `0600`
   (`UserRead | UserWrite`), making the file readable and writable only
   by the owning user.
3. **Given** the host is Windows, **When** the output file is written,
   **Then** the file is created with the inherited NTFS ACL and the
   Unix permission step is silently skipped.
4. **Given** the Unix permission update fails (e.g. due to ACL or
   filesystem restrictions), **When** the failure is observed, **Then**
   the CLI does not crash and still considers the file write successful.

---

### Edge Cases

- **Format validation failure**: An unrecognized `--format` value (e.g.
  `xml`) is rejected at argument parse time with exit code `1` and the
  message `"Format must be 'text' or 'json'"`. No API call is issued.
- **Streaming + JSON**: Streaming chunks are JSON-encoded one-at-a-time
  as `{"content": "<chunk>"}` rather than as elements of a single
  enclosing JSON array — i.e. the output is a stream of JSON values, not
  a valid single JSON document.
- **Output file overwrite**: If the file at `-o <path>` already exists,
  it is overwritten without confirmation (consistent with `File.WriteAllTextAsync`).
- **Output file path is a directory**: The underlying file write surfaces
  the I/O error as a file/IO failure with exit code `3`.
- **Streaming + output file**: The file is written only after streaming
  completes, not incrementally; if the user cancels mid-stream, no file
  is produced.
- **ANSI stripping scope**: Only the SGR (`[m`), cursor-positioning
  (`[G`), and erase-in-line (`[K`) families are stripped (regex
  `\x1B\[[0-9;]*[mGK]`). Other escape sequences (e.g. `[H`, `[J`,
  `[?25l`) are not removed; see Assumptions.
- **Non-success streaming response**: When the streaming HTTP response
  carries a non-2xx status, the streaming pipeline yields no chunks and
  the run completes with empty stdout in `--stream` mode (the error is
  logged but is not surfaced as a thrown exception in the streaming
  path).
- **Effective `Stream` and `Format` from active configuration**: When the
  user does not pass `--stream` or `--format`, the resolved values fall
  back to the active model configuration's `Stream` and `Format` fields.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept an output format selector from the
  command line via the switch `--format`, with the only allowed values
  `text` and `json`.
- **FR-002**: The system MUST default the output format to `text` when
  neither the command line nor the active model configuration overrides
  the value.
- **FR-003**: The system MUST validate the `--format` value at argument
  parse time and MUST reject any value other than `text` or `json` with
  exit code `1` and a clear error message, before issuing any API
  request.
- **FR-004**: The system MUST accept a streaming toggle from the command
  line via the switch `--stream` (boolean, default off).
- **FR-005**: The system MUST accept an output file path from the command
  line via the switches `--output-file`, `-o`, or `/o`.
- **FR-006**: When `--format text`, the system MUST write the assistant's
  reply content (only the generated text) to standard output.
- **FR-007**: When `--format json` and not streaming, the system MUST
  write the raw provider response JSON body verbatim to standard output.
- **FR-008**: When `--stream` and `--format text`, the system MUST write
  each text chunk to standard output in the order received, without
  waiting for the full response.
- **FR-009**: When `--stream` and `--format json`, the system MUST emit
  each chunk as a self-contained JSON object of the form
  `{"content": "<chunk>"}` to standard output, one object per chunk, in
  the order received.
- **FR-010**: The streaming pipeline MUST consume the OpenAI-compatible
  Server-Sent-Events format (`data: <json>` lines, terminated by
  `data: [DONE]`) and extract the `choices[0].delta.content` field of
  each chunk.
- **FR-011**: The streaming pipeline MUST tolerate malformed individual
  SSE chunks: a chunk that fails to JSON-parse is logged at warning
  level and skipped without aborting the stream.
- **FR-012**: When `-o/--output-file <path>` is supplied, the system
  MUST write the same content that was rendered to stdout (resolved per
  the current format mode) to the specified file, in addition to the
  stdout rendering.
- **FR-013**: When `-o/--output-file <path>` is supplied together with
  `--stream`, the system MUST accumulate the streamed text chunks and
  write the concatenated result to the file once the stream completes.
- **FR-014**: Before writing any output file, the system MUST strip
  ANSI escape sequences matching the pattern `ESC[<digits/semicolons>][mGK]`
  from the content. The stdout rendering MAY retain ANSI sequences.
- **FR-015**: On Unix-like systems (any non-Windows OS), the system
  MUST set the output file's mode to `0600` (`UserRead | UserWrite`)
  immediately after writing it.
- **FR-016**: The system MUST tolerate failures of the Unix permission
  step (e.g. swallow the exception) so that a successful write is not
  reported as a failure due to a permission-side error.
- **FR-017**: On Windows, the system MUST skip the Unix permission
  step entirely; the file inherits the parent directory's NTFS ACL.
- **FR-018**: The system MUST forward the resolved `Stream` flag to the
  AI service, so that streaming requests are issued with HTTP semantics
  appropriate for SSE responses (chunked read of the response stream)
  and non-streaming requests are issued in the normal request/response
  manner.
- **FR-019**: When neither `--stream` nor `--format` is supplied, the
  system MUST fall back to the active model configuration's persisted
  `Stream` and `Format` values; if no configuration override applies,
  the built-in defaults (`Stream = false`, `Format = "text"`) MUST be
  used.
- **FR-020**: The system MUST NOT log file contents or output paths at
  Information level beyond what is necessary to describe the run.

### Key Entities

- **CLI Output Options (in `CliOptions`)**: The user-facing surface for
  this feature. Holds the chosen output format (`text` or `json`), the
  streaming toggle (`Stream`), and the optional output file path
  (`OutputFile`).
- **AI Request**: Carries the resolved `Stream` flag downstream to the
  HTTP client; the format choice is *not* carried on `AIRequest` because
  format is purely a rendering concern.
- **AI Response (non-streaming)**: Carries both the parsed assistant
  text (`Content`) and the verbatim provider JSON body (`RawResponse`);
  the renderer picks `Content` for `--format text` and `RawResponse` for
  `--format json`.
- **Streaming Chunk Stream**: A pull-based async sequence of plain text
  fragments yielded by the AI client; the rendering layer transforms
  each chunk either by passthrough (text mode) or by JSON-wrapping
  (json mode), and accumulates them into a buffer for optional file
  write.
- **Output File**: A path on disk to which the rendered (and
  ANSI-stripped) content is persisted, with `0600` permissions on Unix.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of invocations with `--format text` (and no `--stream`)
  write only the assistant text to stdout, with no JSON markup.
- **SC-002**: 100% of invocations with `--format json` (and no `--stream`)
  produce stdout that is a valid JSON document parseable as the provider's
  full response body.
- **SC-003**: With `--stream`, the user observes the first chunk on
  stdout strictly before the upstream response has been fully delivered
  (no end-to-end buffering by the CLI).
- **SC-004**: With `-o <path>` on a Unix host, `stat <path>` reports mode
  `600` and the file contents contain no ANSI escape bytes (verified by
  byte-scan for `0x1B`).
- **SC-005**: With `-o <path>` on Windows, the file is created with
  inherited ACL and no exception is raised.
- **SC-006**: Passing `--format xml` (or any non-`text`/`json` value)
  causes the process to exit with code `1` and the message `"Format
  must be 'text' or 'json'"`, with no HTTP request issued.
- **SC-007**: `-o`, `--output-file`, and `/o` aliases parse identically
  for the same path argument.
- **SC-008**: A streaming response with a malformed SSE chunk continues
  to deliver subsequent valid chunks (no single bad chunk aborts the
  stream).

## Assumptions

- The provider's streaming endpoint emits OpenAI-compatible SSE
  (`data: <json>\n\n` lines, terminating with `data: [DONE]`); the
  streaming pipeline does not attempt to support other framings.
- For `--format json`, the upstream provider's full response body is a
  valid JSON document; the CLI passes the bytes through without
  re-serialization or validation.
- ANSI stripping covers the SGR (`m`), cursor horizontal absolute (`G`),
  and erase-in-line (`K`) commands only. This is sufficient for the
  most common terminal-injection vectors a model is likely to produce
  but is not a complete ANSI/VT100 sanitizer.
- Writing to the output file overwrites it without confirmation; users
  who need append behavior or backup-on-overwrite are out of scope.
- Streaming output to the file is **not** incremental: the file is
  written once at the end of the stream. A cancelled stream therefore
  leaves no file behind.
- For `--format json --stream`, the output is a sequence of JSON values
  on stdout (not a single JSON array). Consumers must parse one value
  at a time.
- Restrictive file permissions apply only on non-Windows OSes via
  `File.SetUnixFileMode`; Windows relies on the parent directory's
  inherited ACL.
- The `Stream` toggle and the `Format` value can each be supplied
  independently or together; they compose orthogonally.

## Retrospec Metadata

**Generated**: 2026-05-29
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 13 source files across 2 projects (`ai-cli`, `ai-cli.Tests`)
**Reference implementation branch**: feature/nodejs
