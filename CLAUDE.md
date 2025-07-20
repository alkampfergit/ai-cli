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