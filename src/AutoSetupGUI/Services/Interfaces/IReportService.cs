using AutoSetupGUI.Models;

namespace AutoSetupGUI.Services.Interfaces;

/// <summary>
/// Service for generating reports.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates an HTML summary report.
    /// </summary>
    Task<string> GenerateHtmlReportAsync(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        ImageCheckResult? imageChecks = null);

    /// <summary>
    /// Saves the HTML report to a file.
    /// </summary>
    Task SaveHtmlReportAsync(
        string htmlContent,
        string filePath);

    /// <summary>
    /// Generates and saves a PDF report (faster to open than HTML).
    /// </summary>
    Task<string> GeneratePdfReportAsync(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        string filePath,
        ImageCheckResult? imageChecks = null);

    /// <summary>
    /// Generates a text-based summary report.
    /// </summary>
    string GenerateTextReport(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        ImageCheckResult? imageChecks = null);

    /// <summary>
    /// Opens the report file in the default application.
    /// </summary>
    void OpenReport(string filePath);

    /// <summary>
    /// Opens the report in the default browser (for HTML files).
    /// </summary>
    void OpenInBrowser(string filePath);
}
