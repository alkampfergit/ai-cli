# Requirements Checklist: Prompt Input Methods

**Purpose**: Verify that each functional requirement and success
criterion in `spec.md` is satisfied by the existing implementation.

**Created**: 2026-05-28

**Feature**: [spec.md](../spec.md)

> All items are pre-marked `[x]` because this checklist was generated
> retrospectively against shipped code. The "Notes" column traces each
> item to the file(s) that satisfy it.

## Functional Requirements

- [x] CHK001 FR-001: Inline prompt accepted via dedicated option.
  Notes: `_promptOption` declared in `CommandLineBuilder.CreateRootCommand`
  with aliases `--prompt`, `-p`, `/p`; read by `PromptService.GetPromptTextAsync`.
- [x] CHK002 FR-002: File-path prompt accepted via dedicated option.
  Notes: `_fileOption` declared in `CommandLineBuilder.CreateRootCommand`
  with aliases `--file`, `-f`, `/f`; read via `File.ReadAllTextAsync` in
  `PromptService.GetPromptTextAsync`.
- [x] CHK003 FR-003: Stdin accepted when no explicit source flag given.
  Notes: `UseStdin` derived in `CommandLineBuilder.ParseOptions`; stdin
  branches in `PromptService.GetPromptTextAsync`.
- [x] CHK004 FR-004: Sources are mutually exclusive at parse time.
  Notes: validator in `CommandLineBuilder.CreateRootCommand` rejects
  `sourceCount > 1` with message
  `"Only one prompt source can be specified: --prompt, --file, or stdin"`.
- [x] CHK005 FR-005: Deterministic resolution order inline → file → stdin.
  Notes: sequential `if` blocks in `PromptService.GetPromptTextAsync`.
- [x] CHK006 FR-006: Missing file raises a clear error and skips the AI call.
  Notes: `FileNotFoundException` thrown before any `_aiClient` call;
  mapped to `ExitCodes.FileError` in `Program.HandleCommandAsync`.
- [x] CHK007 FR-007: Prompt file deleted after processing; deletion
  failure logged but not fatal.
  Notes: `File.Delete` with `try/catch` + `_logger.LogWarning` in both
  `ProcessPromptAsync` and `ProcessStreamingPromptAsync`.
- [x] CHK008 FR-008: Interactive stdin read line-by-line, trimmed.
  Notes: `Console.ReadLine()` loop + `StringBuilder.AppendLine` +
  `TrimEnd()` in the `!Console.IsInputRedirected` branch.
- [x] CHK009 FR-009: Redirected stdin read to completion.
  Notes: `StreamReader(Console.OpenStandardInput()).ReadToEndAsync` in
  the redirected branch.
- [x] CHK010 FR-010: `--prompt` exposes `-p` and `/p` aliases.
  Notes: aliases array on `_promptOption`; covered by theory tests in
  `CommandLineBuilderTests.ParseOptions_AllPromptSyntaxVariations_*`.
- [x] CHK011 FR-011: `--file` exposes `-f` and `/f` aliases.
  Notes: aliases array on `_fileOption`; covered by theory tests in
  `CommandLineBuilderTests.ParseOptions_AllFileSyntaxVariations_*`.
- [x] CHK012 FR-012: Validation skipped in `--config` mode.
  Notes: `isConfigMode` short-circuit at the top of the validator in
  `CommandLineBuilder.CreateRootCommand`.
- [x] CHK013 FR-013: Missing source surfaces a clear error.
  Notes: `throw new InvalidOperationException("No prompt source specified")`
  in `PromptService.GetPromptTextAsync`.
- [x] CHK014 FR-014: Same resolution for streaming and non-streaming.
  Notes: both `ProcessPromptAsync` and `ProcessStreamingPromptAsync`
  call the same private `GetPromptTextAsync` helper.

## Success Criteria

- [x] CHK015 SC-001: Inline prompt delivered verbatim.
  Notes: asserted by
  `PromptServiceTests.ProcessPromptAsync_WithInlinePrompt_ShouldReturnResponse`.
- [x] CHK016 SC-002: File contents delivered verbatim.
  Notes: asserted by
  `PromptServiceTests.ProcessPromptAsync_WithFilePrompt_ShouldReadFileAndReturnResponse`.
- [x] CHK017 SC-003: Stdin payload delivered.
  Notes: covered indirectly — `PromptService` reads from
  `Console.OpenStandardInput()` in the redirected branch; no test
  currently asserts the pipe-through-shell scenario end to end. See
  research D4 for the known limitation.
- [x] CHK018 SC-004: Combined `--prompt` + `--file` rejected pre-call.
  Notes: validator error message; `Program.Main` exits with
  `InvalidArguments` when `parseResult.Errors.Count > 0`.
- [x] CHK019 SC-005: Missing file yields file-error exit code.
  Notes: `FileNotFoundException` caught in `Program.HandleCommandAsync`
  returning `ExitCodes.FileError`.
- [x] CHK020 SC-006: File deletion after successful run.
  Notes: asserted by
  `PromptServiceTests.ProcessPromptAsync_WithFilePrompt_ShouldReadFileAndReturnResponse`
  and `ProcessStreamingPromptAsync_WithFilePrompt_ShouldDeleteFileAfterProcessing`.
- [x] CHK021 SC-007: Interactive single-line input trimmed correctly.
  Notes: `TrimEnd()` at the end of the interactive stdin branch in
  `PromptService.GetPromptTextAsync`.

## Edge Cases

- [x] CHK022 Empty inline prompt treated as not provided.
  Notes: `string.IsNullOrEmpty(options.Prompt)` short-circuits, falls
  through to file/stdin.
- [x] CHK023 Empty file read produces empty string (no special handling).
  Notes: `File.ReadAllTextAsync` returns `""`; passed straight to the
  AI request.
- [x] CHK024 No source in non-interactive context raises a clear error.
  Notes: terminal `InvalidOperationException`.
- [x] CHK025 File deletion failure logged but does not affect response.
  Notes: try/catch + `LogWarning` in both processing methods.

## Notes

- This checklist is informational; it does not gate further work.
- Items CHK017 carries a known limitation (no automated coverage of the
  shell-piped stdin path). See `research.md` D4.
