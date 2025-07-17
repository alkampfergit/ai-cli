using AiCli.Application;
using AiCli.CLI;
using AiCli.Infrastructure;
using AiCli.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.CommandLine;
using System.Text.Json;

namespace AiCli;

/// <summary>
/// Main program entry point
/// </summary>
internal class Program
{
    /// <summary>
    /// Main entry point for the application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    static async Task<int> Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var serilogLogger = LoggingConfiguration.ConfigureLogging();
        Log.Logger = serilogLogger;

        try
        {
            var rootCommand = CommandLineBuilder.CreateRootCommand();
            rootCommand.SetHandler(async (context) =>
            {
                Environment.ExitCode = await HandleCommandAsync(context.ParseResult, cancellationTokenSource.Token);
            });

            await rootCommand.InvokeAsync(args);
            return Environment.ExitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return ExitCodes.UnknownError;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Handles the parsed command line arguments
    /// </summary>
    /// <param name="parseResult">Parse result from System.CommandLine</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code</returns>
    private static async Task<int> HandleCommandAsync(System.CommandLine.Parsing.ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var options = CommandLineBuilder.ParseOptions(parseResult);
            
            // Validate API key
            var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("AI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.Error.WriteLine("Error: API key is required. Set AI_API_KEY environment variable or use --api-key option.");
                return ExitCodes.InvalidArguments;
            }

            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, apiKey, options.BaseUrl);
            
            using var serviceProvider = services.BuildServiceProvider();
            var promptService = serviceProvider.GetRequiredService<IPromptService>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting AI CLI with model {Model}", options.Model);

            // Process the prompt
            if (options.Stream)
            {
                await ProcessStreamingRequest(promptService, options, cancellationToken);
            }
            else
            {
                await ProcessNonStreamingRequest(promptService, options, cancellationToken);
            }

            return ExitCodes.Success;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"File error: {ex.Message}");
            return ExitCodes.FileError;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Access error: {ex.Message}");
            return ExitCodes.FileError;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"API error: {ex.Message}");
            return ExitCodes.ApiError;
        }
        catch (TaskCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return ExitCodes.ApiError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return ExitCodes.UnknownError;
        }
    }

    /// <summary>
    /// Configures dependency injection services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="apiKey">API key</param>
    /// <param name="baseUrl">Base URL</param>
    private static void ConfigureServices(ServiceCollection services, string apiKey, string? baseUrl)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        services.AddHttpClient<IAIClient, OpenAIClient>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
            });

        services.AddSingleton<IAIClient>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(typeof(IAIClient).Name);
            var logger = provider.GetRequiredService<ILogger<OpenAIClient>>();
            return new OpenAIClient(httpClient, logger, apiKey, baseUrl);
        });

        services.AddScoped<IPromptService, PromptService>();
    }

    /// <summary>
    /// Processes a non-streaming request
    /// </summary>
    /// <param name="promptService">Prompt service</param>
    /// <param name="options">CLI options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private static async Task ProcessNonStreamingRequest(IPromptService promptService, CliOptions options, CancellationToken cancellationToken)
    {
        var response = await promptService.ProcessPromptAsync(options, cancellationToken);
        
        if (!response.Success)
        {
            throw new HttpRequestException(response.ErrorMessage ?? "Unknown API error");
        }

        var output = options.Format == "json" ? response.RawResponse : response.Content;
        
        // Write to console
        Console.Write(output);
        
        // Write to file if specified
        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            await WriteToFileAsync(options.OutputFile, output);
        }
    }

    /// <summary>
    /// Processes a streaming request
    /// </summary>
    /// <param name="promptService">Prompt service</param>
    /// <param name="options">CLI options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private static async Task ProcessStreamingRequest(IPromptService promptService, CliOptions options, CancellationToken cancellationToken)
    {
        var content = new System.Text.StringBuilder();
        
        await foreach (var chunk in promptService.ProcessStreamingPromptAsync(options, cancellationToken))
        {
            if (options.Format == "json")
            {
                var jsonChunk = JsonSerializer.Serialize(new { content = chunk });
                Console.Write(jsonChunk);
            }
            else
            {
                Console.Write(chunk);
            }
            
            content.Append(chunk);
        }

        // Write to file if specified
        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            await WriteToFileAsync(options.OutputFile, content.ToString());
        }
    }

    /// <summary>
    /// Writes content to a file with proper permissions
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <param name="content">Content to write</param>
    private static async Task WriteToFileAsync(string filePath, string content)
    {
        // Strip ANSI sequences for security
        var cleanContent = System.Text.RegularExpressions.Regex.Replace(content, @"\x1B\[[0-9;]*[mGK]", "");
        
        await File.WriteAllTextAsync(filePath, cleanContent);
        
        // Set restrictive permissions on Unix systems
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignore permission errors
            }
        }
    }
}
