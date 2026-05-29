# Implementation Plan: Output Formatting and Destination

**Branch**: `003-output-formatting-and-destination` | **Date**: 2026-05-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-output-formatting-and-destination/spec.md`

**Note**: This plan was reverse-engineered from the existing implementation. Each
section reflects the concrete decisions visible in the source code.

## Summary

Add three command-line switches — `--format` (text or json, default text),
`--stream` (boolean), and `-o/--output-file` (path) — that together control
how the AI response is rendered and where it is persisted. The format
selector and streaming toggle live on `CliOptions`; the streaming toggle
is forwarded onto `AIRequest` and flips the `OpenAIClient` between a
single `POST → JSON body` round-trip (`SendRequestAsync`) and a streaming
read of an SSE (`data: <json>`) response (`SendStreamingRequestAsync`),
yielding text chunks through an `IAsyncEnumerable<string>`. The rendering
fork lives in `Program.cs` (`ProcessNonStreamingRequest` /
`ProcessStreamingRequest`): text mode writes `AIResponse.Content` to
stdout, json mode writes `AIResponse.RawResponse` verbatim; under
`--stream + --format json`, each chunk is wrapped in `{"content": "..."}`
before being written. Whenever `-o <path>` is set, the same rendered
content (accumulated to a `StringBuilder` for streaming) is then written
to the file by `WriteToFileAsync`, which (a) strips ANSI SGR / cursor /
erase-in-line escape sequences via a fixed regex and (b) sets the file
mode to `0600` on non-Windows OSes via `File.SetUnixFileMode`.

## Technical Context

**Language/Version**: C# 12 on .NET 9.0

**Primary Dependencies**:
- `System.CommandLine` (argument parsing of `--format`, `--stream`,
  `--output-file`, plus validator rejecting non-text/json formats).
- `System.Text.Json` (per-chunk JSON wrapping in streaming JSON mode;
  the full provider JSON is written through unchanged in non-streaming
  JSON mode — no extra serialization).
- `System.Text.Json.Nodes` (`JsonNode.Parse` of each streaming SSE chunk
  to extract `choices[0].delta.content` in `OpenAIClient`).
- `System.IO` + `System.Text` (`StringBuilder` to accumulate streamed
  chunks; `File.WriteAllTextAsync`; `File.SetUnixFileMode`).
- `System.Text.RegularExpressions` (ANSI-sequence stripping regex
  `\x1B\[[0-9;]*[mGK]`).
- `Microsoft.Extensions.Http` (HttpClientFactory composition of
  `OpenAIClient`; supports long timeouts for streaming).
- `Microsoft.Extensions.Logging` + Serilog.

**Storage**: User settings (including default `ModelConfiguration` with
its own `Format` and `Stream` fields) persisted to a platform-appropriate
file via `FileUserSettingsService`. Output files are written to
user-supplied paths with `0600` mode on Unix.

**Testing**: xUnit + Moq (with `Moq.Protected` for `HttpMessageHandler`)
+ FluentAssertions, in the `ai-cli.Tests` project.

**Target Platform**: Cross-platform console application; published as a
self-contained single-file executable for Windows, Linux, and macOS.
The Unix permission step is conditional on `!OperatingSystem.IsWindows()`.

**Project Type**: CLI / desktop-app.

**Performance Goals**:
- Streaming first-chunk latency is bounded by network and provider, not
  by the CLI; the CLI reads the response stream line-by-line (no
  end-to-end buffering).
- ANSI-stripping regex runs once per file write on the accumulated
  content; negligible compared with the HTTP round-trip.

**Constraints**:
- The streaming pipeline MUST NOT buffer the full response in memory
  for stdout — chunks must be emitted as they arrive.
- The ANSI-stripping regex MUST only run before the file write, never
  before the stdout write (terminal rendering is preserved on stdout).
- The Unix permission step MUST be silently skipped on Windows and MUST
  swallow exceptions on Unix.
- The CLI MUST validate `--format` before issuing any HTTP request.

**Scale/Scope**: Single-user interactive CLI; one outbound HTTP call per
invocation; one optional file write per invocation.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The repository constitution at `.specify/memory/constitution.md` is still
the unpopulated template (placeholder principle names). There are
therefore no concrete principle gates to enforce against this feature.

Observed alignment with conventional speckit principles:

- **CLI Interface**: The feature is wholly a CLI surface (`--format`,
  `--stream`, `-o/--output-file`) with stdout text I/O, optional file
  I/O, and clear exit codes (`1` for invalid arguments, `3` for file I/O
  failures, `2` for API failures). PASS.
- **Test-First / coverage**: Argument parsing for all three switches is
  unit-tested (`CommandLineBuilderTests`), and the streaming SSE
  decoder is unit-tested
  (`OpenAIClientTests.SendStreamingRequestAsync_WithValidRequest_ShouldReturnChunks`).
  The format-fork rendering (`ProcessStreamingRequest` /
  `ProcessNonStreamingRequest`) and the secure file-output path
  (`WriteToFileAsync`: ANSI strip + `0600` mode) are NOT covered by a
  dedicated unit test — see Complexity Tracking.
- **Simplicity**: A small set of additions to existing types
  (`CliOptions.OutputFile`, `Format`, `Stream`; `AIRequest.Stream`;
  `IAIClient.SendStreamingRequestAsync`) plus two private static methods
  in `Program.cs` (`ProcessStreamingRequest`, `WriteToFileAsync`). No
  new project, no new dependency. PASS.
- **Security**: ANSI stripping before file write defends against
  terminal-injection attacks via prompt manipulation. `0600` file mode
  on Unix prevents other users on shared hosts from reading saved
  responses. PASS.

No deviations require justification beyond the gap noted in Complexity
Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/003-output-formatting-and-destination/
├── plan.md              # This file
├── spec.md              # Feature specification (reverse-engineered)
├── research.md          # Technical decisions recovered from code
├── data-model.md        # Entities and their format/stream/output fields
├── quickstart.md        # Usage guide extracted from tests + --help
├── contracts/
│   ├── cli-output-options.md       # CLI switches and validation contract
│   ├── streaming-pipeline.md       # OpenAI SSE → IAsyncEnumerable contract
│   └── output-file-security.md     # ANSI strip + 0600 mode contract
└── checklists/
    └── requirements.md  # Quality checklist (all items satisfied)
```

### Source Code (repository root)

The feature touches the following actual files. Paths are relative to
the repository root.

```text
src/ai-cli/
├── CLI/
│   └── CommandLineBuilder.cs        # Defines --format, --stream,
│                                    # -o/--output-file (with /o alias),
│                                    # the {text, json} validator, and
│                                    # ParseOptions() lifting values into
│                                    # CliOptions.
├── Models/
│   ├── CliOptions.cs                # OutputFile (string?), Format (string,
│   │                                # default "text"), Stream (bool).
│   ├── AIRequest.cs                 # Carries Stream forward to the HTTP
│   │                                # layer (Format is rendering-only and
│   │                                # is NOT on AIRequest).
│   ├── AIResponse.cs                # Carries both Content (text) and
│   │                                # RawResponse (json) so the renderer
│   │                                # can pick either.
│   └── UserSettings.cs              # ModelConfiguration supplies persisted
│                                    # Format and Stream defaults.
├── Application/
│   ├── IAIClient.cs                 # Pair: SendRequestAsync,
│   │                                # SendStreamingRequestAsync.
│   ├── IPromptService.cs            # ProcessPromptAsync,
│   │                                # ProcessStreamingPromptAsync.
│   └── PromptService.cs             # Forwards Stream=true on streaming
│                                    # path; yields each chunk to caller.
├── Infrastructure/
│   └── OpenAIClient.cs              # SendStreamingRequestAsync reads
│                                    # the response as a Stream, parses
│                                    # "data: <json>" lines, extracts
│                                    # choices[0].delta.content, tolerates
│                                    # JsonException per-chunk, and stops
│                                    # at "data: [DONE]".
└── Program.cs                       # Dispatch on options.Stream;
                                     # ProcessNonStreamingRequest picks
                                     # Content vs RawResponse on Format;
                                     # ProcessStreamingRequest does per-
                                     # chunk text or JSON wrapping;
                                     # WriteToFileAsync strips ANSI and
                                     # sets 0600 on Unix.

src/ai-cli.Tests/
├── CLI/
│   └── CommandLineBuilderTests.cs   # Parses --format/--stream/--output-file;
│                                    # rejects --format xml; covers
│                                    # -o, /o, --output-file aliases.
├── Infrastructure/
│   └── OpenAIClientTests.cs         # SendStreamingRequestAsync_WithValid…
│                                    # asserts the SSE → chunk pipeline.
└── Application/
    └── PromptServiceTests.cs        # ProcessStreamingPromptAsync_With…
                                     # asserts streaming chunks pass
                                     # through unchanged.
```

**Structure Decision**: The .NET 9 single-project CLI layout is
preserved (CLI/Application/Infrastructure/Models folders inside
`src/ai-cli/`). This feature adds no new project, no new layer, and no
new third-party dependency.

## Complexity Tracking

> The constitution is unpopulated, so no formal gates were violated.
> The table below records implementation traits worth noting for future
> simplification or hardening.

| Trait | Why It Exists | Possible Simpler Alternative |
|-------|---------------|------------------------------|
| ANSI-stripping regex is hard-coded inside `WriteToFileAsync` rather than centralized | The only place ANSI-bearing content meets persistent storage is file output; the stdout rendering path intentionally preserves ANSI for the terminal. | Extract a small `AnsiSanitizer` helper if a second persistence sink is added (e.g. log redaction, clipboard). |
| Regex covers only `[mGK]` SGR/cursor/erase-in-line commands, not full ANSI/VT100 | These cover the SGR coloring vectors a model is most likely to emit. Full sanitization would require an actual ANSI/VT100 parser. | Replace with a dedicated parser library if richer sanitization is required (e.g. against `[H`, `[J`, OSC, DCS). |
| File write under `--stream` is post-stream, not incremental | Streaming chunks are accumulated in a `StringBuilder` and flushed once at the end. Incremental writes would couple the stream loop to the disk path. | Use `StreamWriter` and `FlushAsync()` per chunk if real-time persistence becomes a requirement; trade-off is a partial file on cancellation. |
| `Format` is carried on `CliOptions` and `ModelConfiguration` but NOT on `AIRequest` | Format is a pure rendering choice — the upstream API does not need to know whether the CLI will print JSON or text. | Keep as-is unless server-side format negotiation is added. |
| Streaming JSON mode emits a stream-of-values, not a single JSON array | Lets downstream consumers parse incrementally without a streaming-array parser. | Wrap in `[…]` with comma-separated chunks if a single-document consumer is needed (would require sentinel-aware buffering). |
| `WriteToFileAsync` swallows all exceptions from `SetUnixFileMode` | Some filesystems (FUSE, mounted Windows shares on Linux) do not support `chmod` and would raise, but the file write itself is still meaningful. | Log the swallowed exception at Debug level so it is recoverable from the log without noise on stderr. |
| No unit test asserts that the saved file (a) has `0600` mode on Unix or (b) is ANSI-free | Both behaviors are I/O-side and would require a filesystem-touching test. | Add a Linux-specific test that creates a temp file, invokes `WriteToFileAsync`, then asserts mode bits and absence of `\x1B` bytes. |
| Streaming non-success HTTP statuses yield zero chunks rather than throwing | Mirrors the "graceful empty" semantics of `IAsyncEnumerable`. | Surface as an exception or a sentinel chunk if the user must distinguish "empty answer" from "transport failure". |
