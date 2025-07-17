using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AiCli.Infrastructure;

/// <summary>
/// Configuration for logging setup
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog logging
    /// </summary>
    /// <param name="minimumLevel">Minimum log level for console output</param>
    /// <returns>Configured logger</returns>
    public static Serilog.ILogger ConfigureLogging(LogLevel minimumLevel = LogLevel.Information)
    {
        var logDirectory = GetLogDirectory();
        var logPath = Path.Combine(logDirectory, "logs", "ai-cli.log");

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(ConvertToSerilogLevel(minimumLevel))
            .WriteTo.Console(restrictedToMinimumLevel: ConvertToSerilogLevel(LogLevel.Error))
            .WriteTo.File(logPath, 
                restrictedToMinimumLevel: LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true);

        return config.CreateLogger();
    }

    private static string GetLogDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var logDirectory = Path.Combine(userProfile, ".ai-cli");
        
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            
            // Set permissions on Unix systems
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(logDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch
                {
                    // Ignore permission errors
                }
            }
        }

        return logDirectory;
    }

    private static LogEventLevel ConvertToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}