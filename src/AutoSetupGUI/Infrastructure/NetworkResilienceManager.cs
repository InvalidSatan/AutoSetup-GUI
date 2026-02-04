using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Manages network resilience by running from a local copy and persisting state.
/// This prevents the app from crashing when network drivers are updated.
/// </summary>
public static class NetworkResilienceManager
{
    private const string LocalAppFolder = @"C:\Temp\UniversityAutoSetup\App";
    private const string StateFileName = "task_state.json";
    private const string RecoveryMarkerFileName = ".recovery_pending";

    /// <summary>
    /// Gets the path to the local app folder.
    /// </summary>
    public static string LocalAppPath => LocalAppFolder;

    /// <summary>
    /// Gets the path to the state file.
    /// </summary>
    public static string StateFilePath => Path.Combine(LocalAppFolder, StateFileName);

    /// <summary>
    /// Gets the path to the recovery marker file.
    /// </summary>
    public static string RecoveryMarkerPath => Path.Combine(LocalAppFolder, RecoveryMarkerFileName);

    /// <summary>
    /// Checks if the application is running from a network share (UNC path).
    /// </summary>
    public static bool IsRunningFromNetworkShare()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        // Check if it's a UNC path (\\server\share)
        if (exePath.StartsWith(@"\\"))
            return true;

