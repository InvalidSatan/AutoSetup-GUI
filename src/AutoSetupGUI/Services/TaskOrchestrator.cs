using System.IO;
using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Logging;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;
using LogLevel = AutoSetupGUI.Models.LogLevel;

namespace AutoSetupGUI.Services;

/// <summary>
/// Orchestrates the execution of all setup tasks.
/// </summary>
public class TaskOrchestrator
{
    private readonly ILogger<TaskOrchestrator> _logger;
    private readonly ILoggingService _loggingService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IGroupPolicyService _groupPolicyService;
    private readonly ISCCMService _sccmService;
    private readonly IDellUpdateService _dellUpdateService;
    private readonly IImageCheckService _imageCheckService;
    private readonly IReportService _reportService;
    private readonly SleepPrevention _sleepPrevention;

    public event EventHandler<TaskProgressEventArgs>? TaskProgressChanged;
    public event EventHandler<string>? StatusChanged;

    public TaskOrchestrator(
        ILogger<TaskOrchestrator> logger,
        ILoggingService loggingService,
        ISystemInfoService systemInfoService,
        IGroupPolicyService groupPolicyService,
        ISCCMService sccmService,
        IDellUpdateService dellUpdateService,
        IImageCheckService imageCheckService,
        IReportService reportService,
        SleepPrevention sleepPrevention)
    {
        _logger = logger;
        _loggingService = loggingService;
        _systemInfoService = systemInfoService;
        _groupPolicyService = groupPolicyService;
        _sccmService = sccmService;
        _dellUpdateService = dellUpdateService;
        _imageCheckService = imageCheckService;
        _reportService = reportService;
        _sleepPrevention = sleepPrevention;
    }

