# Requirements Checklist: Output Formatting and Destination

**Purpose**: Verify that the existing implementation satisfies every
functional requirement in `spec.md`. Items are pre-marked `[x]` because
this checklist was produced by reverse-engineering an implemented
feature.

**Created**: 2026-05-29
**Feature**: [spec.md](../spec.md)

## Command-line surface

- [x] CHK001 `--format` switch exists with default `"text"` (FR-001, FR-002) — declared in `CommandLineBuilder.cs` with `getDefaultValue: () => "text"`; mirrored by `CliOptions.Format = "text"`.
- [x] CHK002 `--stream` boolean flag exists, default `false` (FR-004) — declared in `CommandLineBuilder.cs` as `Option<bool>("--stream", ...)`.
- [x] CHK003 `--output-file` exposes aliases `-o` and `/o` (FR-005) — declared with `aliases: new[] { "--output-file", "-o", "/o" }` in `CommandLineBuilder.cs`; verified by the theory test `ParseOptions_AllOutputSyntaxVariations_ShouldParseCorrectly`.

## Validation

- [x] CHK004 `--format` accepts only `text` or `json`; any other value is rejected at parse time with exit code 1 (FR-003) — enforced by the root command validator block `if (format != "text" && format != "json") result.ErrorMessage = ...`; covered by `CreateRootCommand_WithInvalidFormat_ShouldFail`.

## Rendering — non-streaming

- [x] CHK005 Text mode writes assistant content to stdout (FR-006) — `Program.ProcessNonStreamingRequest`: `var output = options.Format == "json" ? response.RawResponse : response.Content; Console.Write(output);`.
- [x] CHK006 JSON mode writes the raw provider response verbatim (FR-007) — same line as above; `response.RawResponse` is the unmodified provider JSON body captured by `OpenAIClient.SendRequestAsync`.

## Rendering — streaming

- [x] CHK007 Streaming text mode writes each chunk to stdout in order (FR-008) — `Program.ProcessStreamingRequest`: the `else` branch writes `chunk` directly via `Console.Write`.
- [x] CHK008 Streaming JSON mode wraps each chunk as `{"content":"..."}` (FR-009) — `Program.ProcessStreamingRequest`: `JsonSerializer.Serialize(new { content = chunk })`.
- [x] CHK009 SSE pipeline reads `data: <json>` lines and extracts `choices[0].delta.content` (FR-010) — `OpenAIClient.SendStreamingRequestAsync` loop with `if (line.StartsWith("data: "))`, `JsonNode.Parse(data)`, `jsonData["choices"][0]["delta"]["content"]`.
- [x] CHK010 Malformed SSE chunks are skipped with a warning, stream continues (FR-011) — `catch (JsonException ex) { _logger.LogWarning(...); continue; }` inside the SSE loop.

## File output

- [x] CHK011 With `-o`, the rendered content is also written to the file (FR-012) — `Program.ProcessNonStreamingRequest` calls `WriteToFileAsync(options.OutputFile, output)` after `Console.Write(output)`.
- [x] CHK012 With `-o` + `--stream`, the concatenated text is written after the stream ends (FR-013) — `Program.ProcessStreamingRequest` accumulates chunks into a `StringBuilder` and calls `WriteToFileAsync(options.OutputFile, content.ToString())` after the `await foreach` loop completes.

## Security

- [x] CHK013 ANSI sequences `\x1B[<digits/semicolons>][mGK]` are stripped before writing to file (FR-014) — `WriteToFileAsync`: `Regex.Replace(content, @"\x1B\[[0-9;]*[mGK]", "")`.
- [x] CHK014 On Unix, the output file mode is set to `0600` (FR-015) — `WriteToFileAsync`: `File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite)` inside `!OperatingSystem.IsWindows()` branch.
- [x] CHK015 Failures of the Unix permission step are swallowed (FR-016) — the `SetUnixFileMode` call sits inside `try { … } catch { /* Ignore permission errors */ }`.
- [x] CHK016 On Windows, the Unix permission step is skipped entirely (FR-017) — guarded by `if (!OperatingSystem.IsWindows())`.

