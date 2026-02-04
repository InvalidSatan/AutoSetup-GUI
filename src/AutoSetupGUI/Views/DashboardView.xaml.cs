using System.Windows;
using System.Windows.Controls;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AutoSetupGUI.Views;

/// <summary>
/// Dashboard view with system summary and quick actions.
/// </summary>
public partial class DashboardView : Page
{
    private readonly ISystemInfoService _systemInfoService;
    private readonly ILoggingService _loggingService;
    private readonly IDellUpdateService _dellUpdateService;

    public DashboardView()
    {
        InitializeComponent();

        _systemInfoService = App.Services.GetRequiredService<ISystemInfoService>();
        _loggingService = App.Services.GetRequiredService<ILoggingService>();
        _dellUpdateService = App.Services.GetRequiredService<IDellUpdateService>();

        Loaded += DashboardView_Loaded;
    }

    private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    public async void RefreshData()
    {
        // Clear cache for fresh data on manual refresh
        _systemInfoService.ClearCache();
        LoadingOverlay.Visibility = Visibility.Visible;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load system info
            var systemInfo = await _systemInfoService.CollectSystemInfoAsync();

            TxtServiceTag.Text = systemInfo.ServiceTag;
            TxtComputerName.Text = systemInfo.ComputerName;
            TxtEthernetMac.Text = string.IsNullOrEmpty(systemInfo.EthernetMac) ? "Not found" : systemInfo.EthernetMac;
            TxtWifiMac.Text = string.IsNullOrEmpty(systemInfo.WifiMac) ? "Not found" : systemInfo.WifiMac;
            TxtManufacturer.Text = systemInfo.Manufacturer;
            TxtModel.Text = systemInfo.Model;
            TxtOS.Text = $"{systemInfo.OSName} ({systemInfo.OSBuild})";
            TxtSCCM.Text = systemInfo.SCCMClientInstalled ? systemInfo.SCCMClientVersion : "Not installed";

            // Check Dell status
            if (_dellUpdateService.IsDellSystem())
            {
                TxtDellStatus.Text = _dellUpdateService.IsInstalled() ? "DCU Installed" : "DCU Not Installed";
            }
            else
            {
                TxtDellStatus.Text = "Not a Dell system";
            }

            // Log location
            var logPath = _loggingService.GetLocalLogPath();
            TxtLogLocation.Text = string.IsNullOrEmpty(logPath) ? "Not initialized" : logPath;
            TxtLogLocation.ToolTip = logPath;
        }
        catch (Exception ex)
        {
            TxtServiceTag.Text = "Error loading";
            MessageBox.Show($"Error loading system information: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            // Hide loading overlay
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnStartSetup_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Tasks view and start
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            // Find and click the Tasks nav button
            var parent = mainWindow.FindName("NavTasks") as System.Windows.Controls.RadioButton;
            if (parent != null)
            {
                parent.IsChecked = true;
            }
        }
    }
}
