using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services;
using AutoSetupGUI.Services.Implementations;
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
    private readonly ObservableCollection<CompletedUpdateViewModel> _completedUpdates = new();
    private string? _lastReportPath;
    private bool _isRunning;
    private bool _isInitialized;

    // State persistence for network resilience
    private TaskExecutionState? _currentState;
    private System.Timers.Timer? _stateSaveTimer;

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
        _orchestrator.DellProgress += Orchestrator_DellProgress;

        // Subscribe to detailed Dell update progress
        _dellUpdateService.UpdateProgress += DellUpdateService_UpdateProgress;

        Loaded += TasksView_Loaded;
    }

    private void TasksView_Loaded(object sender, RoutedEventArgs e)
    {
        // Only initialize once to preserve state when switching tabs
        if (_isInitialized)
            return;

        _isInitialized = true;

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

        // Check for recovery mode
        if (App.IsRecoveryMode && App.RecoveredState != null)
        {
            RestoreFromRecovery(App.RecoveredState);
        }
    }

    /// <summary>
    /// Restores the UI state from a recovered session.
    /// </summary>
    private void RestoreFromRecovery(TaskExecutionState state)
    {
        try
        {
            // Show recovery notification
            TxtFinalStatus.Text = "Recovered from interrupted session - Dell updates are continuing in background";
            TxtFinalStatus.Foreground = new SolidColorBrush(Color.FromRgb(3, 101, 156)); // Info blue

            // Restore task selection state
            ChkGroupPolicy.IsChecked = state.RunGroupPolicy;
            ChkSCCM.IsChecked = state.RunSCCMActions;
            ChkDell.IsChecked = state.RunDellUpdates;
            ChkImageChecks.IsChecked = state.RunImageChecks;

            // Mark completed tasks
            if (state.GroupPolicyComplete)
            {
                SetStatus(StatusGP, TxtStatusGP, "Success", TaskStatus.Success);
            }
            if (state.SCCMActionsComplete)
            {
                SetStatus(StatusSCCM, TxtStatusSCCM, "Success", TaskStatus.Success);
            }
            if (state.ImageChecksComplete)
            {
                SetStatus(StatusChecks, TxtStatusChecks, "Success", TaskStatus.Success);
            }

            // Restore Dell update state
            if (state.RunDellUpdates && !state.DellUpdatesComplete)
            {
                SetStatus(StatusDell, TxtStatusDell, "Running (Recovered)", TaskStatus.Running);

                // Show Dell progress panel with recovered state
                DellProgressPanel.Visibility = Visibility.Visible;
                TxtDellPhase.Text = state.CurrentDellPhase ?? "Continuing in background...";
                TxtDellUpdatesCount.Text = $"{state.DellCompletedUpdates} of {state.DellTotalUpdates} update(s) complete";

                // Restore completed updates list
                foreach (var updateName in state.DellCompletedUpdateNames)
                {
                    _completedUpdates.Add(new CompletedUpdateViewModel
                    {
                        Name = updateName,
                        StatusText = "Installed"
                    });
                }
                CompletedUpdatesList.ItemsSource = _completedUpdates;

                // Restore log messages
                foreach (var msg in state.LogMessages)
                {
                    TxtDellLiveLog.Text += msg + "\n";
                }
                TxtDellLiveLog.Text += $"\n[{DateTime.Now:HH:mm:ss}] === Session recovered - DCU continuing in background ===\n";
                DellLogScroller.ScrollToEnd();

                // DCU is likely still running in background - we can monitor for completion
                TxtDellCurrentActivity.Text = "Dell Command Update is continuing in the background. Updates will complete even if this window closes.";
            }
            else if (state.DellUpdatesComplete)
            {
                SetStatus(StatusDell, TxtStatusDell, "Success", TaskStatus.Success);
            }

            // Clear recovery state so we don't restore again
            NetworkResilienceManager.ClearState();
        }
        catch
        {
            // Ignore recovery errors - just continue normally
            NetworkResilienceManager.ClearState();
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

        // Initialize state persistence for network resilience
        InitializeStatePersistence(options);

        // Reset all task statuses to pending before starting
        if (options.RunGroupPolicy)
            SetStatus(StatusGP, TxtStatusGP, "Pending", TaskStatus.Pending);
        if (options.RunSCCMActions)
        {
            SetStatus(StatusSCCM, TxtStatusSCCM, "Pending", TaskStatus.Pending);
            ResetSCCMActionStatuses();
        }
        if (options.RunDellUpdates)
        {
            SetStatus(StatusDell, TxtStatusDell, "Pending", TaskStatus.Pending);
            // Reset Dell progress panel
            ResetDellProgressPanel();
        }
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

                // Show the report in our built-in viewer (avoids Edge first-run experience)
                // The viewer also shows the restart button if needed
                _reportService.ShowReportViewer(results.ReportPath, results.RequiresRestart);
            }

            // Always show restart prompt if required, even if report is shown
            if (results.RequiresRestart)
            {
                ShowRestartNotification();
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

            // Clean up state persistence - tasks completed (successfully or with error)
            FinalizeStatePersistence();
        }
    }

    #region State Persistence for Network Resilience

    /// <summary>
    /// Initializes state persistence for network resilience.
    /// </summary>
    private void InitializeStatePersistence(SetupOptions options)
    {
        _currentState = new TaskExecutionState
        {
            StartTime = DateTime.Now,
            LastUpdateTime = DateTime.Now,
            IsRunning = true,
            IsComplete = false,
            RunGroupPolicy = options.RunGroupPolicy,
            RunSCCMActions = options.RunSCCMActions,
            RunDellUpdates = options.RunDellUpdates,
            RunImageChecks = options.RunImageChecks
        };

        // Save initial state
        NetworkResilienceManager.SaveState(_currentState);

        // Set up periodic state saving (every 5 seconds)
        _stateSaveTimer = new System.Timers.Timer(5000);
        _stateSaveTimer.Elapsed += (s, e) =>
        {
            if (_currentState != null)
            {
                _currentState.LastUpdateTime = DateTime.Now;
                NetworkResilienceManager.SaveState(_currentState);
            }
        };
        _stateSaveTimer.Start();
    }

    /// <summary>
    /// Updates the persisted state during task execution.
    /// </summary>
    private void UpdatePersistedState(Action<TaskExecutionState> updateAction)
    {
        if (_currentState == null) return;

        try
        {
            updateAction(_currentState);
            _currentState.LastUpdateTime = DateTime.Now;
            NetworkResilienceManager.SaveState(_currentState);
        }
        catch
        {
            // Ignore state save errors
        }
    }

    /// <summary>
    /// Finalizes state persistence when tasks complete.
    /// </summary>
    private void FinalizeStatePersistence()
    {
        try
        {
            _stateSaveTimer?.Stop();
            _stateSaveTimer?.Dispose();
            _stateSaveTimer = null;

            // Clear the state immediately - tasks are done
            // This is important for cleanup to work properly on exit
            _currentState = null;
            NetworkResilienceManager.ClearState();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion

    private void Orchestrator_TaskProgressChanged(object? sender, TaskProgressEventArgs e)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ProgressBar.Value = e.Percentage;
                    TxtProgressStatus.Text = e.Message;
                }
                catch { /* Ignore UI update errors */ }
            });
        }
        catch { /* Ignore dispatcher errors */ }
    }

    private void Orchestrator_StatusChanged(object? sender, string e)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    TxtProgressDetail.Text = e;
                }
                catch { /* Ignore UI update errors */ }
            });
        }
        catch { /* Ignore dispatcher errors */ }
    }

    private void Orchestrator_IndividualTaskProgress(object? sender, IndividualTaskProgressEventArgs e)
    {
        // Update persisted state for task completion
        if (e.Status == TaskStatus.Success || e.Status == TaskStatus.Warning)
        {
            UpdatePersistedState(state =>
            {
                switch (e.TaskId)
                {
                    case "group_policy":
                        state.GroupPolicyComplete = true;
                        break;
                    case "sccm_actions":
                        state.SCCMActionsComplete = true;
                        break;
                    case "dell_complete":
                        state.DellUpdatesComplete = true;
                        break;
                    case "image_checks":
                        state.ImageChecksComplete = true;
                        break;
                }
            });
        }

        try
        {
            Dispatcher.Invoke(() =>
            {
                try
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
                }
                catch { /* Ignore UI update errors */ }
            });
        }
        catch { /* Ignore dispatcher errors */ }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}m";
        return $"{duration.TotalSeconds:F1}s";
    }

    private void Orchestrator_SCCMActionProgress(object? sender, SCCMActionResult e)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Find the matching view model and update its status
                    var viewModel = _sccmActionViewModels.FirstOrDefault(vm =>
                        vm.ScheduleId == e.Action.ScheduleId);

                    if (viewModel != null)
                    {
                        viewModel.UpdateFromResult(e);
                    }
                }
                catch { /* Ignore UI update errors */ }
            });
        }
        catch { /* Ignore dispatcher errors */ }
    }

    private void Orchestrator_DellProgress(object? sender, DellProgressEventArgs e)
    {
        // Wrap in try-catch to prevent network disruption from crashing the app
        try
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Show the Dell progress panel
                    DellProgressPanel.Visibility = Visibility.Visible;

                    // Update phase display
                    TxtDellPhase.Text = e.Phase switch
                    {
                        "Initializing" => "Initializing Dell Command Update...",
                        "Installing DCU" => "Installing Dell Command Update...",
                        "Configuring" => "Configuring Dell Command Update...",
                        "Scanning" => "Scanning for available updates...",
                        "Scan Complete" => "Scan complete",
                        "Applying Updates" => "Applying updates...",
                        "Complete" => e.Message.Contains("RESTART") ? "Complete - Restart Required" : "Complete",
                        "Error" => "Error occurred",
                        _ => e.Phase
                    };

                    // Update progress bar (only if percentage >= 0)
                    if (e.Percentage >= 0)
                    {
                        DellProgressBar.Value = e.Percentage;
                        TxtDellOverallPercent.Text = $"{e.Percentage}%";
                    }

                    // Parse updates count from message if available
                    if (e.Message.Contains("update(s)") || e.Message.Contains("Found"))
                    {
                        TxtDellUpdatesCount.Text = e.Message;
                    }
                    else if (e.Phase == "Complete")
                    {
                        if (e.Message.Contains("No updates"))
                            TxtDellUpdatesCount.Text = "No updates needed";
                        else if (e.Message.Contains("RESTART"))
                            TxtDellUpdatesCount.Text = "Updates applied - restart required";
                        else
                            TxtDellUpdatesCount.Text = "Updates applied successfully";
                    }

                    // Append to live log (less verbose now since we have better UI)
                    if (!string.IsNullOrEmpty(e.Message) && !e.Message.Contains("%"))
                    {
                        var timestamp = DateTime.Now.ToString("HH:mm:ss");
                        TxtDellLiveLog.Text += $"[{timestamp}] {e.Message}\n";
                        DellLogScroller.ScrollToEnd();
                    }

                    // If we have detailed log output, append it
                    if (!string.IsNullOrEmpty(e.LogOutput))
                    {
                        TxtDellLiveLog.Text += "\n=== Detailed Log ===\n" + e.LogOutput + "\n";
                        DellLogScroller.ScrollToEnd();
                    }
                }
                catch { /* Ignore UI update errors during network disruption */ }
            });
        }
        catch { /* Ignore dispatcher errors */ }
    }

    private void DellUpdateService_UpdateProgress(DellUpdateProgressInfo info)
    {
        // Update persisted state for Dell progress
        UpdatePersistedState(state =>
        {
            state.CurrentDellPhase = info.Phase;
            state.DellTotalUpdates = info.TotalUpdates;
            state.DellCompletedUpdates = info.CompletedCount;

            // Track completed update names
            if (info.IsComplete && !string.IsNullOrEmpty(info.CurrentUpdateName))
            {
                if (!state.DellCompletedUpdateNames.Contains(info.CurrentUpdateName))
                {
                    state.DellCompletedUpdateNames.Add(info.CurrentUpdateName);
                }
            }

            // Track log messages (keep last 50 for recovery)
            if (!string.IsNullOrEmpty(info.Message) && !info.IsRawOutput)
            {
                state.LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {info.Message}");
                if (state.LogMessages.Count > 50)
                {
                    state.LogMessages.RemoveAt(0);
                }
            }
        });

        try
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Show the Dell progress panel
                    DellProgressPanel.Visibility = Visibility.Visible;

                    // Update overall progress
                    if (info.TotalUpdates > 0)
                    {
                        var overallPercent = info.OverallPercentage;
                        DellProgressBar.Value = overallPercent;
                        TxtDellOverallPercent.Text = $"{overallPercent}%";
                        TxtDellUpdatesCount.Text = $"{info.CompletedCount} of {info.TotalUpdates} update(s) complete";
                    }

                    // Update phase text
                    TxtDellPhase.Text = info.Phase switch
                    {
                        "Starting" => $"Found {info.TotalUpdates} update(s) to apply",
                        "Downloading" => $"Downloading update {info.CurrentUpdateIndex} of {info.TotalUpdates}",
                        "Installing" => $"Installing update {info.CurrentUpdateIndex} of {info.TotalUpdates}",
                        "Completed" => info.IsComplete ? $"Completed {info.CompletedCount} of {info.TotalUpdates}" : info.Message,
                        "RebootRequired" => "Complete - Restart Required",
                        _ => info.Phase
                    };

                    // Show/update current update panel for downloading/installing
                    if (info.Phase == "Downloading" || info.Phase == "Installing")
                    {
                        CurrentUpdatePanel.Visibility = Visibility.Visible;
                        TxtCurrentUpdateName.Text = info.CurrentUpdateName ?? "Unknown Update";

                        // Update status badge
                        if (info.Phase == "Downloading")
                        {
                            TxtCurrentUpdateStatus.Text = "Downloading";
                            CurrentUpdateStatusBadge.Background = new SolidColorBrush(Color.FromRgb(225, 240, 250));
                            TxtCurrentUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(3, 101, 156));
                        }
                        else
                        {
                            TxtCurrentUpdateStatus.Text = "Installing";
                            CurrentUpdateStatusBadge.Background = new SolidColorBrush(Color.FromRgb(255, 248, 225));
                            TxtCurrentUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(215, 165, 39));
                        }

                        // Update progress bars
                        CurrentDownloadBar.Value = info.DownloadProgress;
                        TxtCurrentDownloadPercent.Text = $"{info.DownloadProgress}%";

                        CurrentInstallBar.Value = info.InstallProgress;
                        TxtCurrentInstallPercent.Text = $"{info.InstallProgress}%";

                        // If in install phase, ensure download shows 100%
                        if (info.Phase == "Installing")
                        {
                            CurrentDownloadBar.Value = 100;
                            TxtCurrentDownloadPercent.Text = "100%";
                        }
                    }

                    // Handle completed update
                    if (info.IsComplete && !string.IsNullOrEmpty(info.CurrentUpdateName))
                    {
                        // Add to completed list
                        _completedUpdates.Add(new CompletedUpdateViewModel
                        {
                            Name = info.CurrentUpdateName,
                            StatusText = "Installed"
                        });
                        CompletedUpdatesList.ItemsSource = _completedUpdates;

                        // Hide current update panel briefly
                        CurrentUpdatePanel.Visibility = Visibility.Collapsed;
                    }

                    // Append raw output to log
                    if (info.IsRawOutput && !string.IsNullOrWhiteSpace(info.Message))
                    {
                        TxtDellLiveLog.Text += $"{info.Message}\n";
                        DellLogScroller.ScrollToEnd();
                    }
                    else if (!info.IsRawOutput && !string.IsNullOrEmpty(info.Message))
                    {
                        // Also log non-raw important messages
                        var timestamp = DateTime.Now.ToString("HH:mm:ss");
                        TxtDellLiveLog.Text += $"[{timestamp}] {info.Message}\n";
                        DellLogScroller.ScrollToEnd();
                    }

                    // Update current activity for misc messages
                    if (!string.IsNullOrEmpty(info.Message) && info.Phase != "Downloading" && info.Phase != "Installing")
                    {
                        TxtDellCurrentActivity.Text = info.Message;
                    }
                }
                catch { /* Ignore UI update errors during network disruption */ }
            });
        }
        catch { /* Ignore dispatcher errors */ }
    }

    private void ResetDellProgressPanel()
    {
        DellProgressPanel.Visibility = Visibility.Collapsed;
        TxtDellPhase.Text = "Initializing...";
        DellProgressBar.Value = 0;
        TxtDellOverallPercent.Text = "0%";
        TxtDellUpdatesCount.Text = "Checking for updates...";
        TxtDellCurrentActivity.Text = "";
        TxtDellLiveLog.Text = "";
        CurrentUpdatePanel.Visibility = Visibility.Collapsed;
        TxtCurrentUpdateName.Text = "";
        CurrentDownloadBar.Value = 0;
        CurrentInstallBar.Value = 0;
        TxtCurrentDownloadPercent.Text = "0%";
        TxtCurrentInstallPercent.Text = "0%";
        _completedUpdates.Clear();
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

            // Reset and show Dell progress panel
            ResetDellProgressPanel();
            DellProgressPanel.Visibility = Visibility.Visible;

            var result = await _dellUpdateService.RunCompleteUpdateAsync(
                new Progress<string>(msg =>
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                TxtProgressDetail.Text = msg;

                                // Append to live log
                                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                                TxtDellLiveLog.Text += $"[{timestamp}] {msg}\n";
                                DellLogScroller.ScrollToEnd();
                            }
                            catch { /* Ignore UI errors */ }
                        });
                    }
                    catch { /* Ignore dispatcher errors */ }
                }));

            UpdateTaskStatus(StatusDell, TxtStatusDell, TxtDurationDell, result);

            // Update final status
            TxtDellPhase.Text = result.Status == TaskStatus.Success ? "Complete" : "Error";
            TxtDellOverallPercent.Text = "100%";
            DellProgressBar.Value = 100;

            if (!string.IsNullOrEmpty(result.DetailedOutput))
            {
                TxtDellLiveLog.Text += "\n=== Detailed Log ===\n" + result.DetailedOutput;
                DellLogScroller.ScrollToEnd();
            }
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
            _reportService.ShowReportViewer(_lastReportPath, requiresRestart: false);
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

    /// <summary>
    /// Shows a restart notification dialog with options to restart now or later.
    /// </summary>
    private void ShowRestartNotification()
    {
        // Update final status to indicate restart is needed
        TxtFinalStatus.Text = "⚠️ Restart Required - Some updates require a restart to complete.";
        TxtFinalStatus.Foreground = new SolidColorBrush(Color.FromRgb(215, 165, 39)); // Gold warning color

        var result = MessageBox.Show(
            "Some updates require a system restart to complete.\n\n" +
            "Would you like to restart now?\n\n" +
            "• Click 'Yes' to restart immediately (30 second countdown)\n" +
            "• Click 'No' to restart later manually",
            "Restart Required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Clear state before restart so cleanup can happen
                NetworkResilienceManager.ClearState();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 30 /c \"Restarting to complete University Auto Setup updates...\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                TxtFinalStatus.Text = "🔄 Restarting in 30 seconds... Close any open work now.";
                TxtFinalStatus.Foreground = new SolidColorBrush(Color.FromRgb(3, 101, 156)); // Blue info color
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initiate restart: {ex.Message}\n\nPlease restart manually.",
                    "Restart Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            TxtFinalStatus.Text = "⚠️ Setup complete - Please restart your computer to finish updates.";
        }
    }
}

/// <summary>
/// View model for completed Dell updates display.
/// </summary>
public class CompletedUpdateViewModel
{
    public string Name { get; set; } = string.Empty;
    public string StatusText { get; set; } = "Installed";
}
