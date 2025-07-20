namespace AiCli.CLI;

/// <summary>
/// Exit codes for the application
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Success
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Invalid arguments
    /// </summary>
    public const int InvalidArguments = 1;

    /// <summary>
    /// API communication error
    /// </summary>
    public const int ApiError = 2;

    /// <summary>
    /// File or IO error
    /// </summary>
    public const int FileError = 3;

    /// <summary>
    /// Unknown/unhandled error
    /// </summary>
    public const int UnknownError = 4;
}