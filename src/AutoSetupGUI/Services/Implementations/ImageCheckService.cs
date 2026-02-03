using System.IO;
using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for post-image verification checks.
/// </summary>
public class ImageCheckService : IImageCheckService
{
    private readonly ILogger<ImageCheckService> _logger;
    private readonly IConfiguration _configuration;
    private readonly WmiHelper _wmiHelper;
    private readonly RegistryHelper _registryHelper;
    private readonly PowerShellExecutor _psExecutor;
    private readonly ISCCMService _sccmService;

    public ImageCheckService(
        ILogger<ImageCheckService> logger,
        IConfiguration configuration,
        WmiHelper wmiHelper,
        RegistryHelper registryHelper,
        PowerShellExecutor psExecutor,
        ISCCMService sccmService)
    {
        _logger = logger;
        _configuration = configuration;
        _wmiHelper = wmiHelper;
        _registryHelper = registryHelper;
        _psExecutor = psExecutor;
        _sccmService = sccmService;
    }

    public async Task<ImageCheckResult> RunAllChecksAsync(
        IProgress<ImageCheck>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running all image verification checks...");

        var result = new ImageCheckResult
        {
            CheckedAt = DateTime.Now
        };

        var checks = new List<Func<CancellationToken, Task<ImageCheck>>>
        {
            CheckWindowsActivationAsync,
            CheckDomainJoinAsync,
            CheckSCCMClientAsync,
            CheckDiskSpaceAsync,
            CheckNetworkAsync,
            CheckPendingRebootAsync,
            CheckBitLockerAsync
        };

        foreach (var checkFunc in checks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var check = await checkFunc(cancellationToken);
            result.Checks.Add(check);
            progress?.Report(check);
        }

        _logger.LogInformation("Image checks completed: {Passed}/{Total} passed",
            result.PassedCount, result.TotalCount);

        return result;
    }

    public async Task<ImageCheck> CheckWindowsActivationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking Windows activation...");

        try
        {
            var (isActivated, status) = await _psExecutor.GetWindowsActivationStatusAsync();
            return ImageCheck.WindowsActivation(isActivated, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Windows activation");
            return ImageCheck.WindowsActivation(false, "Error checking activation");
        }
    }

    public async Task<ImageCheck> CheckDomainJoinAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking domain membership...");

        return await Task.Run(() =>
        {
            try
            {
                var (_, _, domain, partOfDomain) = _wmiHelper.GetComputerSystemInfo();
                return ImageCheck.DomainJoin(partOfDomain, domain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking domain membership");
                return ImageCheck.DomainJoin(false, "Error checking domain");
            }
        }, cancellationToken);
    }

    public async Task<ImageCheck> CheckSCCMClientAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking SCCM client health...");

        try
        {
            var health = await _sccmService.CheckHealthAsync(cancellationToken);
            return ImageCheck.SCCMClient(health.OverallHealthy, health.ClientVersion, health.HealthMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking SCCM client");
            return ImageCheck.SCCMClient(false, "", "Error checking SCCM client");
        }
    }

    public async Task<ImageCheck> CheckDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking disk space...");

        return await Task.Run(() =>
        {
            try
            {
                var minSpaceGB = _configuration.GetValue("ImageChecks:MinDiskSpaceGB", 20);

                var disks = _wmiHelper.GetLogicalDisks().ToList();
                var systemDisk = disks.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(d.DriveLetter) &&
                    d.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(systemDisk.DriveLetter))
                {
                    return ImageCheck.DiskSpace(false, 0, minSpaceGB);
                }

                var freeSpaceGB = systemDisk.FreeSpace / (1024L * 1024 * 1024);
                var sufficient = freeSpaceGB >= minSpaceGB;

                return ImageCheck.DiskSpace(sufficient, freeSpaceGB, minSpaceGB);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking disk space");
                return ImageCheck.DiskSpace(false, 0, 20);
            }
        }, cancellationToken);
    }

    public async Task<ImageCheck> CheckNetworkAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking network connectivity...");

        return await Task.Run(() =>
        {
            try
            {
                // Check internet connectivity
                var internetConnected = false;
                try
                {
                    using var ping = new System.Net.NetworkInformation.Ping();
                    var reply = ping.Send("8.8.8.8", 2000);
                    internetConnected = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                }
                catch
                {
                    internetConnected = false;
                }

                // Check P:\ drive availability
                var pDriveAvailable = Directory.Exists(@"P:\");

                return ImageCheck.NetworkConnectivity(internetConnected, pDriveAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking network connectivity");
                return ImageCheck.NetworkConnectivity(false, false);
            }
        }, cancellationToken);
    }

    public async Task<ImageCheck> CheckPendingRebootAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking for pending reboot...");

        return await Task.Run(() =>
        {
            try
            {
                var (isPending, reason) = _registryHelper.CheckPendingReboot();
                return ImageCheck.PendingReboot(isPending, reason);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking pending reboot");
                return ImageCheck.PendingReboot(false, "Error checking");
            }
        }, cancellationToken);
    }

    public async Task<ImageCheck> CheckBitLockerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking BitLocker status...");

        try
        {
            var (isEnabled, status) = await _psExecutor.GetBitLockerStatusAsync();
            return ImageCheck.BitLocker(isEnabled, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking BitLocker");
            return ImageCheck.BitLocker(false, "Error checking BitLocker");
        }
    }
}
