# Feature Specification: Model and Generation Parameters

**Feature Branch**: `002-model-and-generation-parameters`

**Created**: 2026-05-28

**Status**: Draft (reverse-engineered from existing implementation)

**Input**: User description: "Select the AI model and tune generation behavior via `-m/--model`, `--temperature` (0.0–2.0, default 1), and `--max-tokens`. Includes how these values flow through `CliOptions` → `OpenAIClient` to the OpenAI-compatible API request."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select which AI model handles the prompt (Priority: P1)

A user wants to direct their prompt to a specific AI model (for example a
higher-capability model for a complex question, or a cheaper model for a
trivial one) without re-running the configuration menu or editing settings
files.

**Why this priority**: Model selection is the single most consequential
generation parameter — it determines cost, latency, capability, and
provider routing. Without per-invocation model override the CLI degrades
into a single-model wrapper.

**Independent Test**: Invoke the CLI with `--prompt "Test" --model gpt-4`
and confirm that the request reaching the AI service identifies `gpt-4`
as its model, regardless of the configured default. Verifiable
independently of temperature and max-tokens.

**Acceptance Scenarios**:

1. **Given** the user has a valid default model configuration, **When**
   they run the CLI with `--model gpt-4`, **Then** the outbound API
   request specifies `gpt-4` as the model and the response is associated
   with that model identifier.
2. **Given** the user prefers a short alias, **When** they run the CLI
   with `-m gpt-4`, **Then** behavior is identical to `--model gpt-4`.
3. **Given** the user is on Windows and uses the forward-slash
   convention, **When** they run the CLI with `/m gpt-4`, **Then** the
   selection is accepted exactly as with `-m` or `--model`.
4. **Given** the user does not supply any model switch, **When** they
   run the CLI, **Then** the model from the active model configuration
   is used; if no model configuration override applies, the documented
   built-in default (`gpt-3.5-turbo`) is used.

---

### User Story 2 - Control randomness of the AI response with a temperature (Priority: P1)

A user wants to control how deterministic or creative the AI response
should be by supplying a temperature value, expressed as a floating-point
number between 0.0 (most deterministic) and 2.0 (most creative).

**Why this priority**: Temperature is the most-used generation knob after
the model itself. Power users tuning prompts or building deterministic
pipelines need a reliable way to set it from the command line, and an
out-of-range value must be rejected before any API cost is incurred.

**Independent Test**: Invoke the CLI with `--temperature 0.5` and a valid
prompt; confirm the outbound API request carries `temperature: 0.5`. Then
invoke with `--temperature 3.0` and confirm the CLI rejects the value
without contacting the API.

**Acceptance Scenarios**:

1. **Given** the user supplies `--temperature 0.7`, **When** the CLI
   builds the API request, **Then** the request carries the temperature
   value `0.7`.
2. **Given** the user supplies `--temperature 0` or `--temperature 2`,
   **When** the CLI validates the value, **Then** both extremes are
   accepted as in-range.
3. **Given** the user supplies `--temperature 3.0` (or any value
   outside `[0.0, 2.0]`), **When** the CLI parses the arguments, **Then**
   the invocation fails with an error message stating that temperature
   must be between 0.0 and 2.0, and no API call is made.
4. **Given** the user supplies `--temperature 0,5` in a non-invariant
   locale, **When** the CLI parses the value, **Then** the CLI still
   accepts the canonical dot-decimal form (e.g. `0.5`) regardless of the
   operating system's locale, and rejects locale-specific forms with a
   parse error rather than silently misinterpreting them.
5. **Given** the user omits `--temperature`, **When** the CLI builds the
   request, **Then** the value defaults to `1.0` (or, if a model
   configuration overrides the default and the user did not specify a
   value, that configured default is used instead).

---

### User Story 3 - Cap the length of the AI response with max tokens (Priority: P2)

A user wants to limit how many tokens the AI service may generate in its
response, in order to control cost, latency, and worst-case output size.

