# Contract: Streaming Pipeline

**Source**: `src/ai-cli/Infrastructure/OpenAIClient.cs` (producer);
`src/ai-cli/Application/PromptService.cs` (forwarder);
`src/ai-cli/Program.cs` → `ProcessStreamingRequest` (consumer).

This contract describes the end-to-end streaming pipeline from the
OpenAI-compatible SSE response to the stdout/file rendering.

## Producer: `OpenAIClient.SendStreamingRequestAsync`

**Signature**:
```csharp
IAsyncEnumerable<string> SendStreamingRequestAsync(
    AIRequest request,
    CancellationToken cancellationToken = default)
```

**Behavior**:

1. Sends `POST {baseUrl}/chat/completions` with a JSON body produced by
   `CreateRequestBody(request with { Stream = true })`. The body contains
   `model`, `messages`, `temperature`, `max_tokens` (omitted if null),
   and `stream: true`.
2. Reads the response with `HttpCompletionOption` implicit default —
   the response body is consumed line-by-line as a `Stream` via
   `StreamReader.ReadLineAsync(cancellationToken)`.
3. For each line:
   - Ignores lines that do not start with `"data: "`.
   - Slices `line[6..]` to obtain the payload portion.
   - If the payload is `"[DONE]"`, breaks the loop (clean termination).
   - Otherwise attempts `JsonNode.Parse(data)`; on `JsonException` it
     logs a warning (`"Failed to parse streaming response chunk: {Data}"`)
     and continues.
   - Extracts `jsonData["choices"][0]["delta"]["content"]?.ToString()`.
   - If non-empty, `yield return delta;`.
4. On non-2xx HTTP status: logs the error, then `yield break` (no
   chunks). Does NOT throw.

**Error semantics**: The producer is intentionally exception-quiet on
the streaming path. A transport failure mid-stream surfaces as a
truncated chunk sequence; the consumer must rely on its own
cancellation/exit-code logic to distinguish "empty" from "broken".

## Forwarder: `PromptService.ProcessStreamingPromptAsync`

**Signature**:
```csharp
IAsyncEnumerable<string> ProcessStreamingPromptAsync(
    CliOptions options,
    CancellationToken cancellationToken = default)
```

**Behavior**:

1. Resolves the prompt text from `CliOptions` (inline, file, or stdin).
2. Constructs an `AIRequest` with `Stream = true` and copies
   `Model`, `Temperature`, `MaxTokens` verbatim.
3. Delegates to `IAIClient.SendStreamingRequestAsync` and re-yields each
   chunk to the caller without transformation.
4. After the stream completes, if `options.FilePath` was used (file-as-
   prompt input, a sibling-feature concern), deletes the prompt file.

## Consumer: `Program.ProcessStreamingRequest`

**Signature**:
```csharp
static async Task ProcessStreamingRequest(
    IPromptService promptService,
    CliOptions options,
    CancellationToken cancellationToken)
```

**Behavior**:

1. Allocates a `StringBuilder` to buffer the full text for the optional
   file write.
2. Iterates the chunk sequence with `await foreach`.
3. For each chunk:
   - If `options.Format == "json"`:
     - Wraps as `JsonSerializer.Serialize(new { content = chunk })` (so
       embedded quotes/newlines are escaped) and writes that JSON value
       to stdout.
   - Else (`"text"`):
     - Writes the raw chunk to stdout.
   - Appends the raw (unwrapped) chunk to the `StringBuilder`.
4. After the stream ends, if `options.OutputFile` is non-empty, calls
   `WriteToFileAsync(options.OutputFile, content.ToString())` (passing
   the unwrapped text — see `output-file-security.md`).

## Wire format (input side)

OpenAI-compatible SSE response body, e.g.:

```
data: {"choices":[{"delta":{"content":"Hello"}}]}

data: {"choices":[{"delta":{"content":"! How"}}]}

data: {"choices":[{"delta":{"content":" can I help?"}}]}

data: [DONE]
```

Blank lines are tolerated (they do not match the `data: ` prefix and
are silently ignored).

## Wire format (output side — JSON streaming mode)

For `--stream --format json`, stdout receives a concatenation of
self-contained JSON values, one per chunk, with no separator between
them:

```
{"content":"Hello"}{"content":"! How"}{"content":" can I help?"}
```

Consumers must parse one JSON value at a time. The output is **not** a
single JSON document.

## Cancellation contract

- The consumer's `cancellationToken` flows through `PromptService` and
  `OpenAIClient` and is honored by `ReadLineAsync(cancellationToken)`.
- On cancellation, the `IAsyncEnumerable` terminates; the
  `StringBuilder` content collected so far is discarded (the
  `OutputFile` write happens *after* the loop, so a cancelled stream
  produces no file).

## Test coverage

- `src/ai-cli.Tests/Infrastructure/OpenAIClientTests.cs`
  → `SendStreamingRequestAsync_WithValidRequest_ShouldReturnChunks`
  exercises the SSE → string-chunk decoding with a three-chunk fixture
  terminated by `data: [DONE]`.
- `src/ai-cli.Tests/Application/PromptServiceTests.cs`
  → `ProcessStreamingPromptAsync_WithInlinePrompt_ShouldReturnChunks`
  asserts each chunk produced by the mocked `IAIClient` is re-yielded
  unmodified.
- `src/ai-cli.Tests/Application/PromptServiceTests.cs`
  → `ProcessStreamingPromptAsync_WithFilePrompt_ShouldDeleteFileAfterProcessing`
  asserts the prompt-file delete-after-stream behavior (orthogonal to
  this feature but exercises the streaming termination path).
