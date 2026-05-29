# Implementation Plan: Prompt Input Methods

**Branch**: `001-prompt-input-methods` | **Date**: 2026-05-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-prompt-input-methods/spec.md`

**Note**: This plan was reverse-engineered from the existing implementation.
Technical context values reflect what the code actually does today rather
than open questions to be resolved.

## Summary

The CLI must accept a user's prompt from exactly one of three mutually
exclusive sources — an inline `-p/--prompt` argument, a file referenced
by `-f/--file`, or standard input (piped or interactive) — and pass the
resolved text to the AI request pipeline. The existing implementation
realises this as:

- A `System.CommandLine`-based option layer that declares the two flags,
  validates that at most one is supplied, and computes a derived
  `UseStdin` flag based on whether stdin is redirected.
- A `PromptService` that resolves the effective prompt text from
  `CliOptions` in a deterministic order (inline → file → stdin), with
  special-case handling for interactive stdin that reads line-by-line
  until end-of-input.
- File-source post-processing that deletes the prompt file after the
  request completes (successful or streaming).

## Technical Context

**Language/Version**: C# / .NET 9.0 (nullable reference types on, warnings
treated as errors)

**Primary Dependencies**:

- `System.CommandLine` — argument parsing, aliases, and validation
- `Microsoft.Extensions.Logging` — logging via Serilog providers
- `Microsoft.Extensions.DependencyInjection` — wiring `IPromptService`
- Standard library: `System.IO`, `System.Console`, `System.Text`

**Storage**: Local filesystem only — read text from `--file` paths,
delete those files after use. No database, no other persistence.

**Testing**: xUnit + Moq + FluentAssertions. Unit tests cover
`PromptService` (each source plus error paths) and `CommandLineBuilder`
(every alias variant, mixed-syntax invocations, validation errors).

**Target Platform**: Cross-platform CLI binary — Windows, Linux, macOS.
Forward-slash option style (`/p`, `/f`) is supported for Windows ergonomics
but works on all platforms.

**Project Type**: CLI application (single-project console app plus a
sibling test project; see source layout below).

**Performance Goals**: Sub-second startup; prompt resolution itself is
O(file size) and runs once per invocation. No throughput requirements
beyond responding promptly to keystrokes in interactive mode.

**Constraints**:

- Prompt is read entirely into memory before the AI request is issued
  (no streaming of the prompt itself).
- Interactive stdin reading is blocking and synchronous from the user's
  point of view; cancellation is honoured via Ctrl+C wired in
  `Program.Main`.
- File deletion after a successful run is part of the contract; tests
  assert this behaviour.

**Scale/Scope**: Single user, single invocation. No concurrency
considerations within one prompt-source resolution.

## Constitution Check

The project constitution (`/workspace/.specify/memory/constitution.md`)
is still the speckit placeholder template — its principles have not been
filled in. No constitutional gates can therefore be evaluated against
this feature. **Action**: When the constitution is ratified, revisit
this plan to confirm compliance (in particular: testing requirements,
CLI text-in/text-out conventions, observability/logging, and simplicity
constraints). The current implementation appears consistent with the
example principles commented out in the template (CLI interface, text
I/O, structured logging, integration via DI).

## Project Structure

### Documentation (this feature)

```text
specs/001-prompt-input-methods/
├── plan.md                       # This file
├── spec.md                       # Business specification
├── research.md                   # Technical decisions captured retrospectively
├── data-model.md                 # CliOptions and prompt-source entities
├── quickstart.md                 # How to use each input method
├── contracts/
│   ├── cli-prompt-options.md     # The -p/--prompt and -f/--file CLI contract
│   └── iprompt-service.md        # IPromptService programmatic contract
└── checklists/
    └── requirements.md           # Reverse-engineered requirements checklist
```

### Source Code (repository root)

The feature is delivered inside the existing `ai-cli` solution. Only the
files directly related to this feature are listed here.

```text
src/
├── ai-cli/
│   ├── Application/
│   │   ├── IPromptService.cs      # contract: ProcessPromptAsync, ProcessStreamingPromptAsync
│   │   └── PromptService.cs       # GetPromptTextAsync resolves the three sources
│   ├── CLI/
│   │   └── CommandLineBuilder.cs  # -p/--prompt/-f/--file options + validator
│   ├── Models/
│   │   └── CliOptions.cs          # Prompt, FilePath, UseStdin fields
│   └── Program.cs                 # parses args, dispatches to PromptService,
│                                  # maps FileNotFoundException -> FileError
└── ai-cli.Tests/
    ├── Application/
    │   └── PromptServiceTests.cs  # inline/file/missing/no-source/streaming
    └── CLI/
        └── CommandLineBuilderTests.cs # alias variants, mixed syntax
```

**Structure Decision**: Single-project console application with a
parallel test project (the project's standing convention). The prompt
input feature is split across three layers — CLI option declaration
(`CLI/`), domain options model (`Models/`), and resolution logic
(`Application/`) — to keep argument parsing decoupled from prompt
resolution.

## Complexity Tracking

> No constitutional violations identified (constitution is template-only).

The implementation does carry one notable behavioural complexity worth
flagging:

| Behaviour | Why it exists | Simpler alternative rejected because |
|-----------|---------------|--------------------------------------|
| `PromptService` deletes the source `--file` after processing | Treats file inputs as transient scratchpads (e.g. tools that generate a temp prompt file then invoke the CLI) | Leaving the file on disk would require callers to manage cleanup themselves, and could leak prompt text containing sensitive context |
| Interactive stdin reads via `Console.ReadLine()` loop instead of `StreamReader.ReadToEndAsync` | Required so the user sees a usable interactive prompt; `ReadToEndAsync` on a TTY-backed stream blocks without echoing line buffering correctly across platforms | Always using `ReadToEndAsync` would make interactive mode feel broken (no per-line behavior) |
| `UseStdin` is set in `ParseOptions` based on `Console.IsInputRedirected` AND absence of `--prompt`/`--file` | Lets the CLI distinguish "user wants stdin" from "user is in interactive mode that should fall through to error" | Simpler "always read stdin when no flag" would hang forever when stdin is `/dev/null` or otherwise empty in non-interactive contexts |
