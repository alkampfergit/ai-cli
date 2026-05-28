# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

### Building
```bash
# Build the solution
dotnet build src/ai-cli.sln

# Build for release
dotnet build src/ai-cli.sln -c Release

# Build single-file executable for current platform
dotnet publish src/ai-cli/ai-cli.csproj -c Release --self-contained -o publish

# Build for multiple platforms using PowerShell script
./build.ps1
```

### Testing
```bash
# Run all tests
dotnet test src/ai-cli.sln

# Run tests with verbose output
dotnet test src/ai-cli.sln --verbosity normal

# Run tests with code coverage
dotnet test src/ai-cli.sln --collect:"XPlat Code Coverage"

# Run a specific test
dotnet test src/ai-cli.Tests/ai-cli.Tests.csproj --filter "TestMethodName"
```

### Code Quality
```bash
# Format code
dotnet format src/ai-cli.sln
```

### Development
```bash
# Run the application in development
dotnet run --project src/ai-cli/ai-cli.csproj -- --help

# Run with configuration mode
dotnet run --project src/ai-cli/ai-cli.csproj -- --config

# Run with a test prompt
dotnet run --project src/ai-cli/ai-cli.csproj -- --prompt "Hello world"
```

## Architecture

This is a .NET 9 console application built with clean architecture principles:

### Core Layers
- **CLI Layer** (`src/ai-cli/CLI/`): Command-line interface using System.CommandLine
- **Application Layer** (`src/ai-cli/Application/`): Business logic and service interfaces
- **Infrastructure Layer** (`src/ai-cli/Infrastructure/`): External integrations (HTTP, file I/O, logging)
- **Models Layer** (`src/ai-cli/Models/`): Data models and POCOs

### Key Components

#### Entry Point
- `Program.cs`: Main entry point with dependency injection setup and error handling

#### Command Line Interface
- `CommandLineBuilder.cs`: Builds command-line options using System.CommandLine
- `CliOptions.cs`: CLI options model with validation
- `ExitCodes.cs`: Standardized exit codes for different error conditions

#### Core Services
- `PromptService.cs`: Handles prompt processing from different sources (inline, file, stdin)
- `OpenAIClient.cs`: HTTP client for OpenAI-compatible API communication
- `ConfigurationService.cs`: Interactive configuration management using Spectre.Console

#### Settings Management
- `UserSettings.cs`: User configuration model with model configurations
- `FileUserSettingsService.cs`: File-based settings persistence
- `SettingsPathProvider.cs`: Cross-platform settings file path resolution

#### Security
- `IEncryptionService`: Interface for encryption services
- `DpapiEncryptionService.cs`: Windows DPAPI encryption (Windows only)
- `AesEncryptionService.cs`: AES encryption for cross-platform use
- `EncryptedSettingAttribute.cs`: Marks properties for automatic encryption

### Configuration System

The application uses a sophisticated configuration system:

1. **Model Configurations**: Store API keys, base URLs, and model settings
2. **Platform-Specific Encryption**: Uses DPAPI on Windows, AES on other platforms
3. **Interactive Configuration**: Spectre.Console-based configuration menus
4. **LiteLLM Integration**: Can automatically discover and configure models from LiteLLM proxy

### Input Methods

The application supports three mutually exclusive input methods:
- **Inline prompts**: `--prompt "text"`
- **File input**: `--file path/to/file.txt`
- **Stdin**: Piped input or interactive mode

### Output Formats

- **Text format**: Plain text output (default)
- **JSON format**: Structured JSON output
- **Streaming**: Real-time token streaming for both formats
- **File output**: Save responses to files with security measures

### Error Handling

Comprehensive error handling with specific exit codes:
- `0`: Success
- `1`: Invalid arguments
- `2`: API communication error
- `3`: File or IO error
- `4`: Unknown/unhandled error

### Security Features

- API keys are encrypted at rest using platform-specific encryption
- ANSI sequence stripping from file outputs prevents injection attacks
- Restrictive file permissions (600) on Unix systems
- Sensitive data is not logged at Information level
- API keys are never included in logs unless debug level

### Testing

The project has comprehensive test coverage using:
- **xUnit**: Testing framework
- **Moq**: Mocking framework
- **FluentAssertions**: Fluent assertion library

Test structure mirrors the main project structure with unit tests for all major components.

### Dependencies

Key external dependencies:
- **System.CommandLine**: CLI parsing and validation
- **Serilog**: Structured logging with file and console sinks
- **Spectre.Console**: Rich console UI for configuration
- **Microsoft.Extensions.DependencyInjection**: Dependency injection
- **Microsoft.Extensions.Http**: HTTP client factory
- **System.Text.Json**: JSON serialization
- **System.Security.Cryptography.ProtectedData**: DPAPI encryption

### Build Configuration

- **Target Framework**: .NET 9.0
- **Single File Deployment**: Configured for self-contained executables
- **Nullable Reference Types**: Enabled
- **Treat Warnings as Errors**: Enabled
- **Documentation Generation**: Enabled
- **Cross-Platform**: Supports Windows, Linux, and macOS

### Logging

