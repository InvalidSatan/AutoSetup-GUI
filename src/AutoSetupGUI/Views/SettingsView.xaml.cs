using System.Windows;
using System.Windows.Controls;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoSetupGUI.Views;

/// <summary>
/// Settings view showing application configuration.
/// </summary>
public partial class SettingsView : Page
{
    private readonly IConfiguration _configuration;
    private readonly IConfigurationService _configService;

    public SettingsView()
    {
        InitializeComponent();

        _configuration = App.Services.GetRequiredService<IConfiguration>();
        _configService = App.Services.GetRequiredService<IConfigurationService>();

        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Application
        TxtVersion.Text = _configService.Version;
        TxtContact.Text = $"{_configuration["Branding:ContactName"]} ({_configuration["Branding:ContactEmail"]})";

        // Logging
        TxtLocalPath.Text = _configuration["Logging:LocalPath"] ?? @"C:\Temp\UniversityAutoSetup\Logs";
        TxtNetworkPath.Text = _configuration["Logging:NetworkPath"] ?? @"P:\UniversityAutoSetup\Logs";

        // Dell Command Update
        TxtDCUPath.Text = _configuration["DellCommandUpdate:InstallerUNC"] ?? @"\\server\share\Dell\DCU\DCU_Setup.exe";
        TxtMaxRetries.Text = _configuration["DellCommandUpdate:MaxRetries"] ?? "3";
        TxtRetryDelay.Text = $"{_configuration["DellCommandUpdate:RetryDelaySeconds"] ?? "10"} seconds";

        // SCCM
        TxtActionTimeout.Text = $"{_configuration["SCCM:ActionTimeoutSeconds"] ?? "120"} seconds";
        TxtRepairOnFailure.Text = _configuration.GetValue("SCCM:RepairOnFailure", true) ? "Yes" : "No";

        // Image Checks
        TxtMinDiskSpace.Text = $"{_configuration["ImageChecks:MinDiskSpaceGB"] ?? "20"} GB";
        TxtBitLockerRequired.Text = _configuration.GetValue("ImageChecks:BitLockerRequired", true) ? "Yes" : "No";
    }
}
