using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AutoSetupGUI.Models.TaskStatus;

namespace AutoSetupGUI.Views;

/// <summary>
/// Tasks view for running setup operations.
/// </summary>
public partial class TasksView : Page
{
    private readonly TaskOrchestrator _orchestrator;
    private readonly IGroupPolicyService _groupPolicyService;
    private readonly ISCCMService _sccmService;
    private readonly IDellUpdateService _dellUpdateService;
    private readonly IImageCheckService _imageCheckService;
    private readonly IReportService _reportService;

    private string? _lastReportPath;
    private bool _isRunning;

    public TasksView()
    {
        InitializeComponent();

        _orchestrator = App.Services.GetRequiredService<TaskOrchestrator>();
        _groupPolicyService = App.Services.GetRequiredService<IGroupPolicyService>();
        _sccmService = App.Services.GetRequiredService<ISCCMService>();
        _dellUpdateService = App.Services.GetRequiredService<IDellUpdateService>();
        _imageCheckService = App.Services.GetRequiredService<IImageCheckService>();
        _reportService = App.Services.GetRequiredService<IReportService>();

        _orchestrator.TaskProgressChanged += Orchestrator_TaskProgressChanged;
        _orchestrator.StatusChanged += Orchestrator_StatusChanged;

        Loaded += TasksView_Loaded;
    }

    private void TasksView_Loaded(object sender, RoutedEventArgs e)
    {
        // Load SCCM actions list
        SCCMActionsList.ItemsSource = _sccmService.GetConfiguredActions();

        // Check if Dell system
        if (!_dellUpdateService.IsDellSystem())
        {
            TxtDellDescription.Text = "Not a Dell system - Dell updates will be skipped";
            ChkDell.IsChecked = false;
            ChkDell.IsEnabled = false;
            BtnRunDell.IsEnabled = false;
        }
    }

    public async void RunAllTasks()
    {
        await RunAllSelectedAsync();
    }

    private async void BtnRunAll_Click(object sender, RoutedEventArgs e)
    {
        await RunAllSelectedAsync();
    }

    private async Task RunAllSelectedAsync()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        SetButtonsEnabled(false);
        ProgressSection.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;

        var options = new SetupOptions
        {
            RunGroupPolicy = ChkGroupPolicy.IsChecked == true,
            RunSCCMActions = ChkSCCM.IsChecked == true,
            RunDellUpdates = ChkDell.IsChecked == true && _dellUpdateService.IsDellSystem(),
            RunImageChecks = ChkImageChecks.IsChecked == true
        };

