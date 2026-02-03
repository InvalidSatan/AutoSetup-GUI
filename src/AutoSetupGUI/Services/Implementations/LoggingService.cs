using System.Collections.Concurrent;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Logging service with dual-location support (local and network P:\ drive).
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentQueue<LogEntry> _logEntries = new();
    private readonly object _fileLock = new();

    private string _localLogPath = string.Empty;
    private string? _networkLogPath;
    private string _mainLogFile = string.Empty;
    private string? _networkMainLogFile;
    private bool _isInitialized;
    private bool _networkLoggingAvailable;

    public event EventHandler<LogEntry>? LogEntryAdded;

    public LoggingService(ILogger<LoggingService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InitializeAsync(string serviceTag, string computerName)
    {
        if (_isInitialized)
            return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var folderName = $"{serviceTag}_{computerName} - {date}";

        // Set up local logging
        var localBasePath = _configuration["Logging:LocalPath"] ?? @"C:\Temp\UniversityAutoSetup\Logs";
        _localLogPath = Path.Combine(localBasePath, folderName);

        try
        {
            Directory.CreateDirectory(_localLogPath);
            _mainLogFile = Path.Combine(_localLogPath, $"UniversityAutoSetup_v3.0.0_{timestamp}.log");
            _logger.LogDebug("Local log directory created: {Path}", _localLogPath);
        }
        catch (Exception ex)
        {
            // Fall back to temp folder
            _localLogPath = Path.Combine(Path.GetTempPath(), "UniversityAutoSetup", "Logs", folderName);
            Directory.CreateDirectory(_localLogPath);
            _mainLogFile = Path.Combine(_localLogPath, $"UniversityAutoSetup_v3.0.0_{timestamp}.log");
            _logger.LogWarning(ex, "Failed to create local log directory, using fallback: {Path}", _localLogPath);
        }

        // Set up network logging (P:\ drive)
        var networkBasePath = _configuration["Logging:NetworkPath"] ?? @"P:\UniversityAutoSetup\Logs";
        _networkLogPath = Path.Combine(networkBasePath, folderName);

        try
        {
            if (Directory.Exists(Path.GetPathRoot(networkBasePath)))
            {
                Directory.CreateDirectory(_networkLogPath);
                _networkMainLogFile = Path.Combine(_networkLogPath, $"UniversityAutoSetup_v3.0.0_{timestamp}.log");
                _networkLoggingAvailable = true;
                _logger.LogDebug("Network log directory created: {Path}", _networkLogPath);
            }
            else
            {
                _networkLoggingAvailable = false;
                _networkLogPath = null;
                _logger.LogWarning("Network drive (P:\\) not available, logging to local only");
            }
        }
        catch (Exception ex)
        {
            _networkLoggingAvailable = false;
            _networkLogPath = null;
            _logger.LogWarning(ex, "Failed to set up network logging");
        }

        _isInitialized = true;

        // Write initial log entries
        LogHeader("University Auto Setup v3.0");
        LogInfo($"Log initialized at {DateTime.Now}");
        LogInfo($"Computer Name: {computerName}");
        LogInfo($"Service Tag: {serviceTag}");
        LogInfo($"Local log location: {_localLogPath}");

        if (_networkLoggingAvailable)
        {
            LogInfo($"Network log location: {_networkLogPath}");
        }
        else
        {
            LogWarning("Network logging not available (P:\\ drive not accessible)");
        }

        await Task.CompletedTask;
    }

    public void Log(string message, LogLevel level = LogLevel.Info, string source = "")
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };

        _logEntries.Enqueue(entry);
        WriteToFile(entry);

        LogEntryAdded?.Invoke(this, entry);
    }

    public void LogDebug(string message, string source = "") => Log(message, LogLevel.Debug, source);
    public void LogInfo(string message, string source = "") => Log(message, LogLevel.Info, source);
    public void LogWarning(string message, string source = "") => Log(message, LogLevel.Warning, source);
    public void LogSuccess(string message, string source = "") => Log(message, LogLevel.Success, source);

    public void LogError(string message, string source = "", Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Error,
            Message = message,
            Source = source,
            Exception = exception?.ToString()
        };

        _logEntries.Enqueue(entry);
        WriteToFile(entry);

        if (exception != null)
        {
            WriteToFile(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Debug,
                Message = $"Exception details: {exception}",
                Source = source
            });
        }

        LogEntryAdded?.Invoke(this, entry);
    }

    public void LogSection(string title)
    {
        var separator = new string('-', 80);
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Section,
            Message = $"\n{separator}\n{title}\n{separator}"
        };

        _logEntries.Enqueue(entry);
        WriteToFile(entry);

        LogEntryAdded?.Invoke(this, entry);
    }

    public void LogHeader(string title)
    {
        var separator = new string('=', 80);
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Header,
            Message = $"\n{separator}\n{title}\n{separator}"
        };

        _logEntries.Enqueue(entry);
        WriteToFile(entry);

        LogEntryAdded?.Invoke(this, entry);
    }

    private void WriteToFile(LogEntry entry)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_mainLogFile))
            return;

        var line = entry.FileFormattedMessage;

        lock (_fileLock)
        {
            try
            {
                // Write to local file
                File.AppendAllText(_mainLogFile, line + Environment.NewLine);

                // Write to network file if available
                if (_networkLoggingAvailable && !string.IsNullOrEmpty(_networkMainLogFile))
                {
                    try
                    {
                        File.AppendAllText(_networkMainLogFile, line + Environment.NewLine);
                    }
                    catch
                    {
                        // Silently fail on network write errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error writing to log file");
            }
        }
    }

    public IReadOnlyList<LogEntry> GetLogEntries()
    {
        return _logEntries.ToList();
    }

    public string GetLocalLogPath() => _localLogPath;

    public string? GetNetworkLogPath() => _networkLogPath;

    public bool IsNetworkLoggingAvailable() => _networkLoggingAvailable;

    public async Task FlushAsync()
    {
        // All writes are synchronous, but we can add async file operations if needed
        await Task.CompletedTask;
    }
}
