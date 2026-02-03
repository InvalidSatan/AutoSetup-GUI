using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    AppConfiguration Configuration { get; }

    /// <summary>
    /// Gets the application version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Reloads configuration from file.
    /// </summary>
    void Reload();

    /// <summary>
    /// Gets the Dell Command Update installer UNC path.
    /// </summary>
    string GetDCUInstallerPath();

    /// <summary>
    /// Gets the configured SCCM actions.
    /// </summary>
    IReadOnlyList<SCCMActionConfig> GetSCCMActions();

    /// <summary>
    /// Gets the local log path.
    /// </summary>
    string GetLocalLogPath();

    /// <summary>
    /// Gets the network log path.
    /// </summary>
    string GetNetworkLogPath();

    /// <summary>
    /// Gets the current theme setting.
    /// </summary>
    string GetTheme();

    /// <summary>
    /// Sets the theme.
    /// </summary>
    void SetTheme(string theme);
}