**Why this priority**: Token caps protect against runaway responses and
budget surprises. They are essential for any non-trivial automation that
embeds the CLI in pipelines or scripts, but a sensible "no cap" default
remains acceptable for interactive use, hence P2 rather than P1.

**Independent Test**: Invoke the CLI with `--max-tokens 100` and confirm
the outbound API request carries that limit. Invoke without the switch
and confirm the request omits the field rather than sending a zero or
null value that would be misinterpreted by the API.

**Acceptance Scenarios**:

1. **Given** the user supplies `--max-tokens 100`, **When** the CLI
   builds the request, **Then** the outbound API payload contains a
   `max_tokens` field with value `100`.
2. **Given** the user omits `--max-tokens` and no configured default
   exists, **When** the CLI builds the request, **Then** the outbound
   API payload omits the `max_tokens` field entirely (rather than
   sending null or zero), allowing the provider to apply its own default.
3. **Given** the user supplies `--max-tokens 100` while also using
   `--stream`, **When** the CLI sends the streaming request, **Then**
   the same `max_tokens: 100` cap is included in the streaming request
   body.

---

### User Story 4 - Combine model, temperature, and max-tokens in a single call (Priority: P2)

A user fine-tuning a prompt run wants to set the model, temperature, and
max-tokens together on a single invocation, so they can reproduce a
specific generation configuration from a script or documentation.

**Why this priority**: Reproducibility matters for prompt engineering and
batch jobs. The composition of all three switches must be self-consistent
and must override per-configuration defaults predictably.

**Independent Test**: Invoke the CLI with `--prompt "x" --model gpt-4
--temperature 0.7 --max-tokens 100` and confirm all three values appear
in the outbound API request body together.

**Acceptance Scenarios**:

1. **Given** the user supplies all three switches in one invocation,
   **When** the CLI sends the request, **Then** the API payload carries
   exactly the model, temperature, and max-tokens that were provided.
2. **Given** the user supplies only some switches, **When** the CLI
   sends the request, **Then** the unspecified parameters fall back to
   the active model configuration's defaults, and only as a last resort
   to the built-in defaults documented in `--help`.

---

### Edge Cases

- **Temperature out of range**: Values < 0.0 or > 2.0 are rejected at
  parse time with a human-readable error; no API request is sent and the
  process exits with the "invalid arguments" exit code.
- **Non-numeric temperature**: A non-parseable temperature string is
  rejected with "Invalid temperature value. Must be a number." and the
  invocation aborts before any API call.
- **Locale-sensitive parsing**: Temperature parsing uses an
  invariant/dot-decimal format so behavior is stable across operating
  systems and user locales.
- **Negative or zero max-tokens**: The CLI does not enforce a positive
  lower bound on `--max-tokens`; any integer the user supplies is passed
  through to the API, which is responsible for rejecting nonsensical
  values. (See Assumptions.)
- **Unknown model name**: The CLI does not validate model names against
  a whitelist; an unrecognised model is passed through and the API is
  responsible for returning an error, which the CLI surfaces as an API
  failure with exit code `2`.
- **Streaming + parameters**: When `--stream` is set, model, temperature,
  and max-tokens are still applied to the streaming request body
  identically to the non-streaming case.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept a model identifier from the
  command line via the switches `--model`, `-m`, or `/m`.
- **FR-002**: The system MUST accept a temperature value from the
  command line via the switch `--temperature`, expressed as a
  floating-point number.
- **FR-003**: The system MUST accept a maximum-tokens value from the
  command line via the switch `--max-tokens`, expressed as an integer.
- **FR-004**: The system MUST validate that the supplied temperature is
  within the inclusive range `[0.0, 2.0]` and MUST reject out-of-range
  values at argument parse time, before any API request is made.
- **FR-005**: The system MUST reject non-numeric temperature values
  with a clear error message before any API request is made.
- **FR-006**: The system MUST parse temperature values using a
  locale-independent (invariant, dot-decimal) numeric format.
- **FR-007**: When the user does not supply `--model`, the system MUST
  use the model identifier from the active model configuration; if no
  active model configuration exists, it MUST fall back to the built-in
  default `gpt-3.5-turbo`.
