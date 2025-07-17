using AiCli.CLI;
using FluentAssertions;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace AiCli.Tests.CLI;

public class CommandLineBuilderTests
{
    [Fact]
    public void CreateRootCommand_ShouldCreateCommandWithAllOptions()
    {
        // Act
        var rootCommand = CommandLineBuilder.CreateRootCommand();

        // Assert
        rootCommand.Should().NotBeNull();
        rootCommand.Description.Should().Be("AI CLI - Send prompts to OpenAI-compatible APIs");
        rootCommand.Options.Should().HaveCount(10);
        
        // Check that all expected options are present
        rootCommand.Options.Select(o => o.Name).Should().Contain(new[]
        {
            "prompt", "file", "model", "temperature", "max-tokens",
            "output-file", "format", "stream", "api-key", "base-url"
        });
    }

    [Fact]
    public void ParseOptions_WithInlinePrompt_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Hello, world!", "--model", "gpt-4" };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Hello, world!");
        options.Model.Should().Be("gpt-4");
        options.FilePath.Should().BeNull();
        options.UseStdin.Should().BeFalse();
    }

    [Fact]
    public void ParseOptions_WithFilePrompt_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--file", "prompt.txt", "--temperature", "0.5" };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.FilePath.Should().Be("prompt.txt");
        options.Temperature.Should().Be(0.5f);
        options.Prompt.Should().BeNull();
        options.UseStdin.Should().BeFalse();
    }

    [Fact]
    public void ParseOptions_WithAllOptions_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[]
        {
            "--prompt", "Test prompt",
            "--model", "gpt-4",
            "--temperature", "0.7",
            "--max-tokens", "100",
            "--output-file", "output.txt",
            "--format", "json",
            "--stream",
            "--api-key", "test-key",
            "--base-url", "https://api.example.com"
        };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Test prompt");
        options.Model.Should().Be("gpt-4");
        options.Temperature.Should().Be(0.7f);
        options.MaxTokens.Should().Be(100);
        options.OutputFile.Should().Be("output.txt");
        options.Format.Should().Be("json");
        options.Stream.Should().BeTrue();
    }

    [Fact]
    public void CreateRootCommand_WithInvalidTemperature_ShouldFail()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Test", "--temperature", "3.0" };

        // Act
        var parseResult = rootCommand.Parse(args);

        // Assert
        parseResult.Errors.Should().HaveCount(1);
        parseResult.Errors[0].Message.Should().Contain("Temperature must be between 0.0 and 2.0");
    }

[Fact]
    public void CreateRootCommand_WithInvalidFormat_ShouldFail()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Test", "--format", "xml" };

        // Act
        var parseResult = rootCommand.Parse(args);

        // Assert
        parseResult.Errors.Should().HaveCount(1);
        parseResult.Errors[0].Message.Should().Contain("Format must be 'text' or 'json'");
    }
}