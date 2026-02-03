using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for post-image verification checks.
/// </summary>
public interface IImageCheckService
{
    /// <summary>
    /// Runs all image verification checks.
    /// </summary>
    Task<ImageCheckResult> RunAllChecksAsync(
        IProgress<ImageCheck>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks Windows activation status.
    /// </summary>
    Task<ImageCheck> CheckWindowsActivationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks domain membership.
    /// </summary>
    Task<ImageCheck> CheckDomainJoinAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks SCCM client health.
    /// </summary>
    Task<ImageCheck> CheckSCCMClientAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks disk space.
    /// </summary>
    Task<ImageCheck> CheckDiskSpaceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks network connectivity and P:\ drive access.
    /// </summary>
    Task<ImageCheck> CheckNetworkAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for pending reboots.
    /// </summary>
    Task<ImageCheck> CheckPendingRebootAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks BitLocker status.
    /// </summary>
    Task<ImageCheck> CheckBitLockerAsync(CancellationToken cancellationToken = default);
}