        // Check if the drive is a network drive
        if (exePath.Length >= 2 && exePath[1] == ':')
        {
            var driveLetter = exePath.Substring(0, 2);
            try
            {
                var driveInfo = new DriveInfo(driveLetter);
                return driveInfo.DriveType == DriveType.Network;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if already running from the local copy.
    /// </summary>
    public static bool IsRunningFromLocalCopy()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        return exePath.StartsWith(LocalAppFolder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies the application to a local folder and relaunches from there.
    /// Returns true if relaunch was initiated (caller should exit).
    /// </summary>
    public static bool EnsureRunningLocally(string[]? args = null)
    {
        // Already running locally - no action needed
        if (IsRunningFromLocalCopy())
            return false;

        // Not running from network - no need to copy
        if (!IsRunningFromNetworkShare())
            return false;

        try
        {
            // Get source directory
            var sourceDir = AppDomain.CurrentDomain.BaseDirectory;

            // Ensure local folder exists
            Directory.CreateDirectory(LocalAppFolder);

            // Copy all files from source to local
            CopyDirectory(sourceDir, LocalAppFolder);

            // Get the local exe path
            var currentExeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "AutoSetup-GUI.exe");
            var localExePath = Path.Combine(LocalAppFolder, currentExeName);

            if (!File.Exists(localExePath))
            {
                // Fallback: look for any .exe in the folder
                var exeFiles = Directory.GetFiles(LocalAppFolder, "*.exe");
                if (exeFiles.Length > 0)
                    localExePath = exeFiles[0];
                else
                    return false; // Can't find executable
            }

            // Create a marker indicating we're launching from network copy
            File.WriteAllText(Path.Combine(LocalAppFolder, ".launched_from_network"), DateTime.Now.ToString("o"));

            // Launch the local copy
            var startInfo = new ProcessStartInfo
            {
                FileName = localExePath,
                Arguments = args != null ? string.Join(" ", args) : "--from-local-copy",
                UseShellExecute = true,
                Verb = "runas" // Maintain admin privileges
            };

            Process.Start(startInfo);
            return true; // Caller should exit
        }
        catch (Exception ex)
        {
            // Log error but don't crash - continue running from network
            Debug.WriteLine($"Failed to copy to local: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Copies a directory recursively.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            try
            {
                File.Copy(file, destFile, overwrite: true);
            }
            catch
            {
                // File might be locked, skip it
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            // Skip certain directories
            if (dirName.Equals("runtimes", StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectory(dir, Path.Combine(destDir, dirName));
            }
            else if (!dirName.StartsWith("."))
            {
                CopyDirectory(dir, Path.Combine(destDir, dirName));
            }
        }
    }

    /// <summary>
    /// Saves the current task execution state to disk.
    /// </summary>
    public static void SaveState(TaskExecutionState state)
    {
        try
        {
            Directory.CreateDirectory(LocalAppFolder);
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json);
        }
        catch
        {
            // Ignore save errors - best effort
        }
    }

    /// <summary>
    /// Loads the task execution state from disk.
    /// </summary>
    public static TaskExecutionState? LoadState()
    {
        try
        {
            if (!File.Exists(StateFilePath))
                return null;

            var json = File.ReadAllText(StateFilePath);
            return JsonSerializer.Deserialize<TaskExecutionState>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the saved state (call when tasks complete successfully).
    /// </summary>
    public static void ClearState()
    {
        try
        {
            if (File.Exists(StateFilePath))
                File.Delete(StateFilePath);

            if (File.Exists(RecoveryMarkerPath))
                File.Delete(RecoveryMarkerPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Marks that a recovery is pending (tasks were interrupted).
    /// </summary>
    public static void MarkRecoveryPending()
    {
        try
        {
            Directory.CreateDirectory(LocalAppFolder);
            File.WriteAllText(RecoveryMarkerPath, DateTime.Now.ToString("o"));
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Checks if there's a pending recovery from a previous interrupted session.
    /// </summary>
    public static bool HasPendingRecovery()
    {
        try
        {
            if (!File.Exists(RecoveryMarkerPath))
                return false;

            // Check if the marker is recent (within last hour)
            var markerTime = File.GetLastWriteTime(RecoveryMarkerPath);
            return DateTime.Now - markerTime < TimeSpan.FromHours(1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if there's an active state file that indicates tasks were running.
    /// </summary>
    public static bool HasActiveState()
    {
        try
        {
            if (!File.Exists(StateFilePath))
                return false;

            var state = LoadState();
            return state != null && state.IsRunning && !state.IsComplete;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Schedules cleanup of the local app folder after the application exits.
    /// Uses a Windows Scheduled Task for reliable cleanup even if files are briefly locked.
    /// </summary>
    public static void ScheduleSelfCleanup()
    {
        try
        {
            if (!Directory.Exists(LocalAppFolder))
                return;

            var cleanupLogPath = Path.Combine(Path.GetTempPath(), "autosetup_cleanup.log");
            var taskName = "UniversityAutoSetupCleanup";

            // Create a simple cleanup script
            var cleanupScriptPath = Path.Combine(Path.GetTempPath(), "autosetup_cleanup.cmd");
            var script = $@"@echo off
:: Cleanup script for University Auto Setup
:: Log file: {cleanupLogPath}

echo [%date% %time%] Scheduled cleanup starting >> ""{cleanupLogPath}""
echo [%date% %time%] Target: {LocalAppFolder} >> ""{cleanupLogPath}""

:: Delete the app folder
rd /s /q ""{LocalAppFolder}"" 2>>""{cleanupLogPath}""

if exist ""{LocalAppFolder}"" (
    echo [%date% %time%] First attempt failed, waiting and retrying... >> ""{cleanupLogPath}""
    timeout /t 30 /nobreak > nul
    rd /s /q ""{LocalAppFolder}"" 2>>""{cleanupLogPath}""
)

if exist ""{LocalAppFolder}"" (
    echo [%date% %time%] Second attempt failed, final retry... >> ""{cleanupLogPath}""
    timeout /t 60 /nobreak > nul
    rd /s /q ""{LocalAppFolder}"" 2>>""{cleanupLogPath}""
)

if not exist ""{LocalAppFolder}"" (
    echo [%date% %time%] Successfully deleted app folder >> ""{cleanupLogPath}""
    :: Try to delete parent folder if empty
    rd ""{Path.GetDirectoryName(LocalAppFolder)}"" 2>nul
) else (
    echo [%date% %time%] Failed to delete folder >> ""{cleanupLogPath}""
)

:: Delete the scheduled task
schtasks /delete /tn ""{taskName}"" /f >nul 2>&1

echo [%date% %time%] Cleanup finished >> ""{cleanupLogPath}""
";

            File.WriteAllText(cleanupScriptPath, script);

            File.WriteAllText(cleanupScriptPath, script);

            // Delete any existing cleanup task first
            var deleteTaskInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/delete /tn \"{taskName}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try { Process.Start(deleteTaskInfo)?.WaitForExit(5000); } catch { }

            // Schedule the cleanup to run 2 minutes from now using Windows Task Scheduler
            // This runs in SYSTEM context which won't have the same file locks
            var runTime = DateTime.Now.AddMinutes(2);
            var scheduleTime = runTime.ToString("HH:mm");
            var scheduleDate = runTime.ToString("MM/dd/yyyy");

            var createTaskInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{cleanupScriptPath}\\\"\" /sc once /st {scheduleTime} /sd {scheduleDate} /f /rl highest",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var createProcess = Process.Start(createTaskInfo);
            createProcess?.WaitForExit(10000);

            Debug.WriteLine($"Scheduled cleanup task '{taskName}' to run at {scheduleTime}");
            Debug.WriteLine($"Cleanup log will be at: {cleanupLogPath}");
        }
        catch (Exception ex)
        {
            // Best effort - don't crash if cleanup scheduling fails
            Debug.WriteLine($"Failed to schedule cleanup: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents the state of task execution for recovery purposes.
/// </summary>
public class TaskExecutionState
{
    public DateTime StartTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public bool IsRunning { get; set; }
    public bool IsComplete { get; set; }

    // Task selection state
    public bool RunGroupPolicy { get; set; }
    public bool RunSCCMActions { get; set; }
    public bool RunDellUpdates { get; set; }
    public bool RunImageChecks { get; set; }

    // Task completion state
    public bool GroupPolicyComplete { get; set; }
    public bool SCCMActionsComplete { get; set; }
    public bool DellUpdatesComplete { get; set; }
    public bool ImageChecksComplete { get; set; }

    // Current Dell update state (most likely to be interrupted)
    public string? CurrentDellPhase { get; set; }
    public int DellTotalUpdates { get; set; }
    public int DellCompletedUpdates { get; set; }
    public List<string> DellCompletedUpdateNames { get; set; } = new();

    // Log output to restore
    public List<string> LogMessages { get; set; } = new();

    // Results
    public bool? RequiresRestart { get; set; }
    public string? ErrorMessage { get; set; }
}
