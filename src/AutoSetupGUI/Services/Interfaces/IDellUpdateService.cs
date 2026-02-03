using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for Dell Command Update operations.
/// </summary>
public interface IDellUpdateService
{
    /// <summary>
    /// Checks if the current system is a Dell machine.
    /// </summary>
    bool IsDellSystem();

    /// <summary>
    /// Checks if Dell Command Update is installed.
    /// </summary>
    bool IsInstalled();

    /// <summary>
    /// Installs Dell Command Update from the network share.
    /// </summary>
    Task<TaskResult> InstallAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures Dell Command Update with recommended settings.
    /// </summary>
    Task<TaskResult> ConfigureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans for available Dell updates.
    /// </summary>
    Task<DellScanResult> ScanForUpdatesAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies all available Dell updates.
    /// </summary>
    Task<TaskResult> ApplyUpdatesAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the complete Dell update workflow (install if needed, configure, scan, apply).
    /// </summary>
    Task<TaskResult> RunCompleteUpdateAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a Dell Command Update scan.
/// </summary>
public class DellScanResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public bool UpdatesAvailable { get; set; }
    public int UpdateCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RawOutput { get; set; } = string.Empty;
}
