using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Helper class for executing PowerShell commands and scripts.
/// </summary>
public class PowerShellExecutor
{
    private readonly ILogger<PowerShellExecutor> _logger;

    public PowerShellExecutor(ILogger<PowerShellExecutor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes a PowerShell command and returns the results.
    /// </summary>
    public async Task<PowerShellResult> ExecuteCommandAsync(
        string command,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing PowerShell command: {Command}", command);

        var result = new PowerShellResult { Command = command };

        try
        {
            using var runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();

            using var powerShell = System.Management.Automation.PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddCommand(command);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    powerShell.AddParameter(param.Key, param.Value);
                }
            }

            var output = await Task.Run(() =>
            {
                return powerShell.Invoke();
            }, cancellationToken);

            result.Output = output.Select(o => o?.ToString() ?? "").ToList();
            result.Success = !powerShell.HadErrors;

            if (powerShell.HadErrors)
            {
                result.Errors = powerShell.Streams.Error
                    .Select(e => e.Exception?.Message ?? e.ToString())
                    .ToList();
            }

            _logger.LogDebug("PowerShell command completed. Success: {Success}, Errors: {ErrorCount}",
                result.Success, result.Errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Exception = ex;
            result.Errors.Add(ex.Message);

            _logger.LogError(ex, "Error executing PowerShell command: {Command}", command);

            return result;
        }
    }

    /// <summary>
    /// Executes a PowerShell script string.
    /// </summary>
    public async Task<PowerShellResult> ExecuteScriptAsync(
        string script,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing PowerShell script ({Length} chars)", script.Length);

        var result = new PowerShellResult { Command = script };

        try
        {
            using var runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();

            using var powerShell = System.Management.Automation.PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(script);

            var output = await Task.Run(() =>
            {
                return powerShell.Invoke();
            }, cancellationToken);

            result.Output = output.Select(o => o?.ToString() ?? "").ToList();
            result.Success = !powerShell.HadErrors;

            if (powerShell.HadErrors)
            {
                result.Errors = powerShell.Streams.Error
                    .Select(e => e.Exception?.Message ?? e.ToString())
                    .ToList();
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Exception = ex;
            result.Errors.Add(ex.Message);

            _logger.LogError(ex, "Error executing PowerShell script");

            return result;
        }
    }

    /// <summary>
    /// Gets Windows activation status using slmgr.vbs.
    /// </summary>
    public async Task<(bool IsActivated, string Status)> GetWindowsActivationStatusAsync()
    {
        try
        {
            var script = @"
                $activation = Get-CimInstance -ClassName SoftwareLicensingProduct |
                    Where-Object { $_.Name -like '*Windows*' -and $_.LicenseStatus -ne 0 } |
                    Select-Object -First 1

                if ($activation) {
                    $status = switch ($activation.LicenseStatus) {
                        0 { 'Unlicensed' }
                        1 { 'Licensed' }
                        2 { 'OOBGrace' }
                        3 { 'OOTGrace' }
                        4 { 'NonGenuineGrace' }
                        5 { 'Notification' }
                        6 { 'ExtendedGrace' }
                        default { 'Unknown' }
                    }
                    Write-Output ""$($activation.LicenseStatus):$status""
                } else {
                    Write-Output ""0:Unlicensed""
                }
            ";

            var result = await ExecuteScriptAsync(script);

            if (result.Success && result.Output.Count > 0)
            {
                var parts = result.Output[0].Split(':');
                var licenseStatus = int.Parse(parts[0]);
                var status = parts.Length > 1 ? parts[1] : "Unknown";
                return (licenseStatus == 1, status);
            }

            return (false, "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Windows activation");
            return (false, "Error checking activation");
        }
    }

    /// <summary>
    /// Gets BitLocker status using PowerShell.
    /// </summary>
    public async Task<(bool IsEnabled, string Status)> GetBitLockerStatusAsync()
    {
        try
        {
            var script = @"
                $volume = Get-BitLockerVolume -MountPoint 'C:' -ErrorAction SilentlyContinue
                if ($volume) {
                    Write-Output ""$($volume.ProtectionStatus):$($volume.VolumeStatus)""
                } else {
                    Write-Output ""Off:NotAvailable""
                }
            ";

            var result = await ExecuteScriptAsync(script);

            if (result.Success && result.Output.Count > 0)
            {
                var parts = result.Output[0].Split(':');
                var protectionStatus = parts[0];
                var volumeStatus = parts.Length > 1 ? parts[1] : "Unknown";
                var isEnabled = protectionStatus.Equals("On", StringComparison.OrdinalIgnoreCase);
                return (isEnabled, $"{protectionStatus} - {volumeStatus}");
            }

            return (false, "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking BitLocker status");
            return (false, "Error checking BitLocker");
        }
    }
}

/// <summary>
/// Result of executing a PowerShell command or script.
/// </summary>
public class PowerShellResult
{
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<string> Output { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Exception? Exception { get; set; }

    public string CombinedOutput => string.Join(Environment.NewLine, Output);
    public string CombinedErrors => string.Join(Environment.NewLine, Errors);
}
