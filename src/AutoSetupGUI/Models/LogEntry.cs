namespace AutoSetupGUI.Models;

/// <summary>
/// Represents a log entry for display in the UI and file logging.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public string FormattedMessage => $"[{FormattedTimestamp}] [{Level}] {(string.IsNullOrEmpty(Source) ? "" : $"[{Source}] ")}{Message}";

    public string FileFormattedMessage => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {(string.IsNullOrEmpty(Source) ? "" : $"[{Source}] ")}{Message}";

    public static LogEntry Debug(string message, string source = "") =>
        new() { Level = LogLevel.Debug, Message = message, Source = source };

    public static LogEntry Info(string message, string source = "") =>
        new() { Level = LogLevel.Info, Message = message, Source = source };

    public static LogEntry Warning(string message, string source = "") =>
        new() { Level = LogLevel.Warning, Message = message, Source = source };

    public static LogEntry Error(string message, string source = "", string? exception = null) =>
        new() { Level = LogLevel.Error, Message = message, Source = source, Exception = exception };

    public static LogEntry Success(string message, string source = "") =>
        new() { Level = LogLevel.Success, Message = message, Source = source };

    public static LogEntry Header(string message) =>
        new() { Level = LogLevel.Header, Message = message };

    public static LogEntry Section(string message) =>
        new() { Level = LogLevel.Section, Message = message };
}

/// <summary>
/// Log levels matching the existing PowerShell script.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Success,
    Header,
    Section
}
