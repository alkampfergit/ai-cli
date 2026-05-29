# Requirements Checklist: Model and Generation Parameters

**Purpose**: Verify that the existing implementation satisfies every
functional requirement in `spec.md`. Items are pre-marked `[x]` because
this checklist was produced by reverse-engineering an implemented
feature.

**Created**: 2026-05-28
**Feature**: [spec.md](../spec.md)

## Command-line surface

- [x] CHK001 `--model` is exposed with short alias `-m` and Windows alias `/m` (FR-001) — defined in `CommandLineBuilder.cs` and exercised by `CommandLineBuilderTests.ParseOptions_AllModelSyntaxVariations_ShouldParseCorrectly`.
- [x] CHK002 `--temperature` accepts a floating-point argument (FR-002) — defined with a custom `parseArgument` returning `float` in `CommandLineBuilder.cs`.
- [x] CHK003 `--max-tokens` accepts an integer argument (FR-003) — defined as `Option<int?>` in `CommandLineBuilder.cs`.

## Validation

- [x] CHK004 Temperature out of `[0.0, 2.0]` is rejected at parse time (FR-004) — see `rootCommand.AddValidator(...)` block and the test `CreateRootCommand_WithInvalidTemperature_ShouldFail`.
- [x] CHK005 Non-numeric temperature is rejected with a clear message (FR-005) — error string `"Invalid temperature value. Must be a number."` set inside the custom parser.
- [x] CHK006 Temperature parsing is locale-independent (FR-006) — parser uses `CultureInfo.InvariantCulture`.

## Defaults & override order

- [x] CHK007 `--model` defaults to `gpt-3.5-turbo` when neither CLI nor configuration overrides apply (FR-007) — default set via `getDefaultValue` and confirmed by `CliOptions.Model` initializer.
- [x] CHK008 `--temperature` defaults to `1.0` when not provided (FR-008) — set via `_temperatureOption.SetDefaultValue(1.0f)` and `CliOptions.Temperature = 1.0f`.
- [x] CHK009 `--max-tokens` is omitted from the request when not provided (FR-009) — `CliOptions.MaxTokens` and `AIRequest.MaxTokens` are `int?`; serializer uses `JsonIgnoreCondition.WhenWritingNull`.
- [x] CHK010 CLI switches override active `ModelConfiguration` defaults (FR-010) — `Program.HandleCommandAsync` resolves effective values prior to constructing the AI request.

## Forwarding to the API

- [x] CHK011 Resolved values are sent as JSON fields `model`, `temperature`, `max_tokens` (FR-011) — `OpenAIClient.CreateRequestBody` with `JsonNamingPolicy.SnakeCaseLower`.
- [x] CHK012 The same resolution applies to non-streaming and streaming requests (FR-012) — `SendStreamingRequestAsync` clones the request with `request with { Stream = true }`.
- [x] CHK013 API errors map to exit code 2 (FR-013) — `Program.HandleCommandAsync` catches `HttpRequestException` and returns `ExitCodes.ApiError`.
- [x] CHK014 API keys are not logged alongside generation parameters (FR-014) — `Program.HandleCommandAsync` logs the model only at Information level; API key is supplied separately to `OpenAIClient` and never included in log scopes for these calls.

## Test coverage

- [x] CHK015 Unit tests cover model parsing across all alias forms — `CommandLineBuilderTests.ParseOptions_AllModelSyntaxVariations_ShouldParseCorrectly`.
- [x] CHK016 Unit tests cover the in-range temperature happy path — `ParseOptions_WithFilePrompt_ShouldParseCorrectly`, `ParseOptions_WithAllOptions_ShouldParseCorrectly`.
- [x] CHK017 Unit tests cover the out-of-range temperature error path — `CreateRootCommand_WithInvalidTemperature_ShouldFail`.
- [x] CHK018 Unit tests cover max-tokens parsing — `ParseOptions_WithAllOptions_ShouldParseCorrectly`.
- [x] CHK019 Integration of `AIRequest.Temperature` through the HTTP client is exercised — `OpenAIClientTests.SendRequestAsync_WithValidRequest_ShouldReturnSuccessResponse`.

## Known gaps (not blocking — recorded for future work)

- [ ] CHK020 *(deferred)* Add an explicit assertion that the outbound `HttpRequestMessage` body contains `model`, `temperature`, and `max_tokens` keys with the expected resolved values — currently inferred indirectly via response round-trip tests.
- [ ] CHK021 *(deferred)* Replace sentinel-default override detection in `Program.HandleCommandAsync` with explicit "was specified on the command line?" tracking, so passing the literal default value via CLI is treated as an override.

## Notes

- Items CHK001–CHK019 are all `[x]` because the corresponding code or test
  exists today.
- Items CHK020–CHK021 are intentionally left unchecked to surface real
  gaps; they are also documented in `plan.md → Complexity Tracking`.