- **FR-008**: When the user does not supply `--temperature`, the system
  MUST use the temperature from the active model configuration; if no
  active configuration override applies, it MUST fall back to the
  built-in default `1.0`.
- **FR-009**: When the user does not supply `--max-tokens`, the system
  MUST use the value from the active model configuration if one is set,
  otherwise it MUST omit the field from the outbound API request body
  entirely (rather than sending null or zero).
- **FR-010**: User-supplied switches MUST override the corresponding
  values from the active model configuration.
- **FR-011**: The system MUST forward the resolved model, temperature,
  and max-tokens values to the AI service as fields named `model`,
  `temperature`, and `max_tokens` (snake_case) in the JSON request body
  sent to the OpenAI-compatible chat completions endpoint.
- **FR-012**: The system MUST apply the same model, temperature, and
  max-tokens resolution to both non-streaming and streaming requests.
- **FR-013**: The system MUST report an API error (exit code `2`) when
  the AI service rejects a request because of an invalid model,
  temperature, or max-tokens value, surfacing the provider's status to
  the user.
- **FR-014**: The system MUST NOT log API keys when logging the
  model/temperature/max-tokens values used for a request.

### Key Entities

- **Generation Parameters (in `CliOptions`)**: The user-facing surface of
  this feature. Holds the chosen model identifier, the temperature, and
  the optional maximum-tokens cap, in addition to other CLI flags.
- **AI Request**: An immutable description of a single AI invocation,
  carrying the prompt text together with the resolved model, temperature,
  max-tokens, and streaming flag. This is the boundary between the
  application layer and the infrastructure layer.
- **Model Configuration**: A persisted, named bundle of defaults stored
  in user settings, which supplies model identifier, temperature, and
  max-tokens (among other fields) when the user has not overridden them
  on the command line.
- **AI Response**: The result returned for a non-streaming request,
  including the content text and the model identifier the provider
  actually used (which may differ from the requested one for proxied or
  aliased models).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can change the model used for a single invocation
  by adding a single switch (`-m <name>` or `--model <name>`) and
  observes the request reach the API with that model in 100% of valid
  cases.
- **SC-002**: 100% of temperature values outside `[0.0, 2.0]` are
  rejected before any HTTP request is issued.
- **SC-003**: When `--max-tokens` is omitted and no configuration
  override is set, the outbound JSON payload contains no `max_tokens`
  field (verified by direct inspection of the request body).
- **SC-004**: All three switches (`--model`, `--temperature`,
  `--max-tokens`) function identically with their short (`-m`),
  forward-slash (`/m`), and long-form spellings where short/forward-slash
  aliases are defined.
- **SC-005**: Temperature parsing produces identical results on Linux,
  macOS, and Windows for any user locale (no comma/dot regressions).
- **SC-006**: When the API rejects a request due to invalid generation
  parameters, the CLI exits with code `2` and surfaces the provider's
  status code in its error output.

## Assumptions

- The application targets OpenAI-compatible chat completion APIs that
  accept `model`, `temperature`, and `max_tokens` as top-level JSON
  fields on the `/chat/completions` endpoint.
- A persisted "active" model configuration exists at runtime (selected
  via `--config`) and supplies fallback defaults for these three
  parameters; if none exists, the CLI emits a configuration error
  before sending a request.
- The `--max-tokens` value is forwarded to the provider without lower-
  or upper-bound validation by the CLI; provider-side validation is
  considered authoritative.
- Model identifier strings are passed through unchanged; the CLI does
  not maintain a curated list of supported models.
- Temperature parsing uses invariant culture so identical input strings
  produce identical numeric values regardless of the host operating
  system or user locale.
- These three parameters are independent: changing one does not alter
  the resolution of the others.

## Retrospec Metadata

**Generated**: 2026-05-28
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 9 source files across 2 projects (`ai-cli`, `ai-cli.Tests`)
**Reference implementation branch**: feature/nodejs
