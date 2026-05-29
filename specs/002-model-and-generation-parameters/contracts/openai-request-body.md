# Contract: OpenAI-compatible Request Body

**Source**: `src/ai-cli/Infrastructure/OpenAIClient.cs` — `CreateRequestBody(AIRequest request)`

This contract describes how the resolved model, temperature, and
max-tokens values are serialized into the HTTP request body sent to the
OpenAI-compatible chat completions endpoint.

## Endpoint

```
POST {baseUrl}/chat/completions
Content-Type: application/json
Authorization: Bearer {apiKey}        (only set when apiKey is non-empty)
User-Agent:    ai-cli/1.0
```

`baseUrl` defaults to `https://api.openai.com/v1` and is overridable per
model configuration. The endpoint path is identical for non-streaming
and streaming requests.

## Request body shape

The body is produced from this anonymous object:

```csharp
new
{
    model        = request.Model,
    messages     = new[] { new { role = "user", content = request.Prompt } },
    temperature  = request.Temperature,
    max_tokens   = request.MaxTokens,
    stream       = request.Stream
}
```

Serialized via `System.Text.Json` with:

- `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`
- `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`

## Field-by-field contract

| JSON field | Source | Type | Notes |
|------------|--------|------|-------|
| `model` | `AIRequest.Model` | string | Verbatim. |
| `messages` | derived from `AIRequest.Prompt` | array of `{role, content}` | Always a single `user` message in this feature. |
| `temperature` | `AIRequest.Temperature` | number | Always sent. |
| `max_tokens` | `AIRequest.MaxTokens` | integer or omitted | Omitted entirely when null (because of `WhenWritingNull`). |
| `stream` | `AIRequest.Stream` | boolean | `true` for streaming, `false` otherwise. |

## Examples

**With max_tokens supplied**:

```json
{
  "model": "gpt-4",
  "messages": [{"role": "user", "content": "Hello"}],
  "temperature": 0.7,
  "max_tokens": 100,
  "stream": false
}
```

**Without max_tokens** (null → field omitted):

```json
{
  "model": "gpt-4",
  "messages": [{"role": "user", "content": "Hello"}],
  "temperature": 0.7,
  "stream": false
}
```

**Streaming request** (`stream = true`):

```json
{
  "model": "gpt-4",
  "messages": [{"role": "user", "content": "Hello"}],
  "temperature": 0.7,
  "max_tokens": 100,
  "stream": true
}
```

## Error contract

If the provider rejects the request (any non-2xx response):

- `SendRequestAsync` returns an `AIResponse` with `Success = false` and
  `ErrorMessage = "API request failed with status {StatusCode}"`.
- `SendStreamingRequestAsync` logs the error and yields no chunks.
- `Program.ProcessNonStreamingRequest` re-throws as
  `HttpRequestException`, which `HandleCommandAsync` catches and maps to
  `ExitCodes.ApiError` (`2`).

## Test coverage

See `src/ai-cli.Tests/Infrastructure/OpenAIClientTests.cs`:

- `SendRequestAsync_WithValidRequest_ShouldReturnSuccessResponse` —
  end-to-end happy path with `Temperature = 0.7f`.
- `SendRequestAsync_WithApiError_ShouldReturnErrorResponse` — non-2xx
  surfaces a `Success = false` response.
- `SendStreamingRequestAsync_WithValidRequest_ShouldReturnChunks` —
  parameters survive into the streaming request.

> Gap: there is no test today that captures the outbound
> `HttpRequestMessage` and asserts on the exact `model`, `temperature`,
> and `max_tokens` keys. This is noted in `plan.md → Complexity Tracking`.