        try
        {
            var results = await _orchestrator.RunAllTasksAsync(options);

            // Update UI with results
            UpdateTaskStatus(StatusGP, TxtStatusGP, TxtDurationGP,
                results.TaskResults.FirstOrDefault(t => t.TaskId == "group_policy"));

            UpdateTaskStatus(StatusSCCM, TxtStatusSCCM, TxtDurationSCCM,
                results.TaskResults.FirstOrDefault(t => t.TaskId == "sccm_actions"));

            UpdateTaskStatus(StatusDell, TxtStatusDell, TxtDurationDell,
                results.TaskResults.FirstOrDefault(t => t.TaskId == "dell_complete"));

            if (results.ImageChecks != null)
            {
                UpdateImageCheckStatus(results.ImageChecks);
            }

            // Final status
            TxtFinalStatus.Text = results.OverallSuccess
                ? "All tasks completed successfully!"
                : "Some tasks completed with warnings or errors.";

            if (!string.IsNullOrEmpty(results.ReportPath))
            {
                _lastReportPath = results.ReportPath;
                BtnViewReport.Visibility = Visibility.Visible;
            }

            // Offer restart if needed
            if (results.RequiresRestart)
            {
                var result = MessageBox.Show(
                    "Some updates require a restart to complete. Would you like to restart now?",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("shutdown", "/r /t 30 /c \"Restarting to complete setup updates...\"");
                }
            }
        }
        catch (Exception ex)
        {
            TxtFinalStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"An error occurred during setup: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isRunning = false;
            SetButtonsEnabled(true);
        }
    }

    private void Orchestrator_TaskProgressChanged(object? sender, TaskProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = e.Percentage;
            TxtProgressStatus.Text = e.Message;
        });
    }

    private void Orchestrator_StatusChanged(object? sender, string e)
    {
        Dispatcher.Invoke(() =>
        {
            TxtProgressDetail.Text = e;
        });
    }

    private async void BtnRunGP_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        BtnRunGP.IsEnabled = false;

        try
        {
            SetStatus(StatusGP, TxtStatusGP, "Running", TaskStatus.Running);
            var result = await _groupPolicyService.UpdateGroupPolicyAsync(
                new Progress<string>(msg => TxtProgressDetail.Text = msg));
            UpdateTaskStatus(StatusGP, TxtStatusGP, TxtDurationGP, result);
        }
        finally
        {
            _isRunning = false;
            BtnRunGP.IsEnabled = true;
        }
    }

    private async void BtnRunSCCM_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        BtnRunSCCM.IsEnabled = false;

        try
        {
            SetStatus(StatusSCCM, TxtStatusSCCM, "Running", TaskStatus.Running);
            var results = await _sccmService.RunAllActionsAsync();

            var successCount = results.Count(r => r.Status == TaskStatus.Success);
            var totalCount = results.Count();

            var taskResult = new TaskResult
            {
                TaskId = "sccm_actions",
                TaskName = "SCCM Client Actions",
                Status = successCount == totalCount ? TaskStatus.Success :
                         successCount > 0 ? TaskStatus.Warning : TaskStatus.Error,
                Message = $"{successCount}/{totalCount} actions completed"
            };

            UpdateTaskStatus(StatusSCCM, TxtStatusSCCM, TxtDurationSCCM, taskResult);
        }
        finally
        {
            _isRunning = false;
            BtnRunSCCM.IsEnabled = true;
        }
    }

    private async void BtnRunDell_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        BtnRunDell.IsEnabled = false;

        try
        {
            SetStatus(StatusDell, TxtStatusDell, "Running", TaskStatus.Running);
            var result = await _dellUpdateService.RunCompleteUpdateAsync(
                new Progress<string>(msg => TxtProgressDetail.Text = msg));
            UpdateTaskStatus(StatusDell, TxtStatusDell, TxtDurationDell, result);
        }
        finally
        {
            _isRunning = false;
            BtnRunDell.IsEnabled = true;
        }
    }

    private async void BtnRunChecks_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        BtnRunChecks.IsEnabled = false;

        try
        {
            SetStatus(StatusChecks, TxtStatusChecks, "Running", TaskStatus.Running);
            var results = await _imageCheckService.RunAllChecksAsync();
            UpdateImageCheckStatus(results);
        }
        finally
        {
            _isRunning = false;
            BtnRunChecks.IsEnabled = true;
        }
    }

    private void BtnViewReport_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastReportPath) && System.IO.File.Exists(_lastReportPath))
        {
            _reportService.OpenInBrowser(_lastReportPath);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        BtnRunAll.IsEnabled = enabled;
        BtnRunGP.IsEnabled = enabled;
        BtnRunSCCM.IsEnabled = enabled;
        BtnRunDell.IsEnabled = enabled && _dellUpdateService.IsDellSystem();
        BtnRunChecks.IsEnabled = enabled;
    }

    private void UpdateTaskStatus(Border statusBorder, TextBlock statusText, TextBlock durationText, TaskResult? result)
    {
        if (result == null)
        {
            SetStatus(statusBorder, statusText, "Skipped", TaskStatus.Skipped);
            return;
        }

        SetStatus(statusBorder, statusText, result.Status.ToString(), result.Status);
        durationText.Text = result.Duration.HasValue ? $"({result.DurationFormatted})" : "";
    }

    private void UpdateImageCheckStatus(ImageCheckResult results)
    {
        var status = results.AllPassed ? TaskStatus.Success :
                     results.PassedCount > 0 ? TaskStatus.Warning : TaskStatus.Error;

        SetStatus(StatusChecks, TxtStatusChecks, $"{results.PassedCount}/{results.TotalCount} Passed", status);
    }

    private void SetStatus(Border statusBorder, TextBlock statusText, string text, TaskStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            statusText.Text = text;

            var (background, foreground) = status switch
            {
                TaskStatus.Success => (Color.FromRgb(223, 246, 221), Color.FromRgb(16, 124, 16)),
                TaskStatus.Warning => (Color.FromRgb(255, 244, 206), Color.FromRgb(255, 140, 0)),
                TaskStatus.Error => (Color.FromRgb(253, 231, 233), Color.FromRgb(209, 52, 56)),
                TaskStatus.Running => (Color.FromRgb(204, 229, 255), Color.FromRgb(0, 120, 215)),
                _ => (Color.FromRgb(240, 240, 240), Color.FromRgb(102, 102, 102))
            };

            statusBorder.Background = new SolidColorBrush(background);
            statusText.Foreground = new SolidColorBrush(foreground);
        });
    }
}
