# AI CLI

A cross-platform .NET 9 console application for sending prompts to OpenAI-compatible generative AI APIs.

## Features

- 🚀 **Cross-platform**: Runs on Windows, Linux, and macOS
- 🔧 **Flexible input**: Support for inline prompts, file input, and stdin
- 🎛️ **Configurable**: Temperature, max tokens, top-p, and other OpenAI parameters
- 📤 **Multiple outputs**: Console output, file output, JSON format
- 🌊 **Streaming support**: Real-time token streaming
- 🔐 **Secure**: API key from environment variables, no logging of sensitive data
- 📝 **Well-tested**: 80%+ test coverage with xUnit and Moq

## Installation

### Prerequisites

- .NET 9.0 SDK or later
- An OpenAI API key (or compatible API)

### Build from Source

```bash
git clone <repository-url>
cd ai-cli
dotnet build src/ai-cli.sln
```

### Using Pre-built Binaries

Download the latest release for your platform from the [Releases](releases) page.

## Usage

### Basic Usage

```bash
# Set your API key
export AI_API_KEY="your-api-key-here"

# Send a prompt inline
ai-cli --prompt "Hello, world!"

# Use a different model
ai-cli --prompt "Explain quantum computing" --model gpt-4

# Read prompt from file
ai-cli --file prompt.txt

# Read from stdin
echo "What is the capital of France?" | ai-cli
```

### Advanced Usage

```bash
# Stream responses in real-time
ai-cli --prompt "Tell me a story" --stream

# Save response to file
ai-cli --prompt "Write a poem" --output-file poem.txt

# JSON output format
ai-cli --prompt "Hello" --format json

# Custom parameters
ai-cli --prompt "Be creative" --temperature 0.8 --max-tokens 200 --top-p 0.9

# Use custom API endpoint
ai-cli --prompt "Hello" --base-url https://api.example.com/v1
```

## Configuration

### Environment Variables

- `AI_API_KEY`: Your OpenAI API key (required)

### Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--prompt` | `-p` | Prompt text to send to the AI service | |
| `--file` | `-f` | Path to file containing the prompt | |
| `--model` | `-m` | AI model to use | `gpt-3.5-turbo` |
| `--temperature` | | Temperature for generation (0.0-2.0) | `1.0` |
| `--max-tokens` | | Maximum number of tokens to generate | |
| `--top-p` | | Top-p sampling parameter (0.0-1.0) | |
| `--output-file` | `-o` | Save response to file | |
| `--format` | | Output format (`text` or `json`) | `text` |
| `--stream` | | Stream response tokens as they arrive | `false` |
| `--api-key` | | API key (overrides environment variable) | |
| `--base-url` | | Base URL for the API endpoint | `https://api.openai.com/v1` |
| `--help` | `-h` | Show help and usage information | |
| `--version` | `-v` | Show version information | |

### Input Methods

You can provide prompts in three ways (mutually exclusive):

1. **Inline**: `--prompt "Your prompt here"`
2. **File**: `--file path/to/prompt.txt`
3. **Stdin**: `echo "Your prompt" | ai-cli`

## Examples

### Basic Examples

```bash
# Simple question
ai-cli --prompt "What is 2+2?"

# Code generation
ai-cli --prompt "Write a Python function to calculate factorial"

# Creative writing
ai-cli --prompt "Write a haiku about programming" --temperature 0.8
```

### File Input

```bash
# Create a prompt file
echo "Explain the concept of machine learning" > prompt.txt

# Use the file
ai-cli --file prompt.txt --model gpt-4
```

### Streaming Example

```bash
# Stream a longer response
ai-cli --prompt "Tell me a detailed story about a robot" --stream --max-tokens 500
```

### JSON Output

```bash
# Get structured output
ai-cli --prompt "Hello" --format json | jq .
```

## Exit Codes

- `0`: Success
- `1`: Invalid arguments
- `2`: API communication error
- `3`: File or IO error
- `4`: Unknown/unhandled error

## Development

### Building

```bash
# Build the solution
dotnet build src/ai-cli.sln

# Run tests
dotnet test src/ai-cli.sln

# Build for specific platform
dotnet publish src/ai-cli/ai-cli.csproj -c Release -r win-x64 --self-contained
```

### Testing

```bash
# Run all tests
dotnet test src/ai-cli.sln --verbosity normal

# Run tests with coverage
dotnet test src/ai-cli.sln --collect:"XPlat Code Coverage"
```

### Code Formatting

```bash
# Format code
dotnet format src/ai-cli.sln
```

## Architecture

The application follows a clean architecture pattern:

- **CLI Layer**: Command-line argument parsing with System.CommandLine
- **Application Layer**: Business logic and service interfaces
- **Infrastructure Layer**: HTTP client, OpenAI API integration, logging

### Key Components

- `Program.cs`: Entry point with dependency injection setup
- `CommandLineBuilder`: CLI argument parsing and validation
- `PromptService`: Core business logic for prompt processing
- `OpenAIClient`: HTTP client for OpenAI API communication
- `LoggingConfiguration`: Serilog configuration for file and console logging

## Security

- API keys are never logged at Information level
- ANSI sequences are stripped from file outputs to prevent injection
- Created files have restrictive permissions (600) on Unix systems
- No sensitive data is written to logs unless debug level is enabled

## Logging

Logs are written to:
- **Console**: Information level and above
- **File**: `~/.ai-cli/ai-cli.log` (Debug level and above, 7-day retention)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## Changelog

### v1.0.0
- Initial release
- Support for OpenAI-compatible APIs
- Streaming and non-streaming responses
- Multiple input methods (inline, file, stdin)
- JSON and text output formats
- Comprehensive error handling
- Full test coverage