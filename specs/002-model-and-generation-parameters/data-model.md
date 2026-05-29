# Data Model: Model and Generation Parameters

This document captures the entities that carry the model identifier,
temperature, and max-tokens values from the command line to the
OpenAI-compatible HTTP request. Each entity is described by the fields
that are relevant to this feature; orthogonal fields are noted but not
specified.

## CliOptions

**Source**: `src/ai-cli/Models/CliOptions.cs`

**Role**: Plain mutable carrier produced by `CommandLineBuilder.ParseOptions`
from a `ParseResult`. Holds the user's verbatim choices for one invocation.

| Field | Type | Default | Constraint |
|-------|------|---------|------------|
| `Model` | `string` | `"gpt-3.5-turbo"` | Non-empty after parse; no whitelist enforced. |
| `Temperature` | `float` | `1.0f` | `0.0 ≤ value ≤ 2.0`, enforced by the root command validator. |
| `MaxTokens` | `int?` | `null` | Nullable; no CLI-side range check. |

Other fields (`Prompt`, `FilePath`, `UseStdin`, `OutputFile`, `Format`,
`Stream`, `Config`) belong to sibling features.

**Lifecycle**: Constructed once per invocation in `ParseOptions`, then
mutated exactly once inside `Program.HandleCommandAsync` to merge in
fallback values from the active `ModelConfiguration`.

## AIRequest

**Source**: `src/ai-cli/Models/AIRequest.cs`

**Role**: Immutable record describing a single API call. This is the
boundary between the application layer (`PromptService`) and the
infrastructure layer (`OpenAIClient`).

| Field | Type | Required | Constraint |
|-------|------|----------|------------|
| `Prompt` | `string` | yes | Non-null; passed as the single user message. |
| `Model` | `string` | yes | Non-null; copied verbatim into the JSON `model` field. |
| `Temperature` | `float` | no (default `1.0f`) | Serialized verbatim into the JSON `temperature` field. |
| `MaxTokens` | `int?` | no | If non-null, serialized as `max_tokens`; if null, the field is omitted. |
| `Stream` | `bool` | no (default `false`) | Controls whether the streaming endpoint shape is used. |

**Relationships**:
- Constructed by `PromptService.ProcessPromptAsync` /
  `ProcessStreamingPromptAsync` from a `CliOptions`.
- Consumed by `OpenAIClient.SendRequestAsync` and
  `OpenAIClient.SendStreamingRequestAsync`.
- For streaming, the client clones it via `request with { Stream = true }`,
  preserving `Model`, `Temperature`, and `MaxTokens` unchanged.

**Validation**: Range validation happens upstream in
`CommandLineBuilder`; `AIRequest` itself does not re-validate.

## ModelConfiguration (fallback defaults)

**Source**: `src/ai-cli/Models/UserSettings.cs`

**Role**: A persisted, named bundle of defaults selected by the user via
`--config`. Supplies fallback values for `Model`, `Temperature`, and
`MaxTokens` when the corresponding CLI switch is not specified.

| Field | Type | Default | Relevance to this feature |
|-------|------|---------|---------------------------|
| `Model` | `string` | `"gpt-3.5-turbo"` | Used when CLI did not pass `--model`. |
| `Temperature` | `float` | `1.0f` | Used when CLI did not pass `--temperature`. |
| `MaxTokens` | `int?` | `null` | Used when CLI did not pass `--max-tokens`. |

Other fields (`Id`, `Name`, `Type`, `ApiKey`, `BaseUrl`, `Format`,
`Stream`) belong to sibling features.

**Resolution rule** (enforced in `Program.HandleCommandAsync`):
```
effective.Model       = CLI.Model      != "gpt-3.5-turbo" ? CLI.Model       : config.Model
effective.Temperature = CLI.Temperature != 1.0f           ? CLI.Temperature : config.Temperature
effective.MaxTokens   = CLI.MaxTokens   ?? config.MaxTokens
```

Sentinel-default comparison is a documented quirk — see
`plan.md → Complexity Tracking`.

## AIResponse

**Source**: `src/ai-cli/Models/AIResponse.cs`

**Role**: The non-streaming response payload. Relevant to this feature
only insofar as it reports the model the provider actually used (which
may differ from the requested one in proxied/aliased setups).

| Field | Type | Notes |
|-------|------|-------|
| `Content` | `string` | Generated text. |
| `Model` | `string` | Echoed from `choices[0].model` or falls back to the requested `Model`. |
| `RawResponse` | `string` | Full provider JSON, used for `--format json` output. |
| `Success` | `bool` | False on non-2xx or transport error. |
| `ErrorMessage` | `string?` | Populated when `Success` is false. |

## State transitions

This feature has no stateful workflow. Each invocation is a single,
linear pass:

```
CliOptions  ── ParseOptions ──▶  CliOptions
   │
   ├─ Program merges with ModelConfiguration ─▶ (mutated) CliOptions
   │
   ▼
PromptService copies into AIRequest (immutable)
   │
   ▼
OpenAIClient.CreateRequestBody serializes JSON (omits nulls, snake_case)
   │
   ▼
POST {baseUrl}/chat/completions
```
