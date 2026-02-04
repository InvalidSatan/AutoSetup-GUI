using System.IO;
using System.Management;
using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for SCCM client operations.
/// </summary>
public class SCCMService : ISCCMService
{
    private readonly ILogger<SCCMService> _logger;
    private readonly IConfiguration _configuration;
    private readonly WmiHelper _wmiHelper;
    private readonly RegistryHelper _registryHelper;
    private readonly ProcessRunner _processRunner;

    private const string SCCM_WMI_NAMESPACE = "root\\ccm";
    private const string SCCM_SERVICE_NAME = "CCMExec";
    private const int ACTION_TIMEOUT_MS = 120000;

    public SCCMService(
        ILogger<SCCMService> logger,
        IConfiguration configuration,
        WmiHelper wmiHelper,
        RegistryHelper registryHelper,
        ProcessRunner processRunner)
    {
        _logger = logger;
        _configuration = configuration;
        _wmiHelper = wmiHelper;
        _registryHelper = registryHelper;
        _processRunner = processRunner;
    }

    public async Task<SCCMClientHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking SCCM client health...");

        var health = new SCCMClientHealth();

        await Task.Run(() =>
        {
            // Check if service exists and is running
            try
            {
                using var serviceController = new System.ServiceProcess.ServiceController(SCCM_SERVICE_NAME);
                health.IsInstalled = true;
                health.ServiceStatus = serviceController.Status.ToString();
                health.IsServiceRunning = serviceController.Status == System.ServiceProcess.ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException)
            {
                health.IsInstalled = false;
                health.ServiceStatus = "Not Installed";
                health.HealthMessage = "SCCM Client service (CCMExec) not found";
                return;
            }

            // Check WMI accessibility
            try
            {
                health.IsWmiAccessible = _wmiHelper.IsNamespaceAccessible(SCCM_WMI_NAMESPACE);

                if (health.IsWmiAccessible)
                {
                    // Try to get client info from WMI
                    foreach (var obj in _wmiHelper.Query("SMS_Client", SCCM_WMI_NAMESPACE))
                    {
                        using (obj)
                        {
                            health.ClientVersion = obj["ClientVersion"]?.ToString() ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                health.IsWmiAccessible = false;
                _logger.LogWarning(ex, "Error accessing SCCM WMI namespace");
            }

            // Get site code and MP from registry
            var (siteCode, mp, version) = _registryHelper.GetSCCMInfo();
            health.SiteCode = siteCode;
            health.ManagementPoint = mp;

            if (!string.IsNullOrEmpty(version) && string.IsNullOrEmpty(health.ClientVersion))
            {
                health.ClientVersion = version;
            }

            // Build health message
            if (health.OverallHealthy)
            {
                health.HealthMessage = $"SCCM Client is healthy (v{health.ClientVersion})";
            }
            else if (!health.IsInstalled)
            {
                health.HealthMessage = "SCCM Client is not installed";
            }
            else if (!health.IsServiceRunning)
            {
                health.HealthMessage = $"SCCM Client service is not running (Status: {health.ServiceStatus})";
            }
            else if (!health.IsWmiAccessible)
            {
                health.HealthMessage = "SCCM Client WMI namespace is not accessible";
            }

        }, cancellationToken);

        _logger.LogInformation("SCCM client health check completed: {Healthy}", health.OverallHealthy);
        return health;
    }

    public async Task<IEnumerable<SCCMActionResult>> RunAllActionsAsync(
        IProgress<SCCMActionResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running all SCCM client actions...");

        var actions = GetConfiguredActions();
        var results = new List<SCCMActionResult>();

        foreach (var action in actions)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Report that the action is starting
            var startingResult = new SCCMActionResult
            {
                Action = action,
                Status = TaskStatus.Running,
                ExecutedAt = DateTime.Now,
                Message = "Running..."
            };
            progress?.Report(startingResult);

            var result = await RunActionAsync(action, cancellationToken);
            results.Add(result);
            progress?.Report(result);

            // Small delay between actions to avoid overwhelming the system
            await Task.Delay(2000, cancellationToken);
        }

        var successCount = results.Count(r => r.Status == TaskStatus.Success);
        _logger.LogInformation("Completed {SuccessCount}/{TotalCount} SCCM actions successfully",
            successCount, results.Count);

        return results;
    }

    public async Task<SCCMActionResult> RunActionAsync(
        SCCMAction action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running SCCM action: {ActionName}", action.Name);

        var result = new SCCMActionResult
        {
            Action = action,
            Status = TaskStatus.Running,
            ExecutedAt = DateTime.Now
        };

        var startTime = DateTime.Now;

        try
        {
            // Ensure service is running
            var isRunning = await EnsureServiceRunningAsync(cancellationToken);
            if (!isRunning)
            {
                result.Status = TaskStatus.Error;
                result.Message = "SCCM service is not running";
                result.Duration = DateTime.Now - startTime;
                return result;
            }

            // Execute the action via WMI
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ACTION_TIMEOUT_MS);

            var invokeResult = await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope(SCCM_WMI_NAMESPACE);
                    scope.Connect();

                    using var clientClass = new ManagementClass(scope, new ManagementPath("SMS_Client"), null);
                    using var inParams = clientClass.GetMethodParameters("TriggerSchedule");
                    inParams["sScheduleID"] = action.ScheduleId;

                    using var outParams = clientClass.InvokeMethod("TriggerSchedule", inParams, null);
                    return outParams?["ReturnValue"];
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking SCCM action: {ActionName}", action.Name);
                    throw;
                }
            }, cts.Token);

            result.Duration = DateTime.Now - startTime;

            // Check return value
            if (invokeResult == null || Convert.ToUInt32(invokeResult) == 0)
            {
                result.Status = TaskStatus.Success;
                result.Message = "Action triggered successfully";
                result.ReturnValue = invokeResult != null ? Convert.ToUInt32(invokeResult) : 0;
                _logger.LogInformation("SCCM action {ActionName} completed successfully", action.Name);
            }
            else
            {
                result.Status = TaskStatus.Warning;
                result.Message = $"Action returned code: {invokeResult}";
                result.ReturnValue = Convert.ToUInt32(invokeResult);
                _logger.LogWarning("SCCM action {ActionName} returned code: {ReturnValue}", action.Name, invokeResult);
            }
        }
        catch (OperationCanceledException)
        {
            result.Status = TaskStatus.Error;
            result.Message = "Action timed out after 120 seconds";
            result.Duration = DateTime.Now - startTime;
            _logger.LogWarning("SCCM action {ActionName} timed out", action.Name);
        }
        catch (Exception ex)
        {
            result.Status = TaskStatus.Error;
            result.Message = ex.Message;
            result.Duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error running SCCM action: {ActionName}", action.Name);
        }

        return result;
    }

    public async Task<bool> RepairClientAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to repair SCCM client...");

        try
        {
            var ccmsetupPath = @"C:\Windows\ccmsetup\ccmsetup.exe";

            if (!File.Exists(ccmsetupPath))
            {
                _logger.LogError("SCCM client repair failed - ccmsetup.exe not found");
                return false;
            }

            // Get site info from registry for reinstall
            var (siteCode, mp, _) = _registryHelper.GetSCCMInfo();

            var args = string.IsNullOrEmpty(mp)
                ? "/logon"
                : $"/mp:{mp} /logon SMSSITECODE={siteCode}";

            var result = await _processRunner.RunAsync(ccmsetupPath, args, timeoutMs: 300000, cancellationToken: cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("SCCM client repair initiated successfully");

                // Wait for service to start
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(15000, cancellationToken);

                    var health = await CheckHealthAsync(cancellationToken);
                    if (health.OverallHealthy)
                    {
                        _logger.LogInformation("SCCM client repair completed successfully");
                        return true;
                    }
                }

                _logger.LogWarning("SCCM client repair initiated but service not yet healthy");
                return false;
            }
            else
            {
                _logger.LogError("SCCM client repair failed with exit code: {ExitCode}", result.ExitCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error repairing SCCM client");
            return false;
        }
    }

    public IReadOnlyList<SCCMAction> GetConfiguredActions()
    {
        var configuredActions = _configuration.GetSection("SCCM:Actions").Get<SCCMActionConfig[]>();

        if (configuredActions == null || configuredActions.Length == 0)
        {
            return SCCMAction.DefaultActions.ToList();
        }

        return configuredActions.Select(c => new SCCMAction
        {
            Name = c.Name,
            ScheduleId = c.ID
        }).ToList();
    }

    private async Task<bool> EnsureServiceRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var serviceController = new System.ServiceProcess.ServiceController(SCCM_SERVICE_NAME);

            if (serviceController.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                return true;

            _logger.LogInformation("Starting SCCM service...");
            serviceController.Start();

            // Wait for service to start
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(2000, cancellationToken);
                serviceController.Refresh();

                if (serviceController.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                {
                    _logger.LogInformation("SCCM service started successfully");
                    return true;
                }
            }

            _logger.LogWarning("SCCM service failed to start within timeout");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring SCCM service is running");
            return false;
        }
    }
}
