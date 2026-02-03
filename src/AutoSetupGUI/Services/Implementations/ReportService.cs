using System.Diagnostics;
using System.IO;
using System.Text;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for generating HTML and text reports.
/// </summary>
public class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;
    private readonly IConfiguration _configuration;

    public ReportService(ILogger<ReportService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string> GenerateHtmlReportAsync(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        ImageCheckResult? imageChecks = null)
    {
        var universityBlue = _configuration.GetValue("Branding:UniversityBlue", "#003399");
        var successGreen = _configuration.GetValue("Branding:SuccessGreen", "#107C10");
        var errorRed = _configuration.GetValue("Branding:ErrorRed", "#D13438");
        var warningYellow = _configuration.GetValue("Branding:WarningYellow", "#FF8C00");

        var sb = new StringBuilder();

        sb.AppendLine($@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>University Auto Setup Report</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f5f5; padding: 20px; }}
        .container {{ max-width: 900px; margin: 0 auto; background: white; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background: {universityBlue}; color: white; padding: 24px; border-radius: 8px 8px 0 0; }}
        .header h1 {{ font-size: 24px; margin-bottom: 8px; }}
        .header p {{ opacity: 0.9; }}
        .section {{ padding: 24px; border-bottom: 1px solid #eee; }}
        .section:last-child {{ border-bottom: none; }}
        .section h2 {{ color: {universityBlue}; font-size: 18px; margin-bottom: 16px; }}
        .info-grid {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }}
        .info-item {{ padding: 8px 12px; background: #f8f8f8; border-radius: 4px; }}
        .info-label {{ font-size: 12px; color: #666; }}
        .info-value {{ font-size: 14px; font-weight: 600; color: #333; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th {{ background: #f8f8f8; text-align: left; padding: 12px; font-weight: 600; }}
        td {{ padding: 12px; border-bottom: 1px solid #eee; }}
        .status {{ display: inline-block; padding: 4px 12px; border-radius: 4px; font-size: 12px; font-weight: 600; }}
        .status-success {{ background: #dff6dd; color: {successGreen}; }}
        .status-error {{ background: #fde7e9; color: {errorRed}; }}
        .status-warning {{ background: #fff4ce; color: {warningYellow}; }}
        .status-pending {{ background: #f0f0f0; color: #666; }}
        .footer {{ padding: 16px 24px; background: #f8f8f8; border-radius: 0 0 8px 8px; text-align: center; font-size: 12px; color: #666; }}
        @media print {{
            body {{ background: white; padding: 0; }}
            .container {{ box-shadow: none; }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>University Auto Setup Report</h1>
            <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>

        <div class=""section"">
            <h2>System Information</h2>
            <div class=""info-grid"">
                <div class=""info-item"">
                    <div class=""info-label"">Service Tag</div>
                    <div class=""info-value"">{systemInfo.ServiceTag}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">Computer Name</div>
                    <div class=""info-value"">{systemInfo.ComputerName}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">Manufacturer</div>
                    <div class=""info-value"">{systemInfo.Manufacturer}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">Model</div>
                    <div class=""info-value"">{systemInfo.Model}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">Operating System</div>
                    <div class=""info-value"">{systemInfo.OSName}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">OS Build</div>
                    <div class=""info-value"">{systemInfo.OSBuild}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">Ethernet MAC</div>
                    <div class=""info-value"">{systemInfo.EthernetMac}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">WiFi MAC</div>
                    <div class=""info-value"">{systemInfo.WifiMac}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">Domain</div>
                    <div class=""info-value"">{systemInfo.DomainName}</div>
                </div>
                <div class=""info-item"">
                    <div class=""info-label"">SCCM Client</div>
                    <div class=""info-value"">{(systemInfo.SCCMClientInstalled ? systemInfo.SCCMClientVersion : "Not Installed")}</div>
                </div>
            </div>
        </div>

        <div class=""section"">
            <h2>Task Results</h2>
            <table>
                <thead>
                    <tr>
                        <th>Task</th>
                        <th>Status</th>
                        <th>Message</th>
                        <th>Duration</th>
                    </tr>
                </thead>
                <tbody>");

        foreach (var task in taskResults)
        {
            var statusClass = task.Status switch
            {
                TaskStatus.Success => "status-success",
                TaskStatus.Error => "status-error",
                TaskStatus.Warning => "status-warning",
                _ => "status-pending"
            };

            sb.AppendLine($@"
                    <tr>
                        <td>{task.TaskName}</td>
                        <td><span class=""status {statusClass}"">{task.Status}</span></td>
                        <td>{task.Message}</td>
                        <td>{task.DurationFormatted}</td>
                    </tr>");
        }

        sb.AppendLine(@"
                </tbody>
            </table>
        </div>");

        if (imageChecks != null)
        {
            sb.AppendLine($@"
        <div class=""section"">
            <h2>Image Verification ({imageChecks.PassedCount}/{imageChecks.TotalCount} Passed)</h2>
            <table>
                <thead>
                    <tr>
                        <th>Check</th>
                        <th>Status</th>
                        <th>Details</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var check in imageChecks.Checks)
            {
                var statusClass = check.Passed ? "status-success" : "status-error";
                var statusText = check.Passed ? "Pass" : "Fail";

                sb.AppendLine($@"
                    <tr>
                        <td>{check.Name}</td>
                        <td><span class=""status {statusClass}"">{statusText}</span></td>
                        <td>{check.Status}</td>
                    </tr>");
            }

            sb.AppendLine(@"
                </tbody>
            </table>
        </div>");
        }

        sb.AppendLine($@"
        <div class=""footer"">
            University Auto Setup v3.0 | Appalachian State University<br>
            Contact: {_configuration.GetValue("Branding:ContactName", "IT Support")} ({_configuration.GetValue("Branding:ContactEmail", "support@appstate.edu")})
        </div>
    </div>
</body>
</html>");

        return await Task.FromResult(sb.ToString());
    }

    public async Task SaveHtmlReportAsync(string htmlContent, string filePath)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, htmlContent);
            _logger.LogInformation("HTML report saved to: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving HTML report to: {FilePath}", filePath);
            throw;
        }
    }

    public string GenerateTextReport(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        ImageCheckResult? imageChecks = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("                      UNIVERSITY AUTO SETUP REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("SYSTEM INFORMATION");
        sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");
        sb.AppendLine($"  Service Tag:     {systemInfo.ServiceTag}");
        sb.AppendLine($"  Computer Name:   {systemInfo.ComputerName}");
        sb.AppendLine($"  Manufacturer:    {systemInfo.Manufacturer}");
        sb.AppendLine($"  Model:           {systemInfo.Model}");
        sb.AppendLine($"  OS:              {systemInfo.OSName} ({systemInfo.OSBuild})");
        sb.AppendLine($"  Ethernet MAC:    {systemInfo.EthernetMac}");
        sb.AppendLine($"  WiFi MAC:        {systemInfo.WifiMac}");
        sb.AppendLine($"  Domain:          {systemInfo.DomainName}");
        sb.AppendLine();

        sb.AppendLine("TASK RESULTS");
        sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");

        foreach (var task in taskResults)
        {
            var statusSymbol = task.Status switch
            {
                TaskStatus.Success => "[✓]",
                TaskStatus.Error => "[✗]",
                TaskStatus.Warning => "[!]",
                _ => "[-]"
            };

            sb.AppendLine($"  {statusSymbol} {task.TaskName}: {task.Status} ({task.DurationFormatted})");
            if (!string.IsNullOrEmpty(task.Message))
            {
                sb.AppendLine($"      {task.Message}");
            }
        }

        if (imageChecks != null)
        {
            sb.AppendLine();
            sb.AppendLine($"IMAGE VERIFICATION ({imageChecks.PassedCount}/{imageChecks.TotalCount} Passed)");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────────────");

            foreach (var check in imageChecks.Checks)
            {
                var statusSymbol = check.Passed ? "[✓]" : "[✗]";
                sb.AppendLine($"  {statusSymbol} {check.Name}: {check.Status}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    public void OpenInBrowser(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening report in browser: {FilePath}", filePath);
        }
    }
}
