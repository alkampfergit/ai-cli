# Research: Model and Generation Parameters

Technical decisions visible in the implementation, with the rationale
that can be inferred from the code, comments, and conventional .NET CLI
practice.

## Decision 1: Custom invariant-culture parser for `--temperature`

- **Decision**: Provide a hand-rolled `parseArgument` on the
  `--temperature` option that uses
  `System.Globalization.NumberStyles.Float` and
  `CultureInfo.InvariantCulture`.
- **Rationale**: `System.CommandLine`'s default `float` binder is
  locale-sensitive. On hosts where the user's culture uses comma as the
  decimal separator (de-DE, it-IT, fr-FR, ...) the literal string `0.5`
  is ambiguous, and the historical default behaviour silently
  misinterprets `0,5` as the integer `5`, which is then rejected by the
  `[0.0, 2.0]` validator with a confusing error. Forcing invariant
  parsing produces deterministic behaviour for the same input string on
  every machine.
- **Alternatives considered**:
  - Rely on the framework default → rejected because of cross-locale
    inconsistency.
  - Document "use dots only" and rely on user discipline → rejected
    because it is still locale-dependent on the framework side.

## Decision 2: Range validation lives in the root command validator

- **Decision**: The `[0.0, 2.0]` range check for `--temperature` is
  performed inside `rootCommand.AddValidator(...)`, not inside
  `CliOptions`, not inside `AIRequest`, and not inside `OpenAIClient`.
- **Rationale**: Failing at parse time exits with
  `ExitCodes.InvalidArguments` (`1`) and a clear message before any
  network I/O occurs, which protects the user from spending API credits
  on a guaranteed-invalid request.
- **Alternatives considered**:
  - Re-validate inside `OpenAIClient` → rejected as redundant.
  - Defer to the provider's own range check → rejected because some
    providers tolerate out-of-range values silently.

## Decision 3: Three short-aliases for `--model` (`-m`, `/m`), none for the others

- **Decision**: `--model` carries the standard short alias `-m` and the
  Windows-style `/m`. `--temperature` and `--max-tokens` carry no short
  aliases.
- **Rationale**: Model is the parameter changed most frequently from
  one invocation to the next, so a one-letter alias is justified.
  Temperature and max-tokens are tuning knobs typically set once per
  scripted pipeline, so the verbose long-form spelling keeps scripts
  self-documenting.
- **Alternatives considered**:
  - Add `-t` for temperature → rejected (could be confused with a
    future `--top-p`, `--tools`, or `--timeout` switch).
  - Drop `/m` for cross-platform consistency → rejected because the rest
    of the CLI (`/p`, `/f`, `/o`) already supports the Windows
    convention.

## Decision 4: `MaxTokens` is nullable; null is omitted from JSON

- **Decision**: `CliOptions.MaxTokens`, `AIRequest.MaxTokens`, and
  `ModelConfiguration.MaxTokens` are all `int?`. The JSON serializer is
  configured with
  `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`.
- **Rationale**: OpenAI-compatible providers interpret an absent
  `max_tokens` as "use the provider's default", but interpret
  `max_tokens: 0` as "generate no tokens" and `max_tokens: null` as a
  schema error on some providers. Using nullability + null-omission
  preserves the provider's intended default behaviour.
- **Alternatives considered**:
  - Use `int.MaxValue` as a sentinel → rejected because some providers
    reject implausibly large values.
  - Use `-1` as "unset" → rejected for the same reason and because it
    crosses validation boundaries.

## Decision 5: CLI options override active model configuration via sentinel comparison

- **Decision**: In `Program.HandleCommandAsync`, the CLI value wins over
  the persisted `ModelConfiguration` value only when the CLI value
  differs from the literal default (`"gpt-3.5-turbo"` for model, `1.0f`
  for temperature). `MaxTokens` uses the cleaner `??` operator because
  it is nullable.
- **Rationale**: `System.CommandLine` does not (in this codebase's
  usage) expose a per-option "was specified" flag through the parsed
  `CliOptions`. Sentinel-default comparison is a quick way to detect
  "user did not override" without introducing per-option nullable
  fields.
- **Alternatives considered**:
  - Add per-option `IsModelSet` flags → noted in plan.md as the cleaner
    long-term simplification.
  - Inspect `ParseResult` for each option in `Program` → rejected
    because it leaks `System.CommandLine` types into the application
    layer.

## Decision 6: Snake-case JSON naming via `JsonNamingPolicy.SnakeCaseLower`

- **Decision**: The outbound JSON payload uses
  `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` so that
  C# `MaxTokens` becomes `max_tokens` on the wire.
- **Rationale**: OpenAI's API and most compatible providers use snake-
  case field names. Centralising the naming policy at serialization
  time keeps the C# code idiomatic.
- **Alternatives considered**:
  - Per-field `[JsonPropertyName]` attributes → rejected as
    boilerplate for a small payload.
  - A hand-built `Dictionary<string, object>` → rejected for type
    safety reasons.

## Decision 7: Same request shape for streaming and non-streaming

- **Decision**: `OpenAIClient.SendStreamingRequestAsync` constructs the
  request body by cloning the input with `request with { Stream = true }`
  and reusing the same `CreateRequestBody`. Model, temperature, and
  max-tokens flow through identically.
- **Rationale**: Two code paths would risk divergence (e.g. one path
  forgetting to forward `max_tokens`). A single serializer keeps both
  modes in lockstep.
- **Alternatives considered**:
  - Hand-write a streaming-specific payload → rejected as duplication.