Structured logging with Serilog:
- **Console**: Information level and above
- **File**: Debug level and above, 7-day retention
- **Location**: `~/.ai-cli/ai-cli.log`
- **Security**: API keys and sensitive data are not logged

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->

<!-- rtk-instructions v2 -->
# RTK (Rust Token Killer) - Token-Optimized Commands

## Golden Rule

**Always prefix commands with `rtk`**. If RTK has a dedicated filter, it uses it. If not, it passes through unchanged. This means RTK is always safe to use.

**Important**: Even in command chains with `&&`, use `rtk`:
```bash
# ❌ Wrong
git add . && git commit -m "msg" && git push

# ✅ Correct
rtk git add . && rtk git commit -m "msg" && rtk git push
```

## RTK Commands by Workflow

### Build & Compile (80-90% savings)
```bash
rtk cargo build         # Cargo build output
rtk cargo check         # Cargo check output
rtk cargo clippy        # Clippy warnings grouped by file (80%)
rtk tsc                 # TypeScript errors grouped by file/code (83%)
rtk lint                # ESLint/Biome violations grouped (84%)
rtk prettier --check    # Files needing format only (70%)
rtk next build          # Next.js build with route metrics (87%)
```

### Test (60-99% savings)
```bash
rtk cargo test          # Cargo test failures only (90%)
rtk go test             # Go test failures only (90%)
rtk jest                # Jest failures only (99.5%)
rtk vitest              # Vitest failures only (99.5%)
rtk playwright test     # Playwright failures only (94%)
rtk pytest              # Python test failures only (90%)
rtk rake test           # Ruby test failures only (90%)
rtk rspec               # RSpec test failures only (60%)
rtk test <cmd>          # Generic test wrapper - failures only
```

### Git (59-80% savings)
```bash
rtk git status          # Compact status
rtk git log             # Compact log (works with all git flags)
rtk git diff            # Compact diff (80%)
rtk git show            # Compact show (80%)
rtk git add             # Ultra-compact confirmations (59%)
rtk git commit          # Ultra-compact confirmations (59%)
rtk git push            # Ultra-compact confirmations
rtk git pull            # Ultra-compact confirmations
rtk git branch          # Compact branch list
rtk git fetch           # Compact fetch
rtk git stash           # Compact stash
rtk git worktree        # Compact worktree
```

Note: Git passthrough works for ALL subcommands, even those not explicitly listed.

### GitHub (26-87% savings)
```bash
rtk gh pr view <num>    # Compact PR view (87%)
rtk gh pr checks        # Compact PR checks (79%)
rtk gh run list         # Compact workflow runs (82%)
rtk gh issue list       # Compact issue list (80%)
rtk gh api              # Compact API responses (26%)
```

### JavaScript/TypeScript Tooling (70-90% savings)
```bash
rtk pnpm list           # Compact dependency tree (70%)
rtk pnpm outdated       # Compact outdated packages (80%)
rtk pnpm install        # Compact install output (90%)
rtk npm run <script>    # Compact npm script output
rtk npx <cmd>           # Compact npx command output
rtk prisma              # Prisma without ASCII art (88%)
```

### Files & Search (60-75% savings)
```bash
rtk ls <path>           # Tree format, compact (65%)
rtk read <file>         # Code reading with filtering (60%)
rtk grep <pattern>      # Search grouped by file (75%). Format flags (-c, -l, -L, -o, -Z) run raw.
rtk find <pattern>      # Find grouped by directory (70%)
```

### Analysis & Debug (70-90% savings)
```bash
rtk err <cmd>           # Filter errors only from any command
rtk log <file>          # Deduplicated logs with counts
rtk json <file>         # JSON structure without values
rtk deps                # Dependency overview
rtk env                 # Environment variables compact
rtk summary <cmd>       # Smart summary of command output
rtk diff                # Ultra-compact diffs
```

### Infrastructure (85% savings)
```bash
rtk docker ps           # Compact container list
rtk docker images       # Compact image list
rtk docker logs <c>     # Deduplicated logs
rtk kubectl get         # Compact resource list
rtk kubectl logs        # Deduplicated pod logs
```

### Network (65-70% savings)
```bash
rtk curl <url>          # Compact HTTP responses (70%)
rtk wget <url>          # Compact download output (65%)
```

### Meta Commands
```bash
rtk gain                # View token savings statistics
rtk gain --history      # View command history with savings
rtk discover            # Analyze Claude Code sessions for missed RTK usage
rtk proxy <cmd>         # Run command without filtering (for debugging)
rtk init                # Add RTK instructions to CLAUDE.md
rtk init --global       # Add RTK to ~/.claude/CLAUDE.md
```

## Token Savings Overview

| Category | Commands | Typical Savings |
|----------|----------|-----------------|
| Tests | vitest, playwright, cargo test | 90-99% |
| Build | next, tsc, lint, prettier | 70-87% |
| Git | status, log, diff, add, commit | 59-80% |
| GitHub | gh pr, gh run, gh issue | 26-87% |
| Package Managers | pnpm, npm, npx | 70-90% |
| Files | ls, read, grep, find | 60-75% |
| Infrastructure | docker, kubectl | 85% |
| Network | curl, wget | 65-70% |

Overall average: **60-90% token reduction** on common development operations.
<!-- /rtk-instructions -->