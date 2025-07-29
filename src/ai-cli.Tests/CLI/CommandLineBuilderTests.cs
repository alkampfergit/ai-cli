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
        rootCommand.Options.Should().HaveCount(9);

        // Check that all expected options are present
        rootCommand.Options.Select(o => o.Name).Should().Contain(new[]
        {
            "prompt", "file", "model", "temperature", "max-tokens",
            "output-file", "format", "stream", "config"
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

    [Fact]
    public void ParseOptions_WithForwardSlashPrompt_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "/p", "Hello with forward slash!", "--model", "gpt-4" };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Hello with forward slash!");
        options.Model.Should().Be("gpt-4");
        options.FilePath.Should().BeNull();
        options.UseStdin.Should().BeFalse();
    }

    [Fact]
    public void ParseOptions_WithForwardSlashFile_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "/f", "prompt.txt", "--temperature", "0.5" };
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
    public void ParseOptions_WithForwardSlashModel_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Test", "/m", "gpt-4" };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Test");
        options.Model.Should().Be("gpt-4");
    }

    [Fact]
    public void ParseOptions_WithForwardSlashOutput_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Test", "/o", "output.txt" };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Test");
        options.OutputFile.Should().Be("output.txt");
    }

    [Fact]
    public void ParseOptions_WithAllForwardSlashOptions_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[]
        {
            "/p", "Test prompt with forward slash",
            "/m", "gpt-4",
            "/o", "output.txt"
        };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Test prompt with forward slash");
        options.Model.Should().Be("gpt-4");
        options.OutputFile.Should().Be("output.txt");
        options.UseStdin.Should().BeFalse();
    }

    [Fact]
    public void ParseOptions_WithMixedSyntax_ShouldParseCorrectly()
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[]
        {
            "/p", "Test prompt",
            "-m", "gpt-4",
            "--output-file", "output.txt"
        };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be("Test prompt");
        options.Model.Should().Be("gpt-4");
        options.OutputFile.Should().Be("output.txt");
        options.UseStdin.Should().BeFalse();
    }

    [Theory]
    [InlineData("-p", "Test with dash")]
    [InlineData("/p", "Test with forward slash")]
    [InlineData("--prompt", "Test with long form")]
    public void ParseOptions_AllPromptSyntaxVariations_ShouldParseCorrectly(string optionSyntax, string expectedValue)
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { optionSyntax, expectedValue };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Prompt.Should().Be(expectedValue);
        options.UseStdin.Should().BeFalse();
    }

    [Theory]
    [InlineData("-f", "file.txt")]
    [InlineData("/f", "file.txt")]
    [InlineData("--file", "file.txt")]
    public void ParseOptions_AllFileSyntaxVariations_ShouldParseCorrectly(string optionSyntax, string expectedValue)
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { optionSyntax, expectedValue };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.FilePath.Should().Be(expectedValue);
        options.UseStdin.Should().BeFalse();
    }

    [Theory]
    [InlineData("-m", "gpt-4")]
    [InlineData("/m", "gpt-4")]
    [InlineData("--model", "gpt-4")]
    public void ParseOptions_AllModelSyntaxVariations_ShouldParseCorrectly(string optionSyntax, string expectedValue)
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Test", optionSyntax, expectedValue };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.Model.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("-o", "output.txt")]
    [InlineData("/o", "output.txt")]
    [InlineData("--output-file", "output.txt")]
    public void ParseOptions_AllOutputSyntaxVariations_ShouldParseCorrectly(string optionSyntax, string expectedValue)
    {
        // Arrange
        var rootCommand = CommandLineBuilder.CreateRootCommand();
        var args = new[] { "--prompt", "Test", optionSyntax, expectedValue };
        var parseResult = rootCommand.Parse(args);

        // Act
        var options = CommandLineBuilder.ParseOptions(parseResult);

        // Assert
        options.OutputFile.Should().Be(expectedValue);
    }
}