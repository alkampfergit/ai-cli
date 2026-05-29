# Implementation Plan: Model and Generation Parameters

**Branch**: `002-model-and-generation-parameters` | **Date**: 2026-05-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-model-and-generation-parameters/spec.md`

**Note**: This plan was reverse-engineered from the existing implementation. Each
section reflects the concrete decisions visible in the source code.

## Summary

Expose three command-line switches — `-m/--model`, `--temperature`, and
`--max-tokens` — that let the user pick the AI model and shape the
generation behavior for a single invocation. The values flow through a
plain CLI options carrier (`CliOptions`), are resolved against the active
persisted model configuration (CLI wins, configuration provides
fallback, built-in defaults are last), are copied into an immutable
`AIRequest`, and are serialized into the OpenAI-compatible JSON request
body sent by `OpenAIClient` to `POST {baseUrl}/chat/completions`. The
same resolution applies to both non-streaming and streaming requests.

## Technical Context

**Language/Version**: C# 12 on .NET 9.0

**Primary Dependencies**:
- `System.CommandLine` (argument parsing, including custom temperature parser)
- `System.Text.Json` (request body serialization with snake_case naming and null-omission)
- `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Http`
  (HttpClientFactory composition of `OpenAIClient`)
- `Microsoft.Extensions.Logging` + Serilog (diagnostic logging of the
  chosen model; never logs API keys)

**Storage**: User settings (including default `ModelConfiguration` with
its own `Model`, `Temperature`, `MaxTokens` fields) persisted to a
platform-appropriate file via `FileUserSettingsService`. API keys at rest
are encrypted (DPAPI on Windows, AES elsewhere) but that is out of scope
for this feature.

**Testing**: xUnit + Moq (with `Moq.Protected` for `HttpMessageHandler`)
+ FluentAssertions, in the `ai-cli.Tests` project.

**Target Platform**: Cross-platform console application; published as a
self-contained single-file executable for Windows, Linux, and macOS.

**Project Type**: CLI / desktop-app.

**Performance Goals**: Argument parsing and request-body construction are
synchronous and bounded by JSON serialization of a small object graph
(model, one user message, a few numbers, a boolean) — negligible CPU
relative to the HTTP round-trip.

**Constraints**:
- Temperature must be parsed with invariant culture so behaviour is
  identical across locales.
- `max_tokens` must be omitted from the JSON payload when null, not
  sent as `null` or `0`.
- Forward-slash aliases (`/m`) must be accepted alongside POSIX dash
  forms (`-m`, `--model`) for Windows users.

**Scale/Scope**: Single-user interactive CLI; one outbound HTTP call per
invocation; no concurrent request fan-out.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The repository constitution at `.specify/memory/constitution.md` is still
the unpopulated template (placeholder principle names). There are
therefore no concrete principle gates to enforce against this feature.

Observed alignment with conventional speckit principles:

- **CLI Interface**: The feature is wholly a CLI surface (`--model`,
  `--temperature`, `--max-tokens`) with stdout/stderr text I/O and clear
  exit codes. PASS.
- **Test-First / coverage**: All three switches have unit tests covering
  parsing, defaults, locale handling, and out-of-range rejection
  (`CommandLineBuilderTests`), and the request-body composition is
  exercised indirectly via `OpenAIClientTests`. PASS for parsing;
  request-body field-by-field assertions are NOT explicitly tested
  (see Complexity Tracking).
- **Simplicity**: A single POCO (`CliOptions`), a single record
  (`AIRequest`), and a single serializer call carry the data end-to-end.
  No extra abstractions are introduced for these three fields. PASS.

No deviations require justification beyond the gap noted in Complexity
Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/002-model-and-generation-parameters/
├── plan.md              # This file
├── spec.md              # Feature specification (reverse-engineered)
├── research.md          # Technical decisions recovered from code
├── data-model.md        # Entity definitions (CliOptions, AIRequest, ModelConfiguration)
├── quickstart.md        # Usage guide extracted from tests + --help
├── contracts/
│   ├── cli-generation-options.md   # CLI switches and validation contract
│   └── openai-request-body.md      # Outbound JSON contract
└── checklists/
    └── requirements.md  # Quality checklist (all items satisfied)
```

### Source Code (repository root)

The feature touches the following actual files. Paths are relative to
the repository root.

```text
src/ai-cli/
├── CLI/
│   └── CommandLineBuilder.cs        # Defines -m/--model, --temperature, --max-tokens
│                                    # options, the invariant-culture parser for
│                                    # temperature, the [0.0, 2.0] range validator,
│                                    # and ParseOptions() which lifts values into
│                                    # CliOptions.
├── Models/
│   ├── CliOptions.cs                # Plain carrier: Model, Temperature, MaxTokens
│   │                                # (+ other CLI flags).
│   ├── AIRequest.cs                 # Immutable record: Model, Temperature,
│   │                                # MaxTokens, Stream, Prompt.
│   ├── AIResponse.cs                # Carries back the model the provider used.
│   └── UserSettings.cs              # ModelConfiguration supplies default
│                                    # Model/Temperature/MaxTokens.
├── Application/
│   ├── IAIClient.cs                 # SendRequestAsync / SendStreamingRequestAsync.
│   └── PromptService.cs             # Copies CliOptions → AIRequest, calls IAIClient.
├── Infrastructure/
│   └── OpenAIClient.cs              # CreateRequestBody() serializes {model,
│                                    # messages, temperature, max_tokens, stream}
│                                    # with snake_case naming and null-omission.
└── Program.cs                       # Resolves CLI vs. configuration defaults,
                                     # wires DI, dispatches streaming vs. non-
                                     # streaming.

src/ai-cli.Tests/
├── CLI/
│   └── CommandLineBuilderTests.cs   # Parses --model/--temperature/--max-tokens;
│                                    # rejects temperature outside [0, 2];
│                                    # covers -m, /m, --model aliases.
└── Infrastructure/
    └── OpenAIClientTests.cs         # Exercises send + streaming round-trips with
                                     # the temperature field on AIRequest.
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
| Custom `parseArgument` for `--temperature` with invariant culture | Default `System.CommandLine` parsing of `float` is locale-sensitive, which previously caused regressions on non-en-US machines (commas vs. dots). | Rely on `System.CommandLine` defaults — rejected because it silently misinterprets `0,5` as `5` on some locales. |
| CLI-vs-configuration merge logic in `Program.HandleCommandAsync` uses sentinel values (e.g. `Temperature != 1.0f`) to detect "user did not override" | `System.CommandLine` does not natively expose "was this option specified on the command line?" through `CliOptions`. | Track per-option `IsSet` flags inside `CliOptions` (nullable backing fields) to avoid relying on the literal default `1.0f` as a "user did not set this" signal. |
| Request body construction lives inline inside `OpenAIClient.CreateRequestBody` as an anonymous object | Keeps the serialization shape co-located with the only consumer. | Extract a `ChatCompletionRequest` DTO if/when additional fields (tools, response_format, top_p, presence/frequency penalties) are added. |
| No direct assertion in `OpenAIClientTests` that the outbound JSON body contains `temperature` and `max_tokens` with the exact resolved values | Existing tests assert the response round-trip rather than the request body shape. | Add a test that captures the `HttpRequestMessage`, parses its content, and asserts on `model`, `temperature`, `max_tokens` snake-case keys. |
