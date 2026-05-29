# Quickstart: Prompt Input Methods

How to send a prompt to `ai-cli` using each of the three supported input
methods. Examples below assume a configured model exists (run
`ai-cli --config` first if not).

## 1. Inline prompt

Use this for short, one-line questions you can type directly on the
command line.

```bash
ai-cli --prompt "Explain monads in one sentence."
# or, equivalently:
ai-cli -p "Explain monads in one sentence."
ai-cli /p "Explain monads in one sentence."   # Windows-style alias
```

The string passed to `--prompt` is sent verbatim to the AI service.

## 2. File-based prompt

Use this for longer, multi-line, or pre-prepared prompts.

```bash
cat > /tmp/prompt.txt <<'EOF'
You are a senior C# reviewer. Review the following code for thread
safety, focusing on the use of Console.In.

<paste code here>
EOF

ai-cli --file /tmp/prompt.txt
# or:
ai-cli -f /tmp/prompt.txt
ai-cli /f /tmp/prompt.txt
```

**Important**: After processing succeeds, `ai-cli` deletes
`/tmp/prompt.txt`. If you need to keep your prompt, copy it elsewhere
first or pass it inline.

If the file does not exist, the CLI exits with the file-error code and
prints the missing path; no AI request is made.

## 3. Piped stdin

Use this for ad-hoc pipelines that compose with other tools.

```bash
# Feed the output of another command as the prompt:
git diff main | ai-cli

# Or read from a file via shell redirection:
ai-cli < some-prompt.txt

# Or chain with cat:
cat README.md | ai-cli
```

`ai-cli` reads stdin to completion before issuing the request.

## 4. Interactive stdin (no source given)

If you run `ai-cli` in a terminal without `--prompt`, without `--file`,
and without piping anything in, it waits for you to type your prompt.

```bash
$ ai-cli
Hello, what's the capital of France?
^D   # Ctrl+D on Unix; Ctrl+Z then Enter on Windows
```

The CLI collects each line and submits the whole buffer once you signal
end-of-input.

## Verifying it works

You can verify each path against the unit tests in
`src/ai-cli.Tests/Application/PromptServiceTests.cs`:

```bash
dotnet test src/ai-cli.sln --filter "FullyQualifiedName~PromptServiceTests"
```

And the option-parsing/validation behaviour in
`src/ai-cli.Tests/CLI/CommandLineBuilderTests.cs`:

```bash
dotnet test src/ai-cli.sln --filter "FullyQualifiedName~CommandLineBuilderTests"
```

## Common errors

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Only one prompt source can be specified: --prompt, --file, or stdin` | Both `--prompt` and `--file` were set on the same invocation. | Drop one of them. |
| `File error: Prompt file not found: <path>` | `--file` pointed at a missing path. | Check the path; quote it if it contains spaces. |
| Hangs after running with no flags | You are in interactive mode and the CLI is waiting for typed input. | Type your prompt, then signal EOF (Ctrl+D / Ctrl+Z+Enter). |
| `Unexpected error: No prompt source specified` | Stdin was redirected from nothing (e.g. `/dev/null`) and no flag was given. | Provide `--prompt`, `--file`, or actual stdin content. |
