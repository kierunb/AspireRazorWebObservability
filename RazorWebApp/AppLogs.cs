namespace RazorWebApp;

public static partial class AppLogs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Index Page Visited. Description: {Description}.")]
    public static partial void LogIndexPageVisited(ILogger logger, string description);
}
