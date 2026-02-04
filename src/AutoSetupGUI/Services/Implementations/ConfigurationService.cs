using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private AppConfiguration? _appConfig;
    private string _currentTheme = "Light";

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
        LoadConfiguration();
    }

    public AppConfiguration Configuration => _appConfig!;

    public string Version => _appConfig?.Application.Version ?? "2.0.0";

    private void LoadConfiguration()
    {
        _appConfig = new AppConfiguration();
        _configuration.Bind(_appConfig);

        // Apply defaults if not specified
        _appConfig.Application ??= new ApplicationSettings();
        _appConfig.Branding ??= new BrandingSettings();
        _appConfig.Logging ??= new LoggingSettings();
        _appConfig.DellCommandUpdate ??= new DellCommandUpdateSettings();
        _appConfig.GroupPolicy ??= new GroupPolicySettings();
        _appConfig.SCCM ??= new SCCMSettings();
        _appConfig.ImageChecks ??= new ImageCheckSettings();
        _appConfig.UI ??= new UISettings();

        _currentTheme = _appConfig.UI.DefaultTheme;
    }

    public void Reload()
    {
        if (_configuration is IConfigurationRoot configRoot)
        {
            configRoot.Reload();
        }
        LoadConfiguration();
    }

    public string GetDCUInstallerPath()
    {
        return _appConfig?.DellCommandUpdate.InstallerUNC ?? @"\\server\share\Dell\DCU\DCU_Setup.exe";
    }

    public IReadOnlyList<SCCMActionConfig> GetSCCMActions()
    {
        return _appConfig?.SCCM.Actions ?? Array.Empty<SCCMActionConfig>();
    }

    public string GetLocalLogPath()
    {
        return _appConfig?.Logging.LocalPath ?? @"C:\Temp\UniversityAutoSetup\Logs";
    }

    public string GetNetworkLogPath()
    {
        return _appConfig?.Logging.NetworkPath ?? @"P:\UniversityAutoSetup\Logs";
    }

    public string GetTheme()
    {
        return _currentTheme;
    }

    public void SetTheme(string theme)
    {
        _currentTheme = theme;
    }
}
