# Data Model: Output Formatting and Destination

This document captures the entities that carry the format selector, the
streaming toggle, and the output file path from the command line to the
rendering and file-write code paths. Each entity is described by the
fields that are relevant to this feature; orthogonal fields are noted but
not specified.

## CliOptions

**Source**: `src/ai-cli/Models/CliOptions.cs`

**Role**: Plain mutable carrier produced by `CommandLineBuilder.ParseOptions`
from a `ParseResult`. Holds the user's verbatim choices for one
invocation, including the three switches this feature owns.

| Field | Type | Default | Constraint |
|-------|------|---------|------------|
| `OutputFile` | `string?` | `null` | Free-form filesystem path; the OS (not the CLI) validates it. |
| `Format` | `string` | `"text"` | MUST be exactly `"text"` or `"json"` (enforced by the root command validator). |
| `Stream` | `bool` | `false` | No CLI-side constraint. |

Other fields (`Prompt`, `FilePath`, `UseStdin`, `Model`, `Temperature`,
`MaxTokens`, `Config`) belong to sibling features.

**Lifecycle**: Constructed once per invocation in `ParseOptions`, then
mutated exactly once inside `Program.HandleCommandAsync` to merge in
fallback values from the active `ModelConfiguration` (for `Format` and
`Stream`). `OutputFile` has no configuration-level fallback — it must be
supplied per-invocation if a file output is desired.

## AIRequest

**Source**: `src/ai-cli/Models/AIRequest.cs`

