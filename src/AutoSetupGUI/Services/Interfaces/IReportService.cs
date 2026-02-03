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
    /// Generates a text-based summary report.
    /// </summary>
    string GenerateTextReport(
        SystemInfo systemInfo,
        IEnumerable<TaskResult> taskResults,
        ImageCheckResult? imageChecks = null);

    /// <summary>
    /// Opens the report in the default browser.
    /// </summary>
    void OpenInBrowser(string filePath);
}
