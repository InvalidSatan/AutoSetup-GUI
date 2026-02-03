using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Helper class for Windows Registry operations.
/// </summary>
public class RegistryHelper
{
    private readonly ILogger<RegistryHelper> _logger;

    public RegistryHelper(ILogger<RegistryHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a registry value.
    /// </summary>
    public T? GetValue<T>(string keyPath, string valueName, RegistryHive hive = RegistryHive.LocalMachine)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var subKey = baseKey.OpenSubKey(keyPath);

            if (subKey == null)
            {
                _logger.LogDebug("Registry key not found: {Hive}\\{Path}", hive, keyPath);
                return default;
            }

            var value = subKey.GetValue(valueName);
            if (value == null)
            {
                _logger.LogDebug("Registry value not found: {Hive}\\{Path}\\{Value}", hive, keyPath, valueName);
                return default;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading registry value: {Hive}\\{Path}\\{Value}", hive, keyPath, valueName);
            return default;
        }
    }

    /// <summary>
    /// Checks if a registry key exists.
    /// </summary>
    public bool KeyExists(string keyPath, RegistryHive hive = RegistryHive.LocalMachine)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var subKey = baseKey.OpenSubKey(keyPath);
            return subKey != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a registry value exists.
    /// </summary>
    public bool ValueExists(string keyPath, string valueName, RegistryHive hive = RegistryHive.LocalMachine)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var subKey = baseKey.OpenSubKey(keyPath);
            return subKey?.GetValue(valueName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks for pending reboot indicators in the registry.
    /// </summary>
    public (bool IsPending, string Reason) CheckPendingReboot()
    {
        var reasons = new List<string>();

        // Check Component Based Servicing
        if (KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
        {
            reasons.Add("Component Based Servicing pending");
        }

        // Check Windows Update
        if (KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
        {
            reasons.Add("Windows Update reboot required");
        }

        // Check Pending File Rename Operations
        var pendingFileRenames = GetValue<string[]>(
            @"SYSTEM\CurrentControlSet\Control\Session Manager",
            "PendingFileRenameOperations");

        if (pendingFileRenames != null && pendingFileRenames.Length > 0)
        {
            reasons.Add("Pending file rename operations");
        }

        // Check for pending computer rename
        var activeComputerName = GetValue<string>(
            @"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName",
            "ComputerName");

        var pendingComputerName = GetValue<string>(
            @"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName",
            "ComputerName");

        if (!string.IsNullOrEmpty(activeComputerName) &&
            !string.IsNullOrEmpty(pendingComputerName) &&
            !activeComputerName.Equals(pendingComputerName, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Pending computer name change");
        }

        // Check SCCM
        try
        {
            using var sccmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\CCM\CcmEval");
            if (sccmKey != null)
            {
                var rebootPending = sccmKey.GetValue("RebootPending");
                if (rebootPending != null && Convert.ToBoolean(rebootPending))
                {
                    reasons.Add("SCCM reboot pending");
                }
            }
        }
        catch { }

        return (reasons.Count > 0, string.Join(", ", reasons));
    }

    /// <summary>
    /// Gets SCCM client information from the registry.
    /// </summary>
    public (string SiteCode, string ManagementPoint, string ClientVersion) GetSCCMInfo()
    {
        var siteCode = GetValue<string>(@"SOFTWARE\Microsoft\CCM\CcmEval", "LastSiteCode") ?? "";
        var mp = GetValue<string>(@"SOFTWARE\Microsoft\CCM", "LastMPServer") ?? "";
        var version = GetValue<string>(@"SOFTWARE\Microsoft\SMS\Mobile Client", "ProductVersion") ?? "";

        return (siteCode, mp, version);
    }

    /// <summary>
    /// Gets Windows activation status.
    /// </summary>
    public (bool IsActivated, string Status) GetWindowsActivationStatus()
    {
        try
        {
            // Check license status via registry
            var statusPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform";
            var backupProductKeyDefault = GetValue<string>(statusPath, "BackupProductKeyDefault");

            // If there's a product key, it's likely activated
            // For more accurate results, we'll need to run slmgr.vbs
            if (!string.IsNullOrEmpty(backupProductKeyDefault))
            {
                return (true, "Licensed");
            }

            return (false, "Not Activated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Windows activation via registry");
            return (false, "Unknown");
        }
    }
}
