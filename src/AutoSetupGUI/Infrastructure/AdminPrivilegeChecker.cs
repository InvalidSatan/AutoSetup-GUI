using System.Diagnostics;
using System.Security.Principal;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Utility class for checking and requesting administrative privileges.
/// </summary>
public static class AdminPrivilegeChecker
{
    /// <summary>
    /// Checks if the current process is running with administrative privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to restart the application with administrative privileges.
    /// </summary>
    /// <returns>True if restart was initiated, false otherwise.</returns>
    public static bool RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
