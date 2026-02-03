using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for application logging with dual-location support.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Event raised when a new log entry is added.
    /// </summary>
    event EventHandler<LogEntry>? LogEntryAdded;

    /// <summary>
    /// Initializes the logging system and creates log directories.
    /// </summary>
    Task InitializeAsync(string serviceTag, string computerName);

    /// <summary>
    /// Logs a message.
    /// </summary>
    void Log(string message, LogLevel level = LogLevel.Info, string source = "");

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    void LogDebug(string message, string source = "");

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void LogInfo(string message, string source = "");

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void LogWarning(string message, string source = "");

    /// <summary>
    /// Logs an error message.
    /// </summary>
    void LogError(string message, string source = "", Exception? exception = null);

    /// <summary>
    /// Logs a success message.
    /// </summary>
    void LogSuccess(string message, string source = "");

    /// <summary>
    /// Logs a section header.
    /// </summary>
    void LogSection(string title);

    /// <summary>
    /// Logs a header.
    /// </summary>
    void LogHeader(string title);

    /// <summary>
    /// Gets all log entries.
    /// </summary>
    IReadOnlyList<LogEntry> GetLogEntries();

    /// <summary>
    /// Gets the local log directory path.
    /// </summary>
    string GetLocalLogPath();

    /// <summary>
    /// Gets the network log directory path, if available.
    /// </summary>
    string? GetNetworkLogPath();

    /// <summary>
    /// Checks if network logging is available.
    /// </summary>
    bool IsNetworkLoggingAvailable();

    /// <summary>
    /// Flushes all pending log entries to files.
    /// </summary>
    Task FlushAsync();
}
