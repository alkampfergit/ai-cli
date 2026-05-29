# Research: Output Formatting and Destination

Technical decisions visible in the implementation, with the rationale
that can be inferred from the code, comments, and conventional .NET CLI
practice.

## Decision 1: `Format` is a CLI / rendering concern only, NOT an `AIRequest` field

- **Decision**: `Format` lives on `CliOptions` and `ModelConfiguration`
  but is deliberately omitted from `AIRequest`. The renderer fork lives
  inside `Program.cs` (`ProcessNonStreamingRequest` /
  `ProcessStreamingRequest`).
- **Rationale**: The upstream OpenAI-compatible API has no notion of
  "client output format" — the JSON body it returns is always JSON
  regardless of what the CLI eventually prints. Adding `Format` to
  `AIRequest` would muddle the application/infrastructure boundary
  (the `IAIClient` would have a parameter it cannot act on).
- **Alternatives considered**:
  - Carry `Format` on `AIRequest` for symmetry with `Stream` →
    rejected because it would only ever be a no-op on the wire.

## Decision 2: Streaming uses `IAsyncEnumerable<string>` (chunks) rather than `IObservable` or a callback

- **Decision**: `IAIClient.SendStreamingRequestAsync` returns
  `IAsyncEnumerable<string>`; consumers iterate with `await foreach`.
- **Rationale**: `IAsyncEnumerable` is the idiomatic .NET 9 mechanism
  for pull-based asynchronous streams. It composes cleanly with
  `CancellationToken` (`[EnumeratorCancellation]`), backpressure
  (the consumer controls iteration speed), and stdout writes.
- **Alternatives considered**:
  - `IObservable<string>` (Rx) → rejected because of the extra
    dependency and the push-model awkwardness for synchronous stdout.
  - Action callbacks `Action<string>` → rejected because cancellation
    and completion signalling become ad-hoc.

## Decision 3: Streaming JSON mode emits one JSON value per chunk, not a single JSON array

- **Decision**: `ProcessStreamingRequest` serializes each chunk as
  `JsonSerializer.Serialize(new { content = chunk })` and writes the
  resulting JSON value to stdout, with no enclosing `[`/`]` or comma
  separators.
- **Rationale**: A single JSON array would require either:
  (a) buffering the entire response (defeating the purpose of streaming), or
  (b) custom logic to write `[`, then `,` between chunks, then `]` at
  the end — fragile in the presence of cancellation. A stream-of-values
  is a well-established format ("ndjson"-like) that downstream
  consumers can parse one value at a time.
- **Alternatives considered**:
  - Emit `[chunk1, chunk2, ...]` → rejected for the buffering / cancellation
    fragility reasons above.
  - Emit one chunk per line (`{"content":"…"}\n`) → effectively NDJSON;
    closer to a published standard but introduces newline semantics the
    current code does not need. Not adopted in the current implementation.

## Decision 4: ANSI stripping only at the file-write boundary, never on stdout

- **Decision**: `WriteToFileAsync` strips ANSI sequences with the regex
  `\x1B\[[0-9;]*[mGK]` before writing; `Console.Write` calls earlier in
  the pipeline pass the content through unchanged.
- **Rationale**: A terminal is expected to interpret ANSI escapes
  meaningfully — stripping them would render colored AI output as
  plain text, defeating the visual UX. A file on disk, however, is
  later read by tools that should not be color-controlled by a remote
  model. The two paths therefore have different threat models, so
  sanitization happens at the persistent-storage boundary only.
- **Alternatives considered**:
  - Strip ANSI on stdout too → rejected (kills legitimate terminal
    coloring).
  - Add a `--no-ansi` flag for stdout → rejected as premature
    flag-proliferation; not yet a user-visible requirement.

## Decision 5: ANSI regex covers only `[m`, `[G`, `[K` sequences

- **Decision**: The regex `\x1B\[[0-9;]*[mGK]` matches the three CSI
  final-byte classes: `m` (SGR), `G` (cursor horizontal absolute), and
  `K` (erase in line). It does NOT match other CSI sequences (`[H`,
  `[J`, `[A`–`[F`, …), nor OSC, DCS, APC, or single-character escapes.
- **Rationale**: SGR is the dominant terminal-injection vector a model
  is likely to emit (colors, bold, blink) — these are the sequences a
  user would unknowingly trigger by re-displaying the saved file. The
  small extension to `G` and `K` covers the most common
  "cursor-line clear" tricks. A complete sanitizer would require a
  real ANSI/VT100 parser, which is out of scope.
