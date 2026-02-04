using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using AutoSetupGUI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for generating HTML, PDF, and text reports.
/// </summary>
public class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;
    private readonly IConfiguration _configuration;

    // App State brand colors
    private readonly string _primaryColorHex;
    private readonly string _accentColorHex;
    private readonly string _successGreenHex;
    private readonly string _errorRedHex;
    private readonly string _warningYellowHex;

    public ReportService(ILogger<ReportService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Load brand colors from configuration
        _primaryColorHex = _configuration.GetValue("Branding:PrimaryColor", "#010101")!;
        _accentColorHex = _configuration.GetValue("Branding:AccentColor", "#FFCC00")!;
        _successGreenHex = _configuration.GetValue("Branding:SuccessGreen", "#69AA61")!;
        _errorRedHex = _configuration.GetValue("Branding:ErrorRed", "#C6602A")!;
        _warningYellowHex = _configuration.GetValue("Branding:WarningYellow", "#D7A527")!;

        // Configure QuestPDF license (Community license for open source/internal use)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Generates and saves a PDF report.
    /// </summary>
    public async Task<string> GeneratePdfReportAsync(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        string filePath,
        ImageCheckResult? imageChecks = null)
    {
        _logger.LogInformation("Generating PDF report: {FilePath}", filePath);

        try
        {
            await Task.Run(() =>
            {
                var taskList = taskResults.ToList();
                var document = CreatePdfDocument(systemInfo, taskList, imageChecks);
                document.GeneratePdf(filePath);
            });

            _logger.LogInformation("PDF report saved to: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF report: {FilePath}", filePath);
            throw;
        }
    }

    private Document CreatePdfDocument(SystemInfo systemInfo, List<TaskResult> taskResults, ImageCheckResult? imageChecks)
    {
        // Convert hex colors to QuestPDF colors
        var primaryColor = Color.FromHex(_primaryColorHex);
        var accentColor = Color.FromHex(_accentColorHex);
        var successColor = Color.FromHex(_successGreenHex);
        var errorColor = Color.FromHex(_errorRedHex);
        var warningColor = Color.FromHex(_warningYellowHex);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                // Header
                page.Header().Element(header =>
                {
                    header.Background(primaryColor).Padding(20).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("University Auto Setup Report")
                                .FontSize(20).Bold().FontColor(Colors.White);
                            col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                .FontSize(10).FontColor(Colors.White).Light();
                        });

                        row.ConstantItem(100).AlignRight().AlignMiddle()
                            .Text("v2.0").FontSize(12).FontColor(accentColor).Bold();
                    });
                });

                // Content
                page.Content().PaddingVertical(10).Column(col =>
                {
                    // System Information Section
                    col.Item().Element(e => SectionHeader(e, "System Information", primaryColor));
                    col.Item().PaddingBottom(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        // Row 1
                        InfoCell(table, "Service Tag", systemInfo.ServiceTag);
                        InfoCell(table, "Computer Name", systemInfo.ComputerName);

                        // Row 2
                        InfoCell(table, "Manufacturer", systemInfo.Manufacturer);
                        InfoCell(table, "Model", systemInfo.Model);

                        // Row 3
                        InfoCell(table, "Operating System", systemInfo.OSName ?? "Unknown");
                        InfoCell(table, "OS Build", systemInfo.OSBuild ?? "Unknown");

                        // Row 4
                        InfoCell(table, "Ethernet MAC", systemInfo.EthernetMac ?? "Not found");
                        InfoCell(table, "WiFi MAC", systemInfo.WifiMac ?? "Not found");

                        // Row 5
                        InfoCell(table, "Domain", systemInfo.DomainName ?? "Unknown");
                        InfoCell(table, "SCCM Client", systemInfo.SCCMClientInstalled ? systemInfo.SCCMClientVersion : "Not Installed");
                    });

                    // Task Results Section
                    col.Item().Element(e => SectionHeader(e, "Task Results", primaryColor));
                    col.Item().PaddingBottom(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Task").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Status").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Message").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                .Text("Duration").Bold();
                        });

                        foreach (var task in taskResults)
                        {
                            var (statusBg, statusFg) = GetStatusColors(task.Status, successColor, errorColor, warningColor);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                .Text(task.TaskName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                .Element(e => StatusBadge(e, task.Status.ToString(), statusBg, statusFg));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                .Text(task.Message ?? "");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                .Text(task.DurationFormatted ?? "");
                        }
                    });

                    // Image Verification Section (if present)
                    if (imageChecks != null)
                    {
                        col.Item().Element(e => SectionHeader(e, $"Image Verification ({imageChecks.PassedCount}/{imageChecks.TotalCount} Passed)", primaryColor));
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(4);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Check").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Status").Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Details").Bold();
                            });

                            foreach (var check in imageChecks.Checks)
                            {
                                var (statusBg, statusFg) = check.Passed
                                    ? (Color.FromHex("#dff6dd"), successColor)
                                    : (Color.FromHex("#fde7e9"), errorColor);

                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text(check.Name);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Element(e => StatusBadge(e, check.Passed ? "Pass" : "Fail", statusBg, statusFg));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text(check.Status ?? "");
                            }
                        });
                    }
                });

                // Footer
                page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("University Auto Setup v2.0 | Appalachian State University").FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span($"Contact: {_configuration.GetValue("Branding:ContactName", "IT Support")}").FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        });
    }

    private static void SectionHeader(IContainer container, string title, Color color)
    {
        container.PaddingTop(10).PaddingBottom(8).BorderBottom(2).BorderColor(color)
            .Text(title).FontSize(14).Bold().FontColor(color);
    }

    private static void InfoCell(TableDescriptor table, string label, string? value)
    {
        table.Cell().Padding(6).Column(col =>
        {
            col.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Medium);
            col.Item().Text(value ?? "Unknown").FontSize(10).Bold();
        });
    }

    private static void StatusBadge(IContainer container, string text, Color background, Color foreground)
    {
        container.Background(background).Padding(4).AlignCenter()
            .Text(text).FontSize(9).Bold().FontColor(foreground);
    }

    private static (Color background, Color foreground) GetStatusColors(TaskStatus status, Color success, Color error, Color warning)
    {
        return status switch
        {
            TaskStatus.Success => (Color.FromHex("#dff6dd"), success),
            TaskStatus.Error => (Color.FromHex("#fde7e9"), error),
            TaskStatus.Warning => (Color.FromHex("#fff4ce"), warning),
            _ => (Color.FromHex("#f0f0f0"), Colors.Grey.Medium)
        };
    }

    public async Task<string> GenerateHtmlReportAsync(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        ImageCheckResult? imageChecks = null)
    {
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
        .header {{ background: {_primaryColorHex}; color: white; padding: 24px; border-radius: 8px 8px 0 0; }}
        .header h1 {{ font-size: 24px; margin-bottom: 8px; }}
        .header p {{ opacity: 0.9; }}
        .section {{ padding: 24px; border-bottom: 1px solid #eee; }}
        .section:last-child {{ border-bottom: none; }}
        .section h2 {{ color: {_primaryColorHex}; font-size: 18px; margin-bottom: 16px; }}
        .info-grid {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }}
        .info-item {{ padding: 8px 12px; background: #f8f8f8; border-radius: 4px; }}
        .info-label {{ font-size: 12px; color: #666; }}
        .info-value {{ font-size: 14px; font-weight: 600; color: #333; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th {{ background: #f8f8f8; text-align: left; padding: 12px; font-weight: 600; }}
        td {{ padding: 12px; border-bottom: 1px solid #eee; }}
        .status {{ display: inline-block; padding: 4px 12px; border-radius: 4px; font-size: 12px; font-weight: 600; }}
        .status-success {{ background: #dff6dd; color: {_successGreenHex}; }}
        .status-error {{ background: #fde7e9; color: {_errorRedHex}; }}
        .status-warning {{ background: #fff4ce; color: {_warningYellowHex}; }}
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
            University Auto Setup v2.0 | Appalachian State University<br>
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

    /// <summary>
    /// Opens the report file in the default application (works for PDF, HTML, etc.)
    /// </summary>
    public void OpenReport(string filePath)
    {
        try
        {
            _logger.LogInformation("Opening report: {FilePath}", filePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening report: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Opens the report in the default browser (for HTML files).
    /// </summary>
    public void OpenInBrowser(string filePath)
    {
        OpenReport(filePath);
    }

    /// <summary>
    /// Shows the PDF report in a built-in viewer window (avoids Edge first-run experience).
    /// </summary>
    public void ShowReportViewer(string filePath, bool requiresRestart = false)
    {
        try
        {
            _logger.LogInformation("Opening report viewer for: {FilePath}, RequiresRestart: {RequiresRestart}",
                filePath, requiresRestart);

            // Must run on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var viewer = new PdfViewerWindow(filePath, requiresRestart);
                viewer.Show();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening report viewer: {FilePath}", filePath);
            // Fall back to shell open
            OpenReport(filePath);
        }
    }
}
