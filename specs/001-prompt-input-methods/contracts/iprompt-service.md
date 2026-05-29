# Contract: IPromptService

`AiCli.Application.IPromptService` is the programmatic seam between the
CLI layer and the prompt-resolution logic. It accepts a fully parsed
`CliOptions` (including the derived `UseStdin` flag) and is responsible
for turning that into an AI request.

Source:

- Interface: `src/ai-cli/Application/IPromptService.cs`
- Implementation: `src/ai-cli/Application/PromptService.cs`
- Tests: `src/ai-cli.Tests/Application/PromptServiceTests.cs`

## Members

### `Task<AIResponse> ProcessPromptAsync(CliOptions options, CancellationToken cancellationToken = default)`

Performs a non-streaming request.

**Inputs**:

- `options.Prompt` — inline prompt text (may be null/empty)
- `options.FilePath` — path to a file containing the prompt (may be
  null/empty)
- `options.UseStdin` — `true` to read from stdin when neither of the
  above is set
- `options.Model`, `options.Temperature`, `options.MaxTokens` — passed
  through to the AI client
- `cancellationToken` — propagated to file read and the AI client

**Output**: `AIResponse` produced by the underlying `IAIClient`. The
service does not wrap or transform the response other than building the
`AIRequest`.

**Side effects**:

- Reads `options.FilePath` from disk if set.
- After `IAIClient.SendRequestAsync` returns, deletes `options.FilePath`
  if it was used. Deletion failures are logged as warnings; they do not
  fail the call.
- Logs `"Processing prompt with model {Model}"` at information level.

### `IAsyncEnumerable<string> ProcessStreamingPromptAsync(CliOptions options, CancellationToken cancellationToken = default)`

Same input semantics, but the AI client is invoked via
`SendStreamingRequestAsync` and the result is yielded chunk-by-chunk.

**Side effects** (in addition to the streaming output):

- The same file-deletion behaviour applies, but it runs after the
  underlying `await foreach` completes (i.e. after the consumer has
  drained the stream).
- Logs `"Processing streaming prompt with model {Model}"` at information
  level.

## Resolution semantics (shared by both methods)

`PromptService.GetPromptTextAsync` evaluates the source in this order:

1. **Inline**: if `options.Prompt` is non-empty, return it.
2. **File**: if `options.FilePath` is non-empty:
   - If the file does not exist, throw `FileNotFoundException` with
     message `"Prompt file not found: {FilePath}"`.
   - Otherwise return `await File.ReadAllTextAsync(filePath, ct)`.
3. **Stdin (interactive)**: if `options.UseStdin` is `true` and
   `!Console.IsInputRedirected`, read lines from `Console.ReadLine()`
   in a loop until null. Return the concatenated lines (one per
   `AppendLine`) with trailing whitespace trimmed.
4. **Stdin (redirected)**: if `options.UseStdin` is `true` and
   `Console.IsInputRedirected` is `true`, read the whole stream via
   `StreamReader.ReadToEndAsync` and return it.
5. **None**: throw `InvalidOperationException("No prompt source specified")`.

## Error contract

| Condition | Exception |
|-----------|-----------|
| `--file` path missing on disk | `FileNotFoundException` |
| No prompt source resolvable | `InvalidOperationException` |
| `cancellationToken` triggered during stdin read or file read | `OperationCanceledException` / `TaskCanceledException` |
| Underlying AI request fails | propagated from `IAIClient` (typically `HttpRequestException`) |

`Program.HandleCommandAsync` maps these to exit codes:
`FileNotFoundException` / `UnauthorizedAccessException` → `FileError`,
`TaskCanceledException` → `ApiError`, everything else → `UnknownError`.

## Tested behaviours (from `PromptServiceTests`)

- `ProcessPromptAsync_WithInlinePrompt_ShouldReturnResponse` — verifies
  the inline branch and that the AI client receives the exact text.
- `ProcessPromptAsync_WithFilePrompt_ShouldReadFileAndReturnResponse` —
  verifies the file branch AND that the prompt file is deleted
  afterwards.
- `ProcessPromptAsync_WithNonExistentFile_ShouldThrowFileNotFoundException`
  — verifies the missing-file error.
- `ProcessPromptAsync_WithNoPromptSource_ShouldThrowInvalidOperationException`
  — verifies the "None" terminal state.
- `ProcessStreamingPromptAsync_WithInlinePrompt_ShouldReturnChunks` —
  parallels the non-streaming inline test for the streaming API.
- `ProcessStreamingPromptAsync_WithFilePrompt_ShouldDeleteFileAfterProcessing`
  — verifies that file deletion also occurs on the streaming path,
  after consumption.
