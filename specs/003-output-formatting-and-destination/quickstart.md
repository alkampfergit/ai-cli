# Quickstart: Output Formatting and Destination

This guide shows how to control the rendered format, enable token
streaming, and save the AI response to a file using `ai-cli`.

## Prerequisites

- `ai-cli` is built or installed and on your `PATH`.
- You have configured at least one model configuration via
  `ai-cli --config` so that an API key and base URL are known. (This is
  required by `Program.HandleCommandAsync`; without it the CLI exits
  with `"No model configuration found in settings."`.)

## The three switches

```
  -o, --output-file <output-file>  Save response to file
      --format <format>            Output format (text or json) [default: text]
      --stream                     Stream response tokens as they arrive
```

Each switch is independent and can be combined freely.

## Minimal working examples

```bash
# Default rendering — plain text on stdout, no streaming, no file:
ai-cli --prompt "Hello"

# Get the raw provider JSON for scripting:
ai-cli --prompt "Hello" --format json

# Watch the answer appear token by token:
ai-cli --prompt "Count from 1 to 10 slowly" --stream

# Stream JSON-wrapped tokens (one JSON value per chunk):
ai-cli --prompt "Count to ten" --stream --format json

# Save the text response to a file (and also print to stdout):
ai-cli --prompt "Summarise this paragraph" -o ./answer.txt

# Save the raw JSON to a file:
ai-cli --prompt "Summarise this paragraph" --format json -o ./answer.json

# Stream to the terminal AND capture the final text to a file:
ai-cli --prompt "Long explanation, please" --stream -o ./long.txt

# Windows-style short alias for the output file works the same:
ai-cli --prompt "Hello" /o ./answer.txt
```

## Defaults and override order

For both `--format` and `--stream`, the value used is the first
non-default in this list:

1. The value supplied on the command line.
2. The value from your active model configuration (set via `--config`).
3. The built-in default:
   - `text` for `--format`
   - `false` for `--stream`

> Implementation note: `--format` and `--stream` use sentinel-default
> comparison to detect "user did not override", so passing the exact
> default value (`--format text`) currently behaves the same as omitting
> the switch. See `research.md → Decision 10`.

`--output-file` has no configuration-level fallback — it must be passed
on the command line for each invocation that wants a file output.

## Validation

- `--format` must be exactly `text` or `json`. Any other value exits
  with code `1` and the message `"Format must be 'text' or 'json'"`,
  before any HTTP request is made.
- `--stream` is a boolean flag — no value validation.
- `-o/--output-file` accepts any string; failures (missing parent
  directory, no write permission) surface at write time as I/O errors
  with exit code `3`.

## How streaming JSON output looks on the wire

For `--stream --format json`, stdout receives a sequence of small JSON
values, one per token chunk, with no separator:

```
{"content":"Hello"}{"content":"! How"}{"content":" can I help?"}
```

This is **not** a single JSON document — consume it one value at a
time. A simple `while (reader.Read())` loop on `Utf8JsonReader`, or a
streaming JSON parser, will iterate the values cleanly.

## File-write guarantees

When `-o <path>` is supplied:

1. The same content that was rendered to stdout (the text for
   `--format text`, the raw provider JSON body for `--format json`) is
   written to the file.
2. Before writing, ANSI escape sequences matching
   `ESC [ <digits/semicolons> [m|G|K]` (SGR coloring, cursor horizontal,
   erase-in-line) are stripped from the content.
3. On Linux/macOS (any non-Windows OS), the file mode is set to `0600`
   (`-rw-------`). Verify with `stat -c %a ./answer.txt`.
4. On Windows, the file inherits the parent directory's NTFS ACL; no
   explicit hardening is performed.
5. The file is overwritten if it already exists; no backup is made.
6. Under `--stream`, the file is written **after** the stream ends, not
   incrementally. A cancelled stream produces no file.

## How to verify it works

1. **Verify JSON mode**:
   ```bash
   ai-cli -p "Say hello" --format json | python -m json.tool
   ```
   Expect a valid JSON dump of the full provider response.

2. **Verify streaming**:
   ```bash
   ai-cli -p "Write a haiku about Mondays" --stream
   ```
   The output should appear token-by-token, not all at once.

3. **Verify ANSI stripping on Linux/macOS**:
   ```bash
   ai-cli -p "Pretend to print red text using ANSI escapes" \
          -o ./out.txt
   grep -P '\x1B\[' ./out.txt
   # Expected: no SGR/cursor/erase-in-line matches (m/G/K final bytes).
   ```

4. **Verify Unix file mode**:
   ```bash
   ai-cli -p "Hi" -o ./answer.txt
   stat -c %a ./answer.txt
   # Expected: 600
   ```

5. **Verify invalid format is rejected at parse time**:
   ```bash
   ai-cli -p "Hi" --format xml; echo "exit=$?"
   # Expected:
   #   Error parsing command line arguments:
   #     Format must be 'text' or 'json'
   #   exit=1
   ```

6. **Verify aliases**:
   ```bash
   ai-cli -p "Hi" -o ./a.txt
   ai-cli -p "Hi" --output-file ./b.txt
   ai-cli -p "Hi" /o ./c.txt
   # All three produce equivalent files.
   ```

## Where the values live in the code

| Surface | Field | File |
|---------|-------|------|
| CLI options | `Format`, `Stream`, `OutputFile` | `src/ai-cli/Models/CliOptions.cs` |
| Request DTO | `Stream` (only) | `src/ai-cli/Models/AIRequest.cs` |
| HTTP streaming pipeline | SSE → chunk decoder | `src/ai-cli/Infrastructure/OpenAIClient.cs` |
| Renderer fork | text/json + stream/non-stream | `src/ai-cli/Program.cs` (`ProcessNonStreamingRequest`, `ProcessStreamingRequest`) |
| Secure file writer | ANSI strip + `0600` mode | `src/ai-cli/Program.cs` (`WriteToFileAsync`) |
| Persisted defaults | `Format`, `Stream` | `src/ai-cli/Models/UserSettings.cs` (`ModelConfiguration`) |
