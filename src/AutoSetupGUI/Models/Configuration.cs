namespace AutoSetupGUI.Models;

/// <summary>
/// Application configuration model.
/// </summary>
public class AppConfiguration
{
    public ApplicationSettings Application { get; set; } = new();
    public BrandingSettings Branding { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public DellCommandUpdateSettings DellCommandUpdate { get; set; } = new();
    public GroupPolicySettings GroupPolicy { get; set; } = new();
    public SCCMSettings SCCM { get; set; } = new();
    public ImageCheckSettings ImageChecks { get; set; } = new();
    public UISettings UI { get; set; } = new();
}

public class ApplicationSettings
{
    public string Version { get; set; } = "2.0.0";
    public string Name { get; set; } = "University Auto Setup";
    public string Organization { get; set; } = "Appalachian State University";
}

public class BrandingSettings
{
    public string PrimaryColor { get; set; } = "#010101";
    public string AccentColor { get; set; } = "#FFCC00";
    public string SuccessGreen { get; set; } = "#69AA61";
    public string WarningYellow { get; set; } = "#D7A527";
    public string ErrorRed { get; set; } = "#C6602A";
    public string InfoBlue { get; set; } = "#03659C";
    public string LogoPath { get; set; } = "Resources/Images/appstate_logo.png";
    public string ContactEmail { get; set; } = "guillra@appstate.edu";
    public string ContactName { get; set; } = "Alex Guill";
}

public class LoggingSettings
{
    public string LocalPath { get; set; } = @"C:\Temp\UniversityAutoSetup\Logs";
    public string NetworkPath { get; set; } = @"P:\UniversityAutoSetup\Logs";
    public string FolderPattern { get; set; } = "{ServiceTag}_{ComputerName} - {Date:yyyy-MM-dd}";
    public int RetentionDays { get; set; } = 90;
    public string LogLevel { get; set; } = "Information";
}

public class DellCommandUpdateSettings
{
    public string InstallerUNC { get; set; } = @"\\server\share\Dell\DCU\DCU_Setup.exe";
    public string[] InstallPaths { get; set; } = new[]
    {
        @"C:\Program Files\Dell\CommandUpdate\dcu-cli.exe",
        @"C:\Program Files (x86)\Dell\CommandUpdate\dcu-cli.exe"
    };
    public string ConfigureArgs { get; set; } = "/configure -silent -autoSuspendBitLocker=enable -userConsent=disable";
    public string ScanArgs { get; set; } = "/scan -outputLog=\"{LogPath}\"";
    public string UpdateArgs { get; set; } = "/applyUpdates -forceUpdate -reboot=disable -outputLog=\"{LogPath}\"";
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 10;
    public ExitCodesSettings ExitCodes { get; set; } = new();
}

public class ExitCodesSettings
{
    public int[] Retryable { get; set; } = new[] { 5, 7, 8 };
    public int NoUpdates { get; set; } = 1;
    public int[] Success { get; set; } = new[] { 0, 2, 3 };
}

public class GroupPolicySettings
{
    public string ForceArgument { get; set; } = "/force";
    public int MaxRetries { get; set; } = 2;
    public int RetryDelaySeconds { get; set; } = 10;
}

public class SCCMSettings
{
    public bool RepairOnFailure { get; set; } = true;
    public int ActionTimeoutSeconds { get; set; } = 120;
    public SCCMActionConfig[] Actions { get; set; } = new[]
    {
        new SCCMActionConfig { Name = "Machine Policy Retrieval", ID = "{00000000-0000-0000-0000-000000000021}" },
        new SCCMActionConfig { Name = "Machine Policy Evaluation", ID = "{00000000-0000-0000-0000-000000000022}" },
        new SCCMActionConfig { Name = "Hardware Inventory", ID = "{00000000-0000-0000-0000-000000000001}" },
        new SCCMActionConfig { Name = "Software Updates Scan", ID = "{00000000-0000-0000-0000-000000000113}" },
        new SCCMActionConfig { Name = "Software Updates Deployment", ID = "{00000000-0000-0000-0000-000000000108}" }
    };
}

public class SCCMActionConfig
{
    public string Name { get; set; } = string.Empty;
    public string ID { get; set; } = string.Empty;
}

public class ImageCheckSettings
{
    public int MinDiskSpaceGB { get; set; } = 20;
    public string RequiredDomainSuffix { get; set; } = ".appstate.edu";
    public bool BitLockerRequired { get; set; } = true;
}

public class UISettings
{
    public string DefaultTheme { get; set; } = "Light";
    public bool EnableDarkMode { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool AutoExpandTaskDetails { get; set; } = false;
}
