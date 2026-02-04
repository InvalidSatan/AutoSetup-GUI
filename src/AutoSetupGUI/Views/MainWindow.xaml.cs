using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AutoSetupGUI.Views;

/// <summary>
/// Main window with navigation shell.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISystemInfoService _systemInfoService;
    private readonly ILoggingService _loggingService;

    // Cache views to preserve state when switching tabs
    private DashboardView? _dashboardView;
    private SystemInfoView? _systemInfoView;
    private TasksView? _tasksView;
    private LogViewerView? _logViewerView;
    private SettingsView? _settingsView;

    public MainWindow()
    {
        InitializeComponent();

        _systemInfoService = App.Services.GetRequiredService<ISystemInfoService>();
        _loggingService = App.Services.GetRequiredService<ILoggingService>();

        // Add command bindings for keyboard shortcuts
        CommandBindings.Add(new CommandBinding(NavigationCommands.Refresh, Refresh_Executed));

        Loaded += MainWindow_Loaded;
    }

    private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        BtnRefresh_Click(sender, e);
    }

    private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize logging
        var serviceTag = _systemInfoService.GetServiceTag();
        var computerName = _systemInfoService.GetComputerName();
        await _loggingService.InitializeAsync(serviceTag, computerName);

        // Check network status
        UpdateNetworkStatus();

        // Navigate to dashboard (use cached instance)
        _dashboardView = new DashboardView();
        ContentFrame.Navigate(_dashboardView);
    }

    private void UpdateNetworkStatus()
    {
        // Check internet connectivity
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send("8.8.8.8", 2000);
            var connected = reply.Status == System.Net.NetworkInformation.IPStatus.Success;

            NetworkIndicator.Fill = connected
                ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                : new SolidColorBrush(Color.FromRgb(209, 52, 56));
            NetworkStatus.Text = connected ? "Network: Connected" : "Network: Disconnected";
        }
        catch
        {
            NetworkIndicator.Fill = new SolidColorBrush(Color.FromRgb(209, 52, 56));
            NetworkStatus.Text = "Network: Error";
        }

        // Check P:\ drive
        var pDriveAvailable = System.IO.Directory.Exists(@"P:\");
        PDriveIndicator.Fill = pDriveAvailable
            ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
            : new SolidColorBrush(Color.FromRgb(255, 140, 0));
        PDriveStatus.Text = pDriveAvailable ? "P:\\ Drive: Available" : "P:\\ Drive: Not Available";
    }

    private void NavDashboard_Checked(object sender, RoutedEventArgs e)
    {
        _dashboardView ??= new DashboardView();
        ContentFrame?.Navigate(_dashboardView);
    }

    private void NavSystemInfo_Checked(object sender, RoutedEventArgs e)
    {
        _systemInfoView ??= new SystemInfoView();
        ContentFrame?.Navigate(_systemInfoView);
    }

    private void NavTasks_Checked(object sender, RoutedEventArgs e)
    {
        _tasksView ??= new TasksView();
        ContentFrame?.Navigate(_tasksView);
    }

    private void NavLogs_Checked(object sender, RoutedEventArgs e)
    {
        _logViewerView ??= new LogViewerView();
        ContentFrame?.Navigate(_logViewerView);
    }

    private void NavSettings_Checked(object sender, RoutedEventArgs e)
    {
        _settingsView ??= new SettingsView();
        ContentFrame?.Navigate(_settingsView);
    }

    private void BtnRunAll_Click(object sender, RoutedEventArgs e)
    {
        NavTasks.IsChecked = true;
        // Use cached tasks view
        _tasksView ??= new TasksView();
        _tasksView.RunAllTasks();
    }

    private async void BtnCopyInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var systemInfo = await _systemInfoService.CollectSystemInfoAsync();
            _systemInfoService.CopyToClipboard(systemInfo);
            MessageBox.Show("System information copied to clipboard!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"SystemInfo_{_systemInfoService.GetServiceTag()}_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".txt",
            Filter = "Text files (*.txt)|*.txt|JSON files (*.json)|*.json|CSV files (*.csv)|*.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var systemInfo = await _systemInfoService.CollectSystemInfoAsync();
                var format = System.IO.Path.GetExtension(dialog.FileName).ToLower() switch
                {
                    ".json" => ExportFormat.Json,
                    ".csv" => ExportFormat.Csv,
                    _ => ExportFormat.Text
                };

                await _systemInfoService.ExportToFileAsync(systemInfo, dialog.FileName, format);
                MessageBox.Show($"System information exported to:\n{dialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        UpdateNetworkStatus();

        // Refresh current view
        if (ContentFrame.Content is DashboardView dashboardView)
        {
            dashboardView.RefreshData();
        }
        else if (ContentFrame.Content is SystemInfoView systemInfoView)
        {
            systemInfoView.RefreshData();
        }
    }
}