## Wiring & defaults

- [x] CHK017 `Stream` is forwarded to the AI service via `AIRequest.Stream` and the SSE-vs-JSON HTTP shape switches accordingly (FR-018) — `PromptService.ProcessStreamingPromptAsync` sets `Stream = true`; `OpenAIClient.SendStreamingRequestAsync` reads the stream incrementally and includes `stream: true` in the JSON body via `request with { Stream = true }`.
- [x] CHK018 Missing `--stream` / `--format` fall back to the active `ModelConfiguration`, then to the built-in defaults (FR-019) — `Program.HandleCommandAsync` computes `effectiveFormat = options.Format != "text" ? options.Format : defaultModelConfig.Format` and similarly for `Stream`; `ModelConfiguration.Format = "text"`, `ModelConfiguration.Stream = false` by default.
- [x] CHK019 File contents and output paths are not logged at Information level (FR-020) — only the model identifier is logged via `logger.LogInformation("Starting AI CLI with model {Model}", options.Model)`; no `Information` level log emits the content or the path.

## Test coverage

- [x] CHK020 Unit tests verify all three switches parse together — `CommandLineBuilderTests.ParseOptions_WithAllOptions_ShouldParseCorrectly`.
- [x] CHK021 Unit tests verify the invalid-format error path — `CommandLineBuilderTests.CreateRootCommand_WithInvalidFormat_ShouldFail`.
- [x] CHK022 Unit tests verify all `-o`/`/o`/`--output-file` aliases parse identically — `CommandLineBuilderTests.ParseOptions_AllOutputSyntaxVariations_ShouldParseCorrectly`.
- [x] CHK023 Unit tests verify the SSE → chunk pipeline — `OpenAIClientTests.SendStreamingRequestAsync_WithValidRequest_ShouldReturnChunks`.
- [x] CHK024 Unit tests verify chunk passthrough from `PromptService` — `PromptServiceTests.ProcessStreamingPromptAsync_WithInlinePrompt_ShouldReturnChunks`.

## Known gaps (not blocking — recorded for future work)

- [ ] CHK025 *(deferred)* Add a unit test for `WriteToFileAsync` that asserts the saved file is ANSI-free (no `0x1B` bytes in the output) for a content sample containing SGR/cursor/erase-in-line sequences.
- [ ] CHK026 *(deferred)* Add a Linux/macOS unit test for `WriteToFileAsync` that asserts the saved file's mode is `0600` after the call.
- [ ] CHK027 *(deferred)* Decide whether to log the swallowed `SetUnixFileMode` exception at Debug level so silent permission-setting failures are still recoverable from the log.
- [ ] CHK028 *(deferred)* Add a unit test for the streaming JSON mode that captures stdout and asserts it contains one `{"content": "..."}` value per chunk in the original order.
- [ ] CHK029 *(deferred)* Replace sentinel-default override detection in `Program.HandleCommandAsync` with explicit "was specified on the command line?" tracking, so passing `--format text` explicitly is treated as an override rather than coalescing with the configuration default.
- [ ] CHK030 *(deferred)* Broaden the ANSI-stripping regex (or replace with an ANSI parser) to cover non-SGR CSI sequences (`[H`, `[J`, mode-set/reset), OSC, DCS, and APC — currently only `[m`, `[G`, `[K` are sanitized.
- [ ] CHK031 *(deferred)* Decide whether the streaming non-success HTTP path should yield an error sentinel or throw, rather than yielding zero chunks silently.

## Notes

- Items CHK001–CHK024 are all `[x]` because the corresponding code or test
  exists today in the repository.
- Items CHK025–CHK031 are intentionally left unchecked to surface real
  gaps; they are also documented in `plan.md → Complexity Tracking` and
  `research.md`.
