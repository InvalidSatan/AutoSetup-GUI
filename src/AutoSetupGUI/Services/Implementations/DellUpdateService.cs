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
        var (manufacturer, _, _, _) = _wmiHelper.GetComputerSystemInfo();
        return manufacturer.Contains("Dell", StringComparison.OrdinalIgnoreCase);
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

        var maxRetries = _configuration.GetValue("DellCommandUpdate:MaxRetries", 3);
        var retryDelay = _configuration.GetValue("DellCommandUpdate:RetryDelaySeconds", 10);
        var retryableCodes = _configuration.GetSection("DellCommandUpdate:ExitCodes:Retryable").Get<int[]>() ?? new[] { 5, 7, 8 };
        var noUpdatesCode = _configuration.GetValue("DellCommandUpdate:ExitCodes:NoUpdates", 1);

        var logPath = Path.Combine(Path.GetTempPath(), $"dcu_scan_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var scanArgs = $"/scan -outputLog=\"{logPath}\"";

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

                if (File.Exists(logPath))
                {
                    result.RawOutput += Environment.NewLine + File.ReadAllText(logPath);
                }

                if (processResult.ExitCode == 0)
                {
                    result.Success = true;
                    result.UpdatesAvailable = true;
                    result.Message = "Updates available";
                    _logger.LogInformation("DCU scan completed - updates available");
                    return result;
                }
                else if (processResult.ExitCode == noUpdatesCode)
                {
                    result.Success = true;
                    result.UpdatesAvailable = false;
                    result.Message = "No updates available";
                    _logger.LogInformation("DCU scan completed - no updates available");
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
        _logger.LogInformation("Applying Dell updates...");

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

        var successCodes = _configuration.GetSection("DellCommandUpdate:ExitCodes:Success").Get<int[]>() ?? new[] { 0, 2, 3 };

        var logPath = Path.Combine(Path.GetTempPath(), $"dcu_update_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var updateArgs = $"/applyUpdates -forceUpdate -reboot=disable -outputLog=\"{logPath}\"";

        try
        {
            progress?.Report("Applying Dell updates (this may take a while)...");

            var processResult = await _processRunner.RunAsync(dcuPath, updateArgs, timeoutMs: 1800000, cancellationToken: cancellationToken);

            result.ExitCode = processResult.ExitCode;
            result.DetailedOutput = processResult.CombinedOutput;

            if (File.Exists(logPath))
            {
                result.DetailedOutput += Environment.NewLine + File.ReadAllText(logPath);
            }

            if (successCodes.Contains(processResult.ExitCode))
            {
                result.Status = TaskStatus.Success;

                if (processResult.ExitCode == 2 || processResult.ExitCode == 3)
                {
                    result.RequiresRestart = true;
                    result.Message = "Updates applied successfully - restart required";
                    _logger.LogInformation("Dell updates applied - restart required");
                }
                else
                {
                    result.Message = "Updates applied successfully";
                    _logger.LogInformation("Dell updates applied successfully");
                }
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