- **Alternatives considered**:
  - Strip ALL `\x1B[…]` sequences with a broader regex (`\x1B\[[^a-zA-Z]*[a-zA-Z]`)
    → rejected as overreach without test coverage of the additional
    classes.
  - Use an off-the-shelf sanitizer library → rejected for binary-size
    reasons (single-file self-contained executable).

## Decision 6: File mode is `0600` on non-Windows OSes, no Windows ACL hardening

- **Decision**: On any non-Windows host, the output file is set to
  `UserRead | UserWrite` (`0600`) via `File.SetUnixFileMode`. On
  Windows, no permission step runs; the file inherits the parent
  directory's NTFS ACL.
- **Rationale**:
  - Unix shared hosts (servers, dev boxes, CI runners) historically
    default to `0644`, which makes a saved AI response readable by any
    local user. Tightening to `0600` is the conservative default for
    output that may contain sensitive prompt context.
  - Windows ACL inheritance is generally sensible by default
    (e.g. `C:\Users\<me>\…` is owner-restricted), and applying an
    explicit DACL would require P/Invoke or `System.Security.AccessControl`
    code that is platform-conditional and noisier than the current
    one-liner.
- **Alternatives considered**:
  - Apply explicit NTFS ACL on Windows → rejected for complexity and
    minimal incremental security benefit.
  - Use `0640` (owner+group) on Unix → rejected as still too permissive
    on multi-tenant boxes with a group of administrators.

## Decision 7: Swallow exceptions from `SetUnixFileMode`

- **Decision**: The `SetUnixFileMode` call is wrapped in
  `try { … } catch { /* swallow */ }` with no logging in the catch.
- **Rationale**: Some filesystems (FUSE mounts, mounted SMB shares,
  certain CI ephemeral storage) raise on `chmod`. The file write
  itself has already succeeded by this point; failing the operation
  because of an ancillary permission step would surprise users with
  spurious errors on otherwise-correct output.
- **Alternatives considered**:
  - Log the swallowed exception at Debug level → noted in
    `plan.md → Complexity Tracking` as a possible refinement;
    currently the catch is silent.
  - Re-raise → rejected for the surprise reason above.

## Decision 8: File write under `--stream` is post-stream, not incremental

- **Decision**: In `ProcessStreamingRequest`, chunks are accumulated
  into a `StringBuilder` and the file is written once after the stream
  ends.
- **Rationale**:
  - Decouples the streaming-loop hot path from disk I/O.
  - Avoids leaving a partial file on disk if the user cancels.
  - The ANSI-strip regex runs once on the full buffer instead of
    once per chunk.
- **Alternatives considered**:
  - Use `StreamWriter.WriteAsync(chunk)` per chunk with
    `FlushAsync()` → rejected because of the partial-file-on-cancel
    issue and because the user expectation for `-o` is "the final
    answer is in the file" rather than "live tail".

## Decision 9: Streaming non-success HTTP statuses yield zero chunks instead of throwing

- **Decision**: `OpenAIClient.SendStreamingRequestAsync` logs the
  failure and `yield break`s on non-2xx; it does not throw. The
  consumer's `await foreach` simply ends.
- **Rationale**: Throwing from inside `IAsyncEnumerable` iteration
  introduces awkward exception filters and inconsistent semantics with
  the non-streaming path (which returns `AIResponse { Success = false }`).
  An empty stream is at least observable and matches the user's
  expectation that "no output" is itself a signal.
- **Alternatives considered**:
  - Throw `HttpRequestException` → rejected for the iterator-state
    complications and for the asymmetry with `SendRequestAsync`.
  - Yield a sentinel chunk like `"ERROR"` → rejected as a
    protocol-in-band-of-data anti-pattern.

## Decision 10: Format/Stream defaults fall back to active `ModelConfiguration` via sentinel comparison

- **Decision**: `Program.HandleCommandAsync` computes
  `effectiveFormat = (options.Format != "text") ? options.Format : config.Format`
  and
  `effectiveStream = (options.Stream != false) ? options.Stream : config.Stream`.
- **Rationale**: Same as sibling-feature
  `002-model-and-generation-parameters` — `System.CommandLine` does
  not (in this codebase's usage) expose a per-option "was specified"
  flag, so a sentinel comparison against the literal default is used
  to detect "user did not override".
- **Known quirk**: Passing `--format text` explicitly on the command
  line currently behaves the same as omitting the switch (the CLI
  value equals the sentinel). Same for `--stream` when it would
  resolve to `false` — although `--stream` is a flag, its presence
  unambiguously sets `true`, so this quirk only bites for `--format`.
- **Alternatives considered**:
  - Per-option `IsSet` flag tracking → noted as the cleaner long-term
    simplification across sibling features.
