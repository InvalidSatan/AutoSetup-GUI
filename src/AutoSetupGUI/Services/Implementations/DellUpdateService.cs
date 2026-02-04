using System.IO;
using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for Dell Command Update operations.
/// </summary>
public class DellUpdateService : IDellUpdateService
{
    private readonly ILogger<DellUpdateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly WmiHelper _wmiHelper;
    private readonly ProcessRunner _processRunner;

    private string? _dcuPath;

    public DellUpdateService(
        ILogger<DellUpdateService> logger,
        IConfiguration configuration,
        WmiHelper wmiHelper,
        ProcessRunner processRunner)
    {
        _logger = logger;
        _configuration = configuration;
        _wmiHelper = wmiHelper;
        _processRunner = processRunner;
    }

    public bool IsDellSystem()
    {
        try
        {
            var (manufacturer, _, _, _) = _wmiHelper.GetComputerSystemInfo();
            return manufacturer.Contains("Dell", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if Dell system");
            return false;
        }
    }

    public bool IsInstalled()
    {
        _dcuPath = FindDcuPath();
        return !string.IsNullOrEmpty(_dcuPath);
    }

    private string? FindDcuPath()
    {
        var paths = _configuration.GetSection("DellCommandUpdate:InstallPaths").Get<string[]>()
            ?? new[]
            {
                @"C:\Program Files\Dell\CommandUpdate\dcu-cli.exe",
                @"C:\Program Files (x86)\Dell\CommandUpdate\dcu-cli.exe"
            };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Found Dell Command Update at: {Path}", path);
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if .NET 8 Desktop Runtime is installed.
    /// </summary>
    public bool IsDotNet8Installed()
    {
        try
        {
            // Check for .NET 8 Desktop Runtime by looking for the runtime folder
            var dotnetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "shared", "Microsoft.WindowsDesktop.App");

            if (Directory.Exists(dotnetPath))
            {
                var versions = Directory.GetDirectories(dotnetPath);
                foreach (var version in versions)
                {
                    var versionName = Path.GetFileName(version);
                    if (versionName.StartsWith("8."))
                    {
                        _logger.LogDebug(".NET 8 Desktop Runtime found: {Version}", versionName);
                        return true;
                    }
                }
            }

            // Also check via registry
            var registryPath = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath);
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    if (valueName.StartsWith("8."))
                    {
                        _logger.LogDebug(".NET 8 Desktop Runtime found in registry: {Version}", valueName);
                        return true;
                    }
                }
            }