    /// <summary>
    /// Runs all selected tasks in sequence.
    /// </summary>
    public async Task<SetupResults> RunAllTasksAsync(
        SetupOptions options,
        CancellationToken cancellationToken = default)
    {
        var results = new SetupResults
        {
            StartTime = DateTime.Now
        };

        try
        {
            // Initialize logging
            var serviceTag = _systemInfoService.GetServiceTag();
            var computerName = _systemInfoService.GetComputerName();
            await _loggingService.InitializeAsync(serviceTag, computerName);

            _loggingService.LogHeader("University Auto Setup v2.0");

            // Prevent sleep during setup
            _sleepPrevention.PreventSleep();
            ReportStatus("Setup started...");

            // Collect system information
            ReportProgress("Collecting system information...", 5);
            results.SystemInfo = await _systemInfoService.CollectSystemInfoAsync(cancellationToken);
            _loggingService.LogSection("System Information");
            _loggingService.LogInfo(results.SystemInfo.ToDetailedFormat());

            // Run Group Policy Update
            if (options.RunGroupPolicy)
            {
                ReportProgress("Updating Group Policy...", 10);
                _loggingService.LogSection("Group Policy Update");

                var gpResult = await _groupPolicyService.UpdateGroupPolicyAsync(
                    new Progress<string>(msg => ReportStatus(msg)),
                    cancellationToken);

                results.TaskResults.Add(gpResult);
                LogTaskResult(gpResult);
                ReportProgress("Group Policy update completed", 25);
            }

            // Run SCCM Actions
            if (options.RunSCCMActions)
            {
                ReportProgress("Running SCCM client actions...", 30);
                _loggingService.LogSection("SCCM Client Actions");

                var sccmResults = await _sccmService.RunAllActionsAsync(
                    new Progress<SCCMActionResult>(r =>
                    {
                        _loggingService.Log($"{r.Action.Name}: {r.Status} - {r.Message}",
                            r.Status == TaskStatus.Success ? LogLevel.Success : LogLevel.Warning);
                    }),
                    cancellationToken);

                var sccmTaskResult = new TaskResult
                {
                    TaskId = "sccm_actions",
                    TaskName = "SCCM Client Actions",
                    StartTime = results.StartTime,
                    EndTime = DateTime.Now,
                    Status = sccmResults.All(r => r.Status == TaskStatus.Success) ? TaskStatus.Success :
                             sccmResults.Any(r => r.Status == TaskStatus.Error) ? TaskStatus.Warning : TaskStatus.Success,
                    Message = $"{sccmResults.Count(r => r.Status == TaskStatus.Success)}/{sccmResults.Count()} actions completed successfully",
                    SubTaskResults = sccmResults.Select(r => new SubTaskResult
                    {
                        Name = r.Action.Name,
                        Status = r.Status,
                        Message = r.Message,
                        CompletedAt = r.ExecutedAt
                    }).ToList()
                };

                results.TaskResults.Add(sccmTaskResult);
                LogTaskResult(sccmTaskResult);
                ReportProgress("SCCM actions completed", 50);
            }

            // Run Dell Command Update
            if (options.RunDellUpdates)
            {
                ReportProgress("Checking Dell updates...", 55);
                _loggingService.LogSection("Dell Command Update");

                var dellResult = await _dellUpdateService.RunCompleteUpdateAsync(
                    new Progress<string>(msg => ReportStatus(msg)),
                    cancellationToken);

                results.TaskResults.Add(dellResult);
                results.RequiresRestart |= dellResult.RequiresRestart;
                LogTaskResult(dellResult);
                ReportProgress("Dell updates completed", 85);
            }

            // Run Image Checks
            if (options.RunImageChecks)
            {
                ReportProgress("Running image verification checks...", 90);
                _loggingService.LogSection("Image Verification Checks");

                results.ImageChecks = await _imageCheckService.RunAllChecksAsync(
                    new Progress<ImageCheck>(check =>
                    {
                        _loggingService.Log($"{check.Name}: {(check.Passed ? "PASS" : "FAIL")} - {check.Status}",
                            check.Passed ? LogLevel.Success : LogLevel.Warning);
                    }),
                    cancellationToken);
            }

            // Generate report
            ReportProgress("Generating report...", 95);
            _loggingService.LogSection("Summary");

            var htmlReport = await _reportService.GenerateHtmlReportAsync(
                results.SystemInfo,
                results.TaskResults,
                results.ImageChecks);

            var reportPath = Path.Combine(
                _loggingService.GetLocalLogPath(),
                $"UniversityAutoSetup_Summary_{DateTime.Now:yyyyMMdd_HHmmss}.html");

            await _reportService.SaveHtmlReportAsync(htmlReport, reportPath);
            results.ReportPath = reportPath;

            // Copy to network if available
            var networkPath = _loggingService.GetNetworkLogPath();
            if (!string.IsNullOrEmpty(networkPath))
            {
                try
                {
                    var networkReportPath = Path.Combine(networkPath, Path.GetFileName(reportPath));
                    await _reportService.SaveHtmlReportAsync(htmlReport, networkReportPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save report to network path");
                }
            }

            // Final status
            results.EndTime = DateTime.Now;
            results.OverallSuccess = results.TaskResults.All(r => r.Status != TaskStatus.Error);

            _loggingService.LogInfo($"Setup completed in {results.Duration?.TotalMinutes:F1} minutes");
            _loggingService.Log(
                results.OverallSuccess ? "All tasks completed successfully" : "Some tasks failed",
                results.OverallSuccess ? LogLevel.Success : LogLevel.Warning);

            ReportProgress("Setup completed!", 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during setup execution");
            _loggingService.LogError("Setup failed", exception: ex);
            results.OverallSuccess = false;
        }
        finally
        {
            // Restore sleep settings
            _sleepPrevention.AllowSleep();
            await _loggingService.FlushAsync();
        }

        return results;
    }

    private void ReportProgress(string message, int percentage)
    {
        TaskProgressChanged?.Invoke(this, new TaskProgressEventArgs(message, percentage));
        _loggingService.LogInfo(message);
    }

    private void ReportStatus(string status)
    {
        StatusChanged?.Invoke(this, status);
    }

    private void LogTaskResult(TaskResult result)
    {
        var level = result.Status switch
        {
            TaskStatus.Success => LogLevel.Success,
            TaskStatus.Warning => LogLevel.Warning,
            TaskStatus.Error => LogLevel.Error,
            _ => LogLevel.Info
        };

        _loggingService.Log($"{result.TaskName}: {result.Status} - {result.Message}", level);
    }
}

/// <summary>
/// Options for running the setup.
/// </summary>
public class SetupOptions
{
    public bool RunGroupPolicy { get; set; } = true;
    public bool RunSCCMActions { get; set; } = true;
    public bool RunDellUpdates { get; set; } = true;
    public bool RunImageChecks { get; set; } = true;
    public bool OfferRestart { get; set; } = true;
}

/// <summary>
/// Results of the complete setup process.
/// </summary>
public class SetupResults
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    public SystemInfo SystemInfo { get; set; } = new();
    public List<TaskResult> TaskResults { get; set; } = new();
    public ImageCheckResult? ImageChecks { get; set; }
    public bool RequiresRestart { get; set; }
    public bool OverallSuccess { get; set; }
    public string? ReportPath { get; set; }
}

/// <summary>
/// Event arguments for task progress updates.
/// </summary>
public class TaskProgressEventArgs : EventArgs
{
    public string Message { get; }
    public int Percentage { get; }

    public TaskProgressEventArgs(string message, int percentage)
    {
        Message = message;
        Percentage = percentage;
    }
}
