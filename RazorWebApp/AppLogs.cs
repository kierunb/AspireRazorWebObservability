namespace RazorWebApp;

public static partial class AppLogs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Index Page Visited. Description: {Description}.")]
    public static partial void LogIndexPageVisited(ILogger logger, string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "Index Page Visited. Description: {Description}, Details: {Details}.")]
    public static partial void LogIndexPageVisitedDetailed(ILogger logger, string description, string details);
}
