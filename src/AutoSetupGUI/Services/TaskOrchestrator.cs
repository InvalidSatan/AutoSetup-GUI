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
    public event EventHandler<IndividualTaskProgressEventArgs>? IndividualTaskProgress;
    public event EventHandler<SCCMActionResult>? SCCMActionProgress;

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

                // Signal task started
                ReportIndividualTaskProgress("group_policy", "Group Policy Update", TaskStatus.Running);

                var gpResult = await _groupPolicyService.UpdateGroupPolicyAsync(
                    new Progress<string>(msg => ReportStatus(msg)),
                    cancellationToken);

                results.TaskResults.Add(gpResult);
                LogTaskResult(gpResult);

                // Signal task completed
                ReportIndividualTaskProgress("group_policy", "Group Policy Update", gpResult.Status, gpResult.Message, gpResult.Duration);

                ReportProgress("Group Policy update completed", 25);
            }

            // Run SCCM Actions
            if (options.RunSCCMActions)
            {
                ReportProgress("Running SCCM client actions...", 30);
                _loggingService.LogSection("SCCM Client Actions");

                // Signal task started
                ReportIndividualTaskProgress("sccm_actions", "SCCM Client Actions", TaskStatus.Running);

                var sccmResults = await _sccmService.RunAllActionsAsync(
                    new Progress<SCCMActionResult>(r =>
                    {
                        _loggingService.Log($"{r.Action.Name}: {r.Status} - {r.Message}",
                            r.Status == TaskStatus.Success ? LogLevel.Success : LogLevel.Warning);
                        // Fire event for individual SCCM action progress
                        SCCMActionProgress?.Invoke(this, r);
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

                // Signal task completed
                ReportIndividualTaskProgress("sccm_actions", "SCCM Client Actions", sccmTaskResult.Status, sccmTaskResult.Message, sccmTaskResult.Duration);

                ReportProgress("SCCM actions completed", 50);
            }

            // Run Dell Command Update
            if (options.RunDellUpdates)
            {
                ReportProgress("Checking Dell updates...", 55);
                _loggingService.LogSection("Dell Command Update");

                // Signal task started
                ReportIndividualTaskProgress("dell_complete", "Dell Command Update", TaskStatus.Running);

                var dellResult = await _dellUpdateService.RunCompleteUpdateAsync(
                    new Progress<string>(msg => ReportStatus(msg)),
                    cancellationToken);

                results.TaskResults.Add(dellResult);
                results.RequiresRestart |= dellResult.RequiresRestart;
                LogTaskResult(dellResult);

                // Signal task completed
                ReportIndividualTaskProgress("dell_complete", "Dell Command Update", dellResult.Status, dellResult.Message, dellResult.Duration);

                ReportProgress("Dell updates completed", 85);
            }

            // Run Image Checks
            if (options.RunImageChecks)
            {
                ReportProgress("Running image verification checks...", 90);
                _loggingService.LogSection("Image Verification Checks");

                // Signal task started
                ReportIndividualTaskProgress("image_checks", "Image Verification", TaskStatus.Running);

                results.ImageChecks = await _imageCheckService.RunAllChecksAsync(
                    new Progress<ImageCheck>(check =>
                    {
                        _loggingService.Log($"{check.Name}: {(check.Passed ? "PASS" : "FAIL")} - {check.Status}",
                            check.Passed ? LogLevel.Success : LogLevel.Warning);
                    }),
                    cancellationToken);

                // Signal task completed
                var checkStatus = results.ImageChecks.AllPassed ? TaskStatus.Success :
                                  results.ImageChecks.PassedCount > 0 ? TaskStatus.Warning : TaskStatus.Error;
                ReportIndividualTaskProgress("image_checks", "Image Verification",
                    checkStatus,
                    $"{results.ImageChecks.PassedCount}/{results.ImageChecks.TotalCount} passed");
            }

            // Generate PDF report (faster to open than HTML)
            ReportProgress("Generating report...", 95);
            _loggingService.LogSection("Summary");

            var reportPath = Path.Combine(
                _loggingService.GetLocalLogPath(),
                $"UniversityAutoSetup_Summary_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            await _reportService.GeneratePdfReportAsync(
                results.SystemInfo,
                results.TaskResults,
                reportPath,
                results.ImageChecks);

            results.ReportPath = reportPath;

            // Copy to network if available
            var networkPath = _loggingService.GetNetworkLogPath();
            if (!string.IsNullOrEmpty(networkPath))
            {
                try
                {
                    var networkReportPath = Path.Combine(networkPath, Path.GetFileName(reportPath));
                    await _reportService.GeneratePdfReportAsync(
                        results.SystemInfo,
                        results.TaskResults,
                        networkReportPath,
                        results.ImageChecks);
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

    private void ReportIndividualTaskProgress(string taskId, string taskName, TaskStatus status, string? message = null, TimeSpan? duration = null)
    {
        IndividualTaskProgress?.Invoke(this, new IndividualTaskProgressEventArgs(taskId, taskName, status, message, duration));
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

/// <summary>
/// Event arguments for individual task progress updates.
/// </summary>
public class IndividualTaskProgressEventArgs : EventArgs
{
    public string TaskId { get; }
    public string TaskName { get; }
    public TaskStatus Status { get; }
    public string? Message { get; }
    public TimeSpan? Duration { get; }

    public IndividualTaskProgressEventArgs(
        string taskId,
        string taskName,
        TaskStatus status,
        string? message = null,
        TimeSpan? duration = null)
    {
        TaskId = taskId;
        TaskName = taskName;
        Status = status;
        Message = message;
        Duration = duration;
    }
}