            _logger.LogDebug(".NET 8 Desktop Runtime not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for .NET 8 Desktop Runtime");
            return false;
        }
    }

    /// <summary>
    /// Installs .NET 8 Desktop Runtime if not present.
    /// </summary>
    public async Task<TaskResult> InstallDotNet8Async(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking .NET 8 Desktop Runtime installation...");

        var result = new TaskResult
        {
            TaskId = "dotnet8_install",
            TaskName = ".NET 8 Desktop Runtime Installation",
            StartTime = DateTime.Now,
            Status = TaskStatus.Running
        };

        // Check if already installed
        if (IsDotNet8Installed())
        {
            result.Status = TaskStatus.Success;
            result.Message = ".NET 8 Desktop Runtime is already installed";
            result.EndTime = DateTime.Now;
            _logger.LogInformation(".NET 8 Desktop Runtime is already installed");
            return result;
        }

        progress?.Report("Installing .NET 8 Desktop Runtime (required for Dell Command Update)...");

        var installerUNC = _configuration.GetValue<string>("DellCommandUpdate:DotNet8InstallerUNC")
            ?? @"\\server\share\Dell\DCU\dotnet-sdk-8.0.417-win-x64.exe";

        var localInstallerPath = Path.Combine(Path.GetTempPath(), "dotnet-sdk-8.0.417-win-x64.exe");

        try
        {
            // Check if UNC path is accessible
            if (!File.Exists(installerUNC))
            {
                result.Status = TaskStatus.Error;
                result.Message = $".NET 8 installer not found at: {installerUNC}";
                result.EndTime = DateTime.Now;
                _logger.LogError(".NET 8 installer not found at UNC path: {Path}", installerUNC);
                return result;
            }

            // Copy installer locally
            progress?.Report("Copying .NET 8 installer locally...");
            _logger.LogInformation("Copying .NET 8 installer from {Source} to {Destination}", installerUNC, localInstallerPath);

            File.Copy(installerUNC, localInstallerPath, overwrite: true);

            // Run installer silently
            progress?.Report("Installing .NET 8 Desktop Runtime (this may take a few minutes)...");
            _logger.LogInformation("Starting .NET 8 installation...");

            var installResult = await _processRunner.RunAsync(
                localInstallerPath,
                "/install /quiet /norestart",
                timeoutMs: 600000, // 10 minutes
                cancellationToken: cancellationToken);

            _logger.LogInformation(".NET 8 installer exited with code: {ExitCode}", installResult.ExitCode);

            // Exit codes: 0 = success, 3010 = success but reboot required, 1641 = success reboot initiated
            if (installResult.ExitCode == 0 || installResult.ExitCode == 3010 || installResult.ExitCode == 1641)
            {
                result.Status = TaskStatus.Success;
                result.Message = ".NET 8 Desktop Runtime installed successfully";
                result.RequiresRestart = installResult.ExitCode == 3010 || installResult.ExitCode == 1641;
                _logger.LogInformation(".NET 8 Desktop Runtime installed successfully");
            }
            else
            {
                result.Status = TaskStatus.Error;
                result.Message = $".NET 8 installation failed with exit code: {installResult.ExitCode}";
                _logger.LogError(".NET 8 installation failed with exit code: {ExitCode}", installResult.ExitCode);
            }

            result.ExitCode = installResult.ExitCode;
            result.DetailedOutput = installResult.CombinedOutput;
        }
        catch (Exception ex)
        {
            result.Status = TaskStatus.Error;
            result.Message = $".NET 8 installation failed: {ex.Message}";
            _logger.LogError(ex, "Error installing .NET 8 Desktop Runtime");
        }
        finally
        {
            // Cleanup
            if (File.Exists(localInstallerPath))
            {
                try { File.Delete(localInstallerPath); } catch { }
            }
        }

        result.EndTime = DateTime.Now;
        return result;
    }

    public async Task<TaskResult> InstallAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking Dell Command Update installation...");

        var result = new TaskResult
        {
            TaskId = "dcu_install",
            TaskName = "Dell Command Update Installation",
            StartTime = DateTime.Now,
            Status = TaskStatus.Running
        };

        // Check if already installed
        if (IsInstalled())
        {
            result.Status = TaskStatus.Success;
            result.Message = "Dell Command Update is already installed";
            result.EndTime = DateTime.Now;
            _logger.LogInformation("Dell Command Update is already installed at: {Path}", _dcuPath);
            return result;
        }

        // Check and install .NET 8 if needed (DCU 5.x requires .NET 8)
        progress?.Report("Checking .NET 8 Desktop Runtime dependency...");
        if (!IsDotNet8Installed())
        {
            _logger.LogInformation(".NET 8 Desktop Runtime not found, installing...");
            var dotNetResult = await InstallDotNet8Async(progress, cancellationToken);

            if (dotNetResult.Status == TaskStatus.Error)
            {
                result.Status = TaskStatus.Error;
                result.Message = $"Failed to install .NET 8 prerequisite: {dotNetResult.Message}";
                result.EndTime = DateTime.Now;
                return result;
            }

            // If restart is required after .NET install, we should note it
            if (dotNetResult.RequiresRestart)
            {
                result.RequiresRestart = true;
                _logger.LogWarning(".NET 8 installation requires restart before DCU can be installed");
            }
        }

        progress?.Report("Downloading Dell Command Update installer...");

        var installerUNC = _configuration.GetValue<string>("DellCommandUpdate:InstallerUNC")
            ?? @"\\server\share\Dell\DCU\DCU_Setup.exe";

        var installerLocalName = _configuration.GetValue<string>("DellCommandUpdate:InstallerLocalName")
            ?? "DCU_Setup.exe";

        var localInstallerPath = Path.Combine(Path.GetTempPath(), installerLocalName);

        try
        {
            // Check if UNC path is accessible
            if (!File.Exists(installerUNC))
            {
                result.Status = TaskStatus.Error;
                result.Message = $"Dell Command Update installer not found at: {installerUNC}";
                result.EndTime = DateTime.Now;
                _logger.LogError("DCU installer not found at UNC path: {Path}", installerUNC);
                return result;
            }

            // Copy installer locally
            progress?.Report("Copying installer locally...");
            _logger.LogInformation("Copying DCU installer from {Source} to {Destination}", installerUNC, localInstallerPath);

            var maxRetries = _configuration.GetValue("DellCommandUpdate:MaxRetries", 3);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    File.Copy(installerUNC, localInstallerPath, overwrite: true);
                    break;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Copy attempt {Attempt} failed, retrying...", attempt);
                    await Task.Delay(5000, cancellationToken);
                }
            }

            // Run installer
            progress?.Report("Installing Dell Command Update...");
            _logger.LogInformation("Starting DCU installation...");

            var installResult = await _processRunner.RunAsync(
                localInstallerPath,
                "-s",
                timeoutMs: 300000,
                cancellationToken: cancellationToken);

            // Wait for installation to complete
            for (int i = 0; i < 36; i++) // Wait up to 3 minutes
            {
                await Task.Delay(5000, cancellationToken);

                if (IsInstalled())
                {
                    result.Status = TaskStatus.Success;
                    result.Message = "Dell Command Update installed successfully";
                    result.EndTime = DateTime.Now;
                    _logger.LogInformation("Dell Command Update installed successfully");
                    return result;
                }

                progress?.Report($"Waiting for installation to complete... ({(i + 1) * 5}s)");
            }

            result.Status = TaskStatus.Error;
            result.Message = "Dell Command Update installation timed out";
            result.EndTime = DateTime.Now;
            _logger.LogError("DCU installation timed out");
        }
        catch (Exception ex)
        {
            result.Status = TaskStatus.Error;
            result.Message = $"Installation failed: {ex.Message}";
            result.EndTime = DateTime.Now;
            _logger.LogError(ex, "Error installing Dell Command Update");
        }
        finally
        {
            // Cleanup
            if (File.Exists(localInstallerPath))
            {
                try { File.Delete(localInstallerPath); } catch { }
            }
        }

        return result;
    }

    public async Task<TaskResult> ConfigureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Configuring Dell Command Update...");

        var result = new TaskResult
        {
            TaskId = "dcu_configure",
            TaskName = "Dell Command Update Configuration",
            StartTime = DateTime.Now,
            Status = TaskStatus.Running
        };

        var dcuPath = _dcuPath ?? FindDcuPath();
        if (string.IsNullOrEmpty(dcuPath))
        {
            result.Status = TaskStatus.Error;
            result.Message = "Dell Command Update not found";
            result.EndTime = DateTime.Now;
            return result;
        }

        // DCU 5.x /configure syntax with -silent flag (per working script)
        // -autoSuspendBitLocker=enable: Automatically suspend BitLocker for BIOS updates
        // -userConsent=disable: Don't prompt user for consent
        var configArgs = _configuration.GetValue<string>("DellCommandUpdate:ConfigureArgs")
            ?? "/configure -silent -autoSuspendBitLocker=enable -userConsent=disable";

        try
        {
            var processResult = await _processRunner.RunAsync(dcuPath, configArgs, timeoutMs: 60000, cancellationToken: cancellationToken);

            result.ExitCode = processResult.ExitCode;
            result.DetailedOutput = processResult.CombinedOutput;

            if (processResult.ExitCode == 0)
            {
                result.Status = TaskStatus.Success;
                result.Message = "Dell Command Update configured successfully";
                _logger.LogInformation("DCU configured successfully");
            }
            else
            {
                result.Status = TaskStatus.Warning;
                result.Message = $"DCU configuration returned exit code: {processResult.ExitCode}";
                _logger.LogWarning("DCU configuration returned exit code: {ExitCode}", processResult.ExitCode);
            }
        }
        catch (Exception ex)
        {
            result.Status = TaskStatus.Error;
            result.Message = $"Configuration failed: {ex.Message}";
            _logger.LogError(ex, "Error configuring DCU");
        }

        result.EndTime = DateTime.Now;
        return result;
    }

    public async Task<DellScanResult> ScanForUpdatesAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning for Dell updates...");

        var result = new DellScanResult();

        var dcuPath = _dcuPath ?? FindDcuPath();
        if (string.IsNullOrEmpty(dcuPath))
        {
            result.Success = false;
            result.Message = "Dell Command Update not found";
            return result;
        }

        var maxRetries = _configuration.GetValue("DellCommandUpdate:MaxRetries", 2);
        var retryDelay = _configuration.GetValue("DellCommandUpdate:RetryDelaySeconds", 10);
        // Per working script: exit codes 104, 106 are retryable for scan
        var retryableCodes = _configuration.GetSection("DellCommandUpdate:ExitCodes:ScanRetryable").Get<int[]>() ?? new[] { 104, 106 };

        // DCU 5.x exit codes for scan:
        // 0 = Updates available
        // 500 = No updates available
        // 1 = Reboot required from previous operation
        var noUpdatesCodes = new[] { _configuration.GetValue("DellCommandUpdate:ExitCodes:NoUpdates", 500) };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logPath = Path.Combine(Path.GetTempPath(), $"dcu_scan_{timestamp}.log");
        var reportPath = Path.Combine(Path.GetTempPath(), $"dcu_report_{timestamp}.xml");

        // Use -report to get detailed list of available updates
        var scanArgs = $"/scan -outputLog=\"{logPath}\" -report=\"{reportPath}\"";

        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            progress?.Report(attempt == 1
                ? "Scanning for Dell updates..."
                : $"Retrying scan (attempt {attempt})...");

            try
            {
                var processResult = await _processRunner.RunAsync(dcuPath, scanArgs, timeoutMs: 300000, cancellationToken: cancellationToken);

                result.ExitCode = processResult.ExitCode;
                result.RawOutput = processResult.CombinedOutput;

                // Read and log the scan output
                if (File.Exists(logPath))
                {
                    var logContent = File.ReadAllText(logPath);
                    result.RawOutput += Environment.NewLine + "=== DCU Log ===" + Environment.NewLine + logContent;
                    _logger.LogInformation("DCU scan log output:\n{LogContent}", logContent);
                }

                // Parse the report XML to get update details
                if (File.Exists(reportPath))
                {
                    try
                    {
                        var reportContent = File.ReadAllText(reportPath);
                        result.RawOutput += Environment.NewLine + "=== Update Report ===" + Environment.NewLine + reportContent;

                        // Parse updates from XML - look for UpdateInfo elements
                        var updateNames = new List<string>();
                        var updateMatches = System.Text.RegularExpressions.Regex.Matches(
                            reportContent,
                            @"<name>([^<]+)</name>",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        foreach (System.Text.RegularExpressions.Match match in updateMatches)
                        {
                            updateNames.Add(match.Groups[1].Value.Trim());
                        }

                        result.UpdateCount = updateNames.Count;

                        if (updateNames.Count > 0)
                        {
                            _logger.LogInformation("=== Found {Count} Dell update(s) available ===", updateNames.Count);
                            foreach (var name in updateNames)
                            {
                                _logger.LogInformation("  - {UpdateName}", name);
                            }

                            progress?.Report($"Found {updateNames.Count} update(s): {string.Join(", ", updateNames.Take(3))}{(updateNames.Count > 3 ? "..." : "")}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not parse DCU report XML");
                    }
                }

                if (processResult.ExitCode == 0)
                {
                    result.Success = true;
                    result.UpdatesAvailable = true;
                    result.Message = result.UpdateCount > 0
                        ? $"{result.UpdateCount} update(s) available"
                        : "Updates available";
                    _logger.LogInformation("DCU scan completed - {Message}", result.Message);
                    return result;
                }
                else if (noUpdatesCodes.Contains(processResult.ExitCode))
                {
                    result.Success = true;
                    result.UpdatesAvailable = false;
                    result.UpdateCount = 0;
                    result.Message = "No updates available";
                    _logger.LogInformation("DCU scan completed - no updates available (exit code {ExitCode})", processResult.ExitCode);
                    return result;
                }
                else if (processResult.ExitCode == 1)
                {
                    // Exit code 1 = Reboot required from previous operation
                    result.Success = true;
                    result.UpdatesAvailable = false;
                    result.Message = "Reboot required before scan can complete";
                    _logger.LogWarning("DCU scan: reboot required from previous operation");
                    return result;
                }
                else if (retryableCodes.Contains(processResult.ExitCode) && attempt <= maxRetries)
                {
                    _logger.LogWarning("DCU scan returned retryable exit code {ExitCode}, retrying...", processResult.ExitCode);
                    await Task.Delay(retryDelay * 1000, cancellationToken);
                }
                else
                {
                    result.Success = false;
                    result.Message = $"Scan failed with exit code: {processResult.ExitCode}";
                    _logger.LogError("DCU scan failed with exit code: {ExitCode}", processResult.ExitCode);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DCU scan attempt {Attempt}", attempt);
                if (attempt > maxRetries)
                {
                    result.Success = false;
                    result.Message = $"Scan failed: {ex.Message}";
                    return result;
                }
            }
        }

        return result;
    }

    public async Task<TaskResult> ApplyUpdatesAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Starting Dell Update Application ===");

        var result = new TaskResult
        {
            TaskId = "dcu_update",
            TaskName = "Dell Updates",
            StartTime = DateTime.Now,
            Status = TaskStatus.Running
        };

        var dcuPath = _dcuPath ?? FindDcuPath();
        if (string.IsNullOrEmpty(dcuPath))
        {
            result.Status = TaskStatus.Error;
            result.Message = "Dell Command Update not found";
            result.EndTime = DateTime.Now;
            return result;
        }

        // DCU 5.x exit codes (per working script):
        // 0 = Success, no reboot required
        // 1 = Success, soft reboot required
        // 5 = Success, hard reboot required
        // 500 = No updates available
        // NOTE: Success codes now include 1 and 5 as they indicate updates were applied
        var successCodes = _configuration.GetSection("DellCommandUpdate:ExitCodes:Success").Get<int[]>() ?? new[] { 0, 1, 5 };
        var rebootRequiredCodes = _configuration.GetSection("DellCommandUpdate:ExitCodes:RebootRequired").Get<int[]>() ?? new[] { 1, 5 };
        var noUpdatesCodes = new[] { _configuration.GetValue("DellCommandUpdate:ExitCodes:NoUpdates", 500) };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logPath = Path.Combine(Path.GetTempPath(), $"dcu_update_{timestamp}.log");

        // Build update arguments matching the WORKING script:
        // -forceupdate=enable is CRITICAL - forces updates even if previously declined
        // -reboot=enable allows DCU to handle reboots (we can intercept via exit codes)
        var forceUpdateArg = _configuration.GetValue<string>("DellCommandUpdate:ForceUpdateArgument") ?? "-forceupdate=enable";
        var rebootArg = _configuration.GetValue<string>("DellCommandUpdate:RebootArgument") ?? "-reboot=enable";
        var updateArgs = $"/applyUpdates {forceUpdateArg} {rebootArg} -outputLog=\"{logPath}\"";

        _logger.LogInformation("DCU command: {DcuPath} {Args}", dcuPath, updateArgs);

        try
        {
            progress?.Report("Applying Dell updates (this may take 15-30 minutes)...");
            _logger.LogInformation("Starting Dell update application - this may take a while...");

            // Use a longer timeout for updates - BIOS and firmware updates can take a long time
            var processResult = await _processRunner.RunAsync(
                dcuPath,
                updateArgs,
                timeoutMs: 3600000, // 60 minutes timeout
                cancellationToken: cancellationToken);

            result.ExitCode = processResult.ExitCode;
            result.DetailedOutput = processResult.CombinedOutput;

            _logger.LogInformation("DCU applyUpdates exited with code: {ExitCode}", processResult.ExitCode);
            _logger.LogInformation("DCU stdout:\n{Output}", processResult.StandardOutput);
            if (!string.IsNullOrEmpty(processResult.StandardError))
            {
                _logger.LogWarning("DCU stderr:\n{Error}", processResult.StandardError);
            }

            // Read the detailed log file
            if (File.Exists(logPath))
            {
                var logContent = File.ReadAllText(logPath);
                result.DetailedOutput += Environment.NewLine + "=== DCU Update Log ===" + Environment.NewLine + logContent;
                _logger.LogInformation("=== DCU Update Log ===\n{LogContent}", logContent);

                // Parse log for installed updates
                var installedMatches = System.Text.RegularExpressions.Regex.Matches(
                    logContent,
                    @"(Installed|Applied|Updated|Success)[:\s]+([^\r\n]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (installedMatches.Count > 0)
                {
                    _logger.LogInformation("=== Updates Applied ===");
                    foreach (System.Text.RegularExpressions.Match match in installedMatches)
                    {
                        _logger.LogInformation("  {Status}: {Update}", match.Groups[1].Value, match.Groups[2].Value);
                    }
                }
            }

            if (successCodes.Contains(processResult.ExitCode))
            {
                result.Status = TaskStatus.Success;
                result.Message = "All updates applied successfully";
                _logger.LogInformation("=== Dell updates applied successfully (no reboot needed) ===");
            }
            else if (rebootRequiredCodes.Contains(processResult.ExitCode))
            {
                result.Status = TaskStatus.Success;
                result.RequiresRestart = true;
                result.Message = "Updates applied successfully - RESTART REQUIRED to complete";
                _logger.LogInformation("=== Dell updates applied - RESTART REQUIRED (exit code {ExitCode}) ===", processResult.ExitCode);
                progress?.Report("Updates applied - RESTART REQUIRED to complete installation");
            }
            else if (noUpdatesCodes.Contains(processResult.ExitCode))
            {
                result.Status = TaskStatus.Success;
                result.Message = "No updates needed";
                _logger.LogInformation("Dell updates: no updates needed");
            }
            else
            {
                result.Status = TaskStatus.Error;
                result.Message = $"Update failed with exit code: {processResult.ExitCode}";
                _logger.LogError("Dell update failed with exit code: {ExitCode}", processResult.ExitCode);
            }
        }
        catch (Exception ex)
        {
            result.Status = TaskStatus.Error;
            result.Message = $"Update failed: {ex.Message}";
            _logger.LogError(ex, "Error applying Dell updates");
        }

        result.EndTime = DateTime.Now;
        _logger.LogInformation("Dell update application completed in {Duration}", result.Duration);
        return result;
    }

    public async Task<TaskResult> RunCompleteUpdateAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new TaskResult
        {
            TaskId = "dell_complete",
            TaskName = "Dell Command Update",
            StartTime = DateTime.Now,
            Status = TaskStatus.Running
        };

        // Check if Dell system
        if (!IsDellSystem())
        {
            result.Status = TaskStatus.Skipped;
            result.Message = "Not a Dell system - skipping Dell updates";
            result.EndTime = DateTime.Now;
            _logger.LogInformation("Not a Dell system, skipping Dell updates");
            return result;
        }

        try
        {
            // Install if needed
            var installResult = await InstallAsync(progress, cancellationToken);
            if (installResult.Status == TaskStatus.Error)
            {
                result.Status = TaskStatus.Error;
                result.Message = $"Installation failed: {installResult.Message}";
                result.EndTime = DateTime.Now;
                return result;
            }

            // Configure
            var configResult = await ConfigureAsync(cancellationToken);
            if (configResult.Status == TaskStatus.Error)
            {
                _logger.LogWarning("DCU configuration failed, continuing anyway");
            }

            // Scan
            var scanResult = await ScanForUpdatesAsync(progress, cancellationToken);
            if (!scanResult.Success)
            {
                result.Status = TaskStatus.Error;
                result.Message = $"Scan failed: {scanResult.Message}";
                result.DetailedOutput = scanResult.RawOutput;
                result.EndTime = DateTime.Now;
                return result;
            }

            if (!scanResult.UpdatesAvailable)
            {
                result.Status = TaskStatus.Success;
                result.Message = "No updates available";
                result.EndTime = DateTime.Now;
                return result;
            }

            // Apply updates
            var updateResult = await ApplyUpdatesAsync(progress, cancellationToken);
            result.Status = updateResult.Status;
            result.Message = updateResult.Message;
            result.DetailedOutput = updateResult.DetailedOutput;
            result.RequiresRestart = updateResult.RequiresRestart;
            result.ExitCode = updateResult.ExitCode;
        }
        catch (Exception ex)
        {
            result.Status = TaskStatus.Error;
            result.Message = $"Error: {ex.Message}";
            _logger.LogError(ex, "Error during Dell update workflow");
        }

        result.EndTime = DateTime.Now;
        return result;
    }
}