**Role**: Immutable record describing a single API call. Carries the
`Stream` flag (this feature's contribution) forward to the HTTP layer.

| Field | Type | Required | Relevance to this feature |
|-------|------|----------|---------------------------|
| `Prompt` | `string` | yes | Sibling feature. |
| `Model` | `string` | yes | Sibling feature. |
| `Temperature` | `float` | no | Sibling feature. |
| `MaxTokens` | `int?` | no | Sibling feature. |
| `Stream` | `bool` | no (default `false`) | Selects between `OpenAIClient.SendRequestAsync` and `SendStreamingRequestAsync`, and is serialized as the JSON `stream` field. |

> **Note**: `Format` is intentionally NOT a field of `AIRequest`. Format
> is a pure rendering decision made after the response is received; the
> upstream provider needs no knowledge of it.

**Relationships**:
- `PromptService.ProcessPromptAsync` constructs an `AIRequest` with
  `Stream = false`.
- `PromptService.ProcessStreamingPromptAsync` constructs an `AIRequest`
  with `Stream = true`.
- `OpenAIClient.SendStreamingRequestAsync` defensively re-asserts the
  flag via `request with { Stream = true }` before serializing.

## AIResponse (non-streaming)

**Source**: `src/ai-cli/Models/AIResponse.cs`

**Role**: The non-streaming response payload. Two of its fields are the
direct outputs of this feature's rendering fork.

| Field | Type | Notes |
|-------|------|-------|
| `Content` | `string` | The assistant's reply text; selected for stdout/file when `Format == "text"`. |
| `Model` | `string` | Echoed from the provider; not used by this feature. |
| `RawResponse` | `string` | The verbatim provider JSON body; selected for stdout/file when `Format == "json"`. |
| `Success` | `bool` | False on non-2xx or transport error; surfaces as `HttpRequestException` in `Program.ProcessNonStreamingRequest`. |
| `ErrorMessage` | `string?` | Populated when `Success` is false. |

## Streaming chunk sequence

**Source**: `IAIClient.SendStreamingRequestAsync` (interface in
`src/ai-cli/Application/IAIClient.cs`, implementation in
`src/ai-cli/Infrastructure/OpenAIClient.cs`).

**Role**: A pull-based async sequence of plain-text fragments produced
by parsing the OpenAI SSE response stream.

| Property | Type | Notes |
|----------|------|-------|
| Element type | `string` | Each element is the value of `choices[0].delta.content` from one SSE chunk. |
| Termination | implicit | Producer stops at `data: [DONE]` or on end-of-stream. |
| Error semantics | non-throwing | A non-success HTTP status causes the producer to log an error and `yield break` (no chunks). A `JsonException` on an individual chunk causes a warning log and `continue`. |
| Cancellation | `CancellationToken` | Honored via `[EnumeratorCancellation]` and propagated to `ReadLineAsync`. |

## ModelConfiguration (fallback defaults)

**Source**: `src/ai-cli/Models/UserSettings.cs`

**Role**: A persisted, named bundle of defaults selected by the user via
`--config`. Supplies fallback values for `Format` and `Stream` when the
corresponding CLI switch is not specified.

| Field | Type | Default | Relevance to this feature |
|-------|------|---------|---------------------------|
| `Format` | `string` | `"text"` | Used when CLI did not pass `--format`. |
| `Stream` | `bool` | `false` | Used when CLI did not pass `--stream`. |

Other fields belong to sibling features.

**Resolution rule** (enforced in `Program.HandleCommandAsync`):
```
effective.Format = CLI.Format != "text" ? CLI.Format : config.Format
effective.Stream = CLI.Stream != false  ? CLI.Stream : config.Stream
```

Sentinel-default comparison is a documented quirk — see
`plan.md → Complexity Tracking` of the sibling spec
`002-model-and-generation-parameters`.

## Output File

**Source**: produced by `Program.WriteToFileAsync` in
`src/ai-cli/Program.cs`.

**Role**: The on-disk persistence sink. Not a class — described here as
a logical entity for the data model.

| Aspect | Value | Source |
|--------|-------|--------|
| Path | `CliOptions.OutputFile` (verbatim) | User input. |
| Content (text mode) | `AIResponse.Content`, ANSI-stripped | `Program.ProcessNonStreamingRequest` + `WriteToFileAsync`. |
| Content (json mode) | `AIResponse.RawResponse`, ANSI-stripped | Same. |
| Content (streaming) | concatenation of all yielded chunks, ANSI-stripped | `Program.ProcessStreamingRequest` accumulates via `StringBuilder`. |
| ANSI sanitization regex | `\x1B\[[0-9;]*[mGK]` | Hard-coded in `WriteToFileAsync`. |
| Unix mode | `UserRead | UserWrite` (0600) | `File.SetUnixFileMode` when `!OperatingSystem.IsWindows()`. |
| Windows mode | inherited NTFS ACL | No explicit step; permission code path skipped. |
| Permission failures | swallowed | `try/catch { }` around `SetUnixFileMode`. |
| Write semantics | overwrite | `File.WriteAllTextAsync` truncates existing files. |

## State transitions

This feature has no stateful workflow per se, but the streaming case
exhibits a producer/consumer interaction worth documenting:

### Non-streaming path

```
CliOptions  ── ParseOptions ──▶ CliOptions
   │
   ├─ Program merges Format/Stream with ModelConfiguration
   │
   ▼
PromptService.ProcessPromptAsync → AIRequest (Stream=false)
   │
   ▼
OpenAIClient.SendRequestAsync → AIResponse
   │
   ▼
Program.ProcessNonStreamingRequest:
   output := (Format == "json") ? response.RawResponse : response.Content
   Console.Write(output)
   if (OutputFile != null) WriteToFileAsync(OutputFile, output)
```

### Streaming path

```
CliOptions  ── ParseOptions ──▶ CliOptions  (Stream=true)
   │
   ├─ Program merges Format/Stream with ModelConfiguration
   │
   ▼
PromptService.ProcessStreamingPromptAsync → AIRequest (Stream=true)
   │
   ▼
OpenAIClient.SendStreamingRequestAsync reads SSE:
   for each "data: <json>" line:
     parse → delta.content → yield
   stop on "data: [DONE]"
   │
   ▼
Program.ProcessStreamingRequest:
   buffer := new StringBuilder()
   for each chunk:
     if (Format == "json")
       Console.Write({"content": chunk})
     else
       Console.Write(chunk)
     buffer.Append(chunk)
   if (OutputFile != null) WriteToFileAsync(OutputFile, buffer.ToString())
```

### File-write substate (`WriteToFileAsync`)

```
content
   │
   ├─ Regex.Replace(content, "\x1B\\[[0-9;]*[mGK]", "")
   ▼
File.WriteAllTextAsync(path, sanitized)
   │
   ├─ if (!OperatingSystem.IsWindows())
   │     try { File.SetUnixFileMode(path, UserRead | UserWrite) }
   │     catch { /* swallowed */ }
   ▼
done
```
