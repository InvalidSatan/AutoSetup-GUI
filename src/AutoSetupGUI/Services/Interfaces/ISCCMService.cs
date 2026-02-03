using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for SCCM client operations.
/// </summary>
public interface ISCCMService
{
    /// <summary>
    /// Checks the health of the SCCM client.
    /// </summary>
    Task<SCCMClientHealth> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs all configured SCCM client actions.
    /// </summary>
    Task<IEnumerable<SCCMActionResult>> RunAllActionsAsync(
        IProgress<SCCMActionResult>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a specific SCCM action.
    /// </summary>
    Task<SCCMActionResult> RunActionAsync(
        SCCMAction action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to repair the SCCM client.
    /// </summary>
    Task<bool> RepairClientAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured SCCM actions.
    /// </summary>
    IReadOnlyList<SCCMAction> GetConfiguredActions();
}
