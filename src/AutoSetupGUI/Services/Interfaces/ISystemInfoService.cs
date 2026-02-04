using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for collecting comprehensive system information.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>
    /// Collects all system information asynchronously.
    /// </summary>
    Task<SystemInfo> CollectSystemInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets just the service tag (serial number).
    /// </summary>
    string GetServiceTag();

    /// <summary>
    /// Gets the computer name.
    /// </summary>
    string GetComputerName();

    /// <summary>
    /// Copies system info to clipboard in a formatted string.
    /// </summary>
    void CopyToClipboard(SystemInfo info);

    /// <summary>
    /// Exports system info to a file.
    /// </summary>
    Task ExportToFileAsync(SystemInfo info, string filePath, ExportFormat format);

    /// <summary>
    /// Clears the cached system info to force fresh data collection.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Format for exporting system information.
/// </summary>
public enum ExportFormat
{
    Text,
    Json,
    Csv
}
