# Contract: Output File Security

**Source**: `src/ai-cli/Program.cs` → `WriteToFileAsync`.

This contract describes the security-hardened file persistence path
invoked whenever `-o/--output-file <path>` is supplied.

## Signature

```csharp
private static async Task WriteToFileAsync(string filePath, string content)
```

## Behavior

1. **ANSI sequence stripping** — before writing, the content is
   sanitized by:

   ```csharp
   var cleanContent = System.Text.RegularExpressions.Regex.Replace(
       content,
       @"\x1B\[[0-9;]*[mGK]",
       "");
   ```

   This removes ANSI control sequences of the form
   `ESC [ <digits/semicolons> <m|G|K>`:
   - `m` — Select Graphic Rendition (colors, bold, underline, etc.)
   - `G` — Cursor Horizontal Absolute
   - `K` — Erase in Line

   Other ANSI sequences (e.g. `[H`, `[J`, `[?…l`, OSC, DCS) are NOT
   stripped. See `plan.md → Complexity Tracking` for rationale.

2. **File write** — the sanitized content is written via
   `File.WriteAllTextAsync(filePath, cleanContent)`. If the path is
   invalid, unwriteable, or the parent directory does not exist, the
   exception propagates out to `Program.HandleCommandAsync`, which
   maps `FileNotFoundException` and `UnauthorizedAccessException` to
   exit code `ExitCodes.FileError` (`3`). Any existing file is
   overwritten.

3. **Restrictive Unix permissions** — when `!OperatingSystem.IsWindows()`,
   the method then calls:

   ```csharp
   File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
   ```

   This sets the file mode to `0600` (owner-only read+write). The call
   is wrapped in `try { … } catch { /* swallow */ }` so that
   filesystems which do not honor `chmod` (FUSE mounts, mounted
   Windows shares on Linux, etc.) do not break an otherwise successful
   write.

4. **Windows** — the Unix permission step is skipped entirely. The file
   inherits the parent directory's NTFS ACL.

## Inputs to the contract

| Caller | `filePath` | `content` |
|--------|-----------|-----------|
| `Program.ProcessNonStreamingRequest` (text mode) | `options.OutputFile` | `AIResponse.Content` |
| `Program.ProcessNonStreamingRequest` (json mode) | `options.OutputFile` | `AIResponse.RawResponse` |
| `Program.ProcessStreamingRequest` (any format) | `options.OutputFile` | accumulated `StringBuilder.ToString()` of all yielded text chunks |

In all three callers, the content passed is the **unwrapped text** —
JSON mode passes either the raw provider body or the accumulated text
chunks. Stream-mode does NOT pass the JSON-wrapped (`{"content": …}`)
per-chunk form to the file; the file receives the concatenated original
text.

## Security guarantees

1. **No ANSI escape injection** into saved files. A model that emits
   `"\x1B[31m PWNED \x1B[0m"` cannot produce a file that re-colorizes
   the user's terminal when later `cat`ed (for SGR sequences).
2. **No world-readable secrets** on shared Unix hosts. A response that
   echoes API output the user wants to keep private is not exposed to
   other local users by default mode (umask is overridden to `0600`).
3. **No log leak** — the content is never written to the logger; only
   the act of writing and any caught exception is logged.

## Non-guarantees

- The ANSI sanitizer is intentionally narrow (`[m|G|K]` only). It does
  NOT defend against:
  - Cursor positioning (`[H`, `[f`).
  - Screen-clearing (`[J`).
  - Mode-set/reset (`[?…h`, `[?…l`).
  - OSC (`\x1B]…`), DCS (`\x1BP…`), or APC (`\x1B_…`) sequences.
- The Unix permission step is best-effort. A swallowed exception means
  the file may exist with default umask permissions; the CLI does NOT
  notify the user.
- The file is not signed, checksummed, or otherwise integrity-verified.
- The file is overwritten without backup if the path already exists.

## Failure modes

| Condition | CLI behavior | Exit code |
|-----------|--------------|-----------|
| Path does not exist (parent dir missing) | `FileNotFoundException` propagates → caught in `HandleCommandAsync` | `3` (`FileError`) |
| Unwriteable (permissions) | `UnauthorizedAccessException` propagates → caught | `3` (`FileError`) |
| `SetUnixFileMode` fails (Unix only) | exception swallowed inside `WriteToFileAsync` | `0` (write reported as success) |
| Disk full during write | underlying `IOException` propagates | `4` (`UnknownError`) (no specific catch) |

## Test coverage

- No dedicated unit test exists for `WriteToFileAsync`'s ANSI-strip or
  `0600` mode behavior; the method is private to `Program`. See
  `plan.md → Complexity Tracking` for the recommended Linux-specific
  test that would exercise both.
- The CLI-side acceptance of `-o`, `--output-file`, and `/o` aliases is
  covered in `CommandLineBuilderTests.ParseOptions_AllOutputSyntaxVariations_ShouldParseCorrectly`.
