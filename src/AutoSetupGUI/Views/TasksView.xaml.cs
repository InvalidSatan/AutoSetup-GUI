using System.Collections.ObjectModel;
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

    private readonly ObservableCollection<SCCMActionViewModel> _sccmActionViewModels = new();
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
        _orchestrator.IndividualTaskProgress += Orchestrator_IndividualTaskProgress;
        _orchestrator.SCCMActionProgress += Orchestrator_SCCMActionProgress;

        Loaded += TasksView_Loaded;
    }

    private void TasksView_Loaded(object sender, RoutedEventArgs e)
    {
        // Load SCCM actions list with observable view models
        var actions = _sccmService.GetConfiguredActions();
        _sccmActionViewModels.Clear();
        foreach (var action in actions)
        {
            _sccmActionViewModels.Add(new SCCMActionViewModel(action));
        }
        SCCMActionsList.ItemsSource = _sccmActionViewModels;

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

        // Reset all task statuses to pending before starting
        if (options.RunGroupPolicy)
            SetStatus(StatusGP, TxtStatusGP, "Pending", TaskStatus.Pending);
        if (options.RunSCCMActions)
        {
            SetStatus(StatusSCCM, TxtStatusSCCM, "Pending", TaskStatus.Pending);
            ResetSCCMActionStatuses();
        }
        if (options.RunDellUpdates)
            SetStatus(StatusDell, TxtStatusDell, "Pending", TaskStatus.Pending);
        if (options.RunImageChecks)
            SetStatus(StatusChecks, TxtStatusChecks, "Pending", TaskStatus.Pending);

        // Clear duration texts
        TxtDurationGP.Text = "";
        TxtDurationSCCM.Text = "";
        TxtDurationDell.Text = "";

        // Reset final status text
        TxtFinalStatus.Text = "";
        BtnViewReport.Visibility = Visibility.Collapsed;

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

                // Auto-open the PDF report (much faster than HTML in browser)
                _reportService.OpenReport(results.ReportPath);
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

    private void Orchestrator_IndividualTaskProgress(object? sender, IndividualTaskProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Map task ID to UI elements and update status
            switch (e.TaskId)
            {
                case "group_policy":
                    SetStatus(StatusGP, TxtStatusGP,
                        e.Status == TaskStatus.Running ? "Running..." : e.Status.ToString(),
                        e.Status);
                    if (e.Duration.HasValue)
                        TxtDurationGP.Text = $"({FormatDuration(e.Duration.Value)})";
                    break;

                case "sccm_actions":
                    SetStatus(StatusSCCM, TxtStatusSCCM,
                        e.Status == TaskStatus.Running ? "Running..." : (e.Message ?? e.Status.ToString()),
                        e.Status);
                    if (e.Duration.HasValue)
                        TxtDurationSCCM.Text = $"({FormatDuration(e.Duration.Value)})";
                    break;

                case "dell_complete":
                    SetStatus(StatusDell, TxtStatusDell,
                        e.Status == TaskStatus.Running ? "Running..." : e.Status.ToString(),
                        e.Status);
                    if (e.Duration.HasValue)
                        TxtDurationDell.Text = $"({FormatDuration(e.Duration.Value)})";
                    break;

                case "image_checks":
                    SetStatus(StatusChecks, TxtStatusChecks,
                        e.Status == TaskStatus.Running ? "Running..." : (e.Message ?? e.Status.ToString()),
                        e.Status);
                    break;
            }
        });
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}m";
        return $"{duration.TotalSeconds:F1}s";
    }

    private void Orchestrator_SCCMActionProgress(object? sender, SCCMActionResult e)
    {
        Dispatcher.Invoke(() =>
        {
            // Find the matching view model and update its status
            var viewModel = _sccmActionViewModels.FirstOrDefault(vm =>
                vm.ScheduleId == e.Action.ScheduleId);

            if (viewModel != null)
            {
                viewModel.UpdateFromResult(e);
            }
        });
    }

    private void ResetSCCMActionStatuses()
    {
        foreach (var vm in _sccmActionViewModels)
        {
            vm.Reset();
        }
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
            ResetSCCMActionStatuses();

            var results = await _sccmService.RunAllActionsAsync(
                new Progress<SCCMActionResult>(r =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var viewModel = _sccmActionViewModels.FirstOrDefault(vm =>
                            vm.ScheduleId == r.Action.ScheduleId);
                        viewModel?.UpdateFromResult(r);
                    });
                }));

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
            _reportService.OpenReport(_lastReportPath);
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

            // App State branded status colors
            var (background, foreground) = status switch
            {
                TaskStatus.Success => (Color.FromRgb(232, 245, 232), Color.FromRgb(105, 170, 97)),   // Grass Green
                TaskStatus.Warning => (Color.FromRgb(255, 248, 225), Color.FromRgb(215, 165, 39)),   // Dark Gold
                TaskStatus.Error => (Color.FromRgb(255, 235, 232), Color.FromRgb(198, 96, 42)),      // Brick Orange
                TaskStatus.Running => (Color.FromRgb(225, 240, 250), Color.FromRgb(3, 101, 156)),    // Lake Blue
                _ => (Color.FromRgb(240, 240, 240), Color.FromRgb(76, 72, 71))                       // Dark Gray
            };

            statusBorder.Background = new SolidColorBrush(background);
            statusText.Foreground = new SolidColorBrush(foreground);
        });
    }
}
