using AiCli.Application;
using AiCli.CLI;
using AiCli.Configuration;
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
            
            // Handle config mode
            if (options.Config)
            {
                return await HandleConfigModeAsync(cancellationToken);
            }
            
            // Initialize user settings service
            var settingsPath = SettingsPathProvider.GetDefaultSettingsPath();
            var tempServiceCollection = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                });
            
            // Register platform-specific encryption service
            if (OperatingSystem.IsWindows())
            {
                tempServiceCollection.AddSingleton<IEncryptionService, DpapiEncryptionService>();
            }
            else
            {
                tempServiceCollection.AddSingleton<IEncryptionService, AesEncryptionService>();
            }
            
            var tempServiceProvider = tempServiceCollection.BuildServiceProvider();
            var settingsLogger = tempServiceProvider.GetRequiredService<ILogger<FileUserSettingsService>>();
            var encryptionService = tempServiceProvider.GetRequiredService<IEncryptionService>();
            
            var userSettingsService = new FileUserSettingsService(settingsPath, settingsLogger, encryptionService);
            var userSettings = userSettingsService.Load();
            
            // Get the default model configuration
            var defaultModelConfig = userSettings.GetDefaultModelConfiguration();
            if (defaultModelConfig == null)
            {
                Console.Error.WriteLine("Error: No model configuration found in settings. Please configure at least one model.");
                return ExitCodes.InvalidArguments;
            }
            
            // CLI options override model configuration settings
            var effectiveApiKey = options.ApiKey ?? defaultModelConfig.ApiKey ?? Environment.GetEnvironmentVariable("AI_API_KEY");
            var effectiveBaseUrl = options.BaseUrl ?? defaultModelConfig.BaseUrl;
            var effectiveModel = options.Model != "gpt-3.5-turbo" ? options.Model : defaultModelConfig.Model;
            var effectiveTemperature = options.Temperature != 1.0f ? options.Temperature : defaultModelConfig.Temperature;
            var effectiveMaxTokens = options.MaxTokens ?? defaultModelConfig.MaxTokens;
            var effectiveFormat = options.Format != "text" ? options.Format : defaultModelConfig.Format;
            var effectiveStream = options.Stream != false ? options.Stream : defaultModelConfig.Stream;
            
            // Validate API key
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                Console.Error.WriteLine("Error: API key is required. Set AI_API_KEY environment variable, use --api-key option, or configure in settings file.");
                return ExitCodes.InvalidArguments;
            }

            // Update CLI options with effective values
            options.ApiKey = effectiveApiKey;
            options.BaseUrl = effectiveBaseUrl;
            options.Model = effectiveModel;
            options.Temperature = effectiveTemperature;
            options.MaxTokens = effectiveMaxTokens;
            options.Format = effectiveFormat;
            options.Stream = effectiveStream;

            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, userSettingsService);
            
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
    /// Handles configuration mode
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code</returns>
    private static async Task<int> HandleConfigModeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Initialize user settings service
            var settingsPath = SettingsPathProvider.GetDefaultSettingsPath();
            var tempServiceCollection = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                });
            
            // Register platform-specific encryption service
            if (OperatingSystem.IsWindows())
            {
                tempServiceCollection.AddSingleton<IEncryptionService, DpapiEncryptionService>();
            }
            else
            {
                tempServiceCollection.AddSingleton<IEncryptionService, AesEncryptionService>();
            }
            
            var tempServiceProvider = tempServiceCollection.BuildServiceProvider();
            var settingsLogger = tempServiceProvider.GetRequiredService<ILogger<FileUserSettingsService>>();
            var encryptionService = tempServiceProvider.GetRequiredService<IEncryptionService>();
            
            var userSettingsService = new FileUserSettingsService(settingsPath, settingsLogger, encryptionService);
            
            // Create configuration service
            var configLogger = tempServiceProvider.GetRequiredService<ILogger<ConfigurationService>>();
            var configService = new ConfigurationService(userSettingsService, configLogger);
            
            // Start configuration menu
            await configService.StartConfigurationAsync();
            
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return ExitCodes.UnknownError;
        }
    }

    /// <summary>
    /// Configures dependency injection services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="userSettingsService">User settings service instance</param>
    private static void ConfigureServices(ServiceCollection services, IUserSettingsService userSettingsService)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        // Register user settings service as singleton
        services.AddSingleton(userSettingsService);
        
        // Register platform-specific encryption service as singleton
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IEncryptionService, DpapiEncryptionService>();
        }
        else
        {
            services.AddSingleton<IEncryptionService, AesEncryptionService>();
        }

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
            var settingsService = provider.GetRequiredService<IUserSettingsService>();
            var settings = settingsService.Load();
            var defaultModelConfig = settings.GetDefaultModelConfiguration();
            
            // Use settings for API key and base URL, with CLI override capability
            var apiKey = defaultModelConfig?.ApiKey ?? Environment.GetEnvironmentVariable("AI_API_KEY");
            var baseUrl = defaultModelConfig?.BaseUrl;
            
            return new OpenAIClient(httpClient, logger, apiKey!, baseUrl);
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
