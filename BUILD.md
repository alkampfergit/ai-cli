# Building AI CLI

## Single Executable Deployment

The AI CLI can be built as a single, self-contained executable that doesn't require .NET runtime to be installed on the target machine.

### Build Commands

#### Windows (x64)
```bash
dotnet publish -c Release -r win-x64 --self-contained -o publish
```

#### Linux (x64)
```bash
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

#### macOS (x64)
```bash
dotnet publish -c Release -r osx-x64 --self-contained -o publish
```

#### macOS (ARM64)
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -o publish
```

### Features

- **Single File**: Everything is bundled into one executable file
- **Self-Contained**: No .NET runtime required on target machine
- **Compressed**: Compression is enabled to reduce file size
- **Cross-Platform**: Build for Windows, Linux, and macOS

### Output

The build process creates:
- `ai-cli.exe` (Windows) or `ai-cli` (Linux/macOS) - The main executable (~37MB)
- `ai-cli.pdb` - Debug symbols (optional, can be deleted)
- `ai-cli.xml` - XML documentation (optional, can be deleted)

### Usage

After building, you can distribute just the executable file:

```bash
# Direct execution
./ai-cli --help

# Interactive mode
./ai-cli

# Configuration mode
./ai-cli --config

# With prompt
./ai-cli --prompt "Hello world"

# Piped input
echo "Hello world" | ./ai-cli
```

### File Size

The single executable is approximately 37MB, which includes:
- The entire .NET runtime
- All dependencies
- Application code
- Compressed with built-in compression

This size is typical for self-contained .NET applications and ensures maximum compatibility.