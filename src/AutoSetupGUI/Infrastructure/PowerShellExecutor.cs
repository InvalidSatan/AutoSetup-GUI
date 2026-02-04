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
    /// Gets Windows activation status using WMI SoftwareLicensingProduct.
    /// LicenseStatus values: 0=Unlicensed, 1=Licensed, 2=OOBGrace, 3=OOTGrace, 4=NonGenuineGrace, 5=Notification
    /// For KMS/Volume licensing, status 1 = properly activated.
    /// </summary>
    public async Task<(bool IsActivated, string Status)> GetWindowsActivationStatusAsync()
    {
        try
        {
            // More robust query that handles Windows Education, Enterprise, Pro, etc.
            // PartialProductKey being non-null indicates a license key is installed
            // ApplicationId for Windows is 55c92734-d682-4d71-983e-d6ec3f16059f
            var script = @"
                $windowsAppId = '55c92734-d682-4d71-983e-d6ec3f16059f'
                $activation = Get-CimInstance -ClassName SoftwareLicensingProduct -ErrorAction SilentlyContinue |
                    Where-Object {
                        $_.ApplicationId -eq $windowsAppId -and
                        $_.PartialProductKey -ne $null -and
                        $_.PartialProductKey -ne ''
                    } |
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
                    $productName = $activation.Name -replace 'Windows.*,\s*', ''
                    Write-Output ""$($activation.LicenseStatus)|$status|$productName""
                } else {
                    # Fallback: try slmgr approach
                    $slmgrOutput = cscript //nologo ""$env:SystemRoot\System32\slmgr.vbs"" /dli 2>&1 | Out-String
                    if ($slmgrOutput -match 'License Status:\s*Licensed') {
                        Write-Output ""1|Licensed|Windows (slmgr)""
                    } elseif ($slmgrOutput -match 'License Status:\s*(\w+)') {
                        Write-Output ""0|$($Matches[1])|Windows (slmgr)""
                    } else {
                        Write-Output ""0|Unknown|Unable to determine""
                    }
                }
            ";

            var result = await ExecuteScriptAsync(script);

            if (result.Success && result.Output.Count > 0)
            {
                var parts = result.Output[0].Split('|');
                var licenseStatus = int.TryParse(parts[0], out var status) ? status : 0;
                var statusText = parts.Length > 1 ? parts[1] : "Unknown";
                var productName = parts.Length > 2 ? parts[2] : "";

                // Licensed (1) is the only fully activated state
                var isActivated = licenseStatus == 1;
                var fullStatus = string.IsNullOrEmpty(productName) ? statusText : $"{statusText} ({productName})";

                return (isActivated, fullStatus);
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
