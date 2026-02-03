using System.Windows;
using System.Windows.Controls;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AutoSetupGUI.Views;

/// <summary>
/// System information view with detailed hardware and software info.
/// </summary>
public partial class SystemInfoView : Page
{
    private readonly ISystemInfoService _systemInfoService;
    private SystemInfo? _currentSystemInfo;

    public SystemInfoView()
    {
        InitializeComponent();

        _systemInfoService = App.Services.GetRequiredService<ISystemInfoService>();

        Loaded += SystemInfoView_Loaded;
    }

    private async void SystemInfoView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    public async void RefreshData()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _currentSystemInfo = await _systemInfoService.CollectSystemInfoAsync();

            // Asset Information
            TxtServiceTag.Text = _currentSystemInfo.ServiceTag;
            TxtComputerName.Text = _currentSystemInfo.ComputerName;
            TxtManufacturer.Text = _currentSystemInfo.Manufacturer;
            TxtModel.Text = _currentSystemInfo.Model;

            // BIOS
            TxtBiosVersion.Text = _currentSystemInfo.BiosVersion;
            TxtBiosDate.Text = _currentSystemInfo.BiosReleaseDate;

            // Operating System
            TxtOSName.Text = _currentSystemInfo.OSName;
            TxtOSVersion.Text = _currentSystemInfo.OSVersion;
            TxtOSBuild.Text = _currentSystemInfo.OSBuild;
            TxtOSArch.Text = _currentSystemInfo.OSArchitecture;
            TxtLastBoot.Text = _currentSystemInfo.LastBootTime.ToString("yyyy-MM-dd HH:mm:ss");

            // Network
            TxtEthernetMac.Text = string.IsNullOrEmpty(_currentSystemInfo.EthernetMac) ? "Not found" : _currentSystemInfo.EthernetMac;
            TxtWifiMac.Text = string.IsNullOrEmpty(_currentSystemInfo.WifiMac) ? "Not found" : _currentSystemInfo.WifiMac;
            TxtDomain.Text = _currentSystemInfo.DomainName;
            TxtDomainJoined.Text = _currentSystemInfo.IsDomainJoined ? "Yes" : "No";

            // Hardware
            TxtProcessor.Text = _currentSystemInfo.ProcessorName;
            TxtCores.Text = $"{_currentSystemInfo.ProcessorCores} cores ({_currentSystemInfo.ProcessorLogicalProcessors} logical)";
            TxtRAM.Text = _currentSystemInfo.TotalRAMFormatted;

            // Storage
            DiskList.ItemsSource = _currentSystemInfo.Disks;

            // SCCM
            TxtSCCMInstalled.Text = _currentSystemInfo.SCCMClientInstalled ? "Yes" : "No";
            TxtSCCMVersion.Text = string.IsNullOrEmpty(_currentSystemInfo.SCCMClientVersion) ? "N/A" : _currentSystemInfo.SCCMClientVersion;
            TxtSCCMSiteCode.Text = string.IsNullOrEmpty(_currentSystemInfo.SCCMSiteCode) ? "N/A" : _currentSystemInfo.SCCMSiteCode;
            TxtSCCMMP.Text = string.IsNullOrEmpty(_currentSystemInfo.SCCMManagementPoint) ? "N/A" : _currentSystemInfo.SCCMManagementPoint;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading system information: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSystemInfo == null)
        {
            MessageBox.Show("Please wait for system information to load.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _systemInfoService.CopyToClipboard(_currentSystemInfo);
            MessageBox.Show("System information copied to clipboard!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }
}
