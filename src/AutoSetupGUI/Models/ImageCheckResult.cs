namespace AutoSetupGUI.Models;

/// <summary>
/// Comprehensive image verification check results.
/// </summary>
public class ImageCheckResult
{
    public List<ImageCheck> Checks { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public int PassedCount => Checks.Count(c => c.Passed);
    public int FailedCount => Checks.Count(c => !c.Passed);
    public int TotalCount => Checks.Count;
    public bool AllPassed => Checks.All(c => c.Passed);
    public double PassPercentage => TotalCount > 0 ? (double)PassedCount / TotalCount * 100 : 0;
}

/// <summary>
/// Individual image verification check.
/// </summary>
public class ImageCheck
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public ImageCheckSeverity Severity { get; set; } = ImageCheckSeverity.Warning;

    public static ImageCheck WindowsActivation(bool activated, string status)
    {
        return new ImageCheck
        {
            Id = "windows_activation",
            Name = "Windows Activation",
            Category = "Operating System",
            Description = "Verifies Windows is properly activated",
            Passed = activated,
            Status = status,
            Details = activated ? "Windows is activated" : "Windows is not activated",
            Recommendation = activated ? string.Empty : "Activate Windows using the organization's KMS server",
            Severity = ImageCheckSeverity.Error
        };
    }

    public static ImageCheck DomainJoin(bool joined, string domainName)
    {
        return new ImageCheck
        {
            Id = "domain_join",
            Name = "Domain Membership",
            Category = "Network",
            Description = "Verifies the computer is joined to the domain",
            Passed = joined,
            Status = joined ? $"Joined to {domainName}" : "Not joined to domain",
            Details = joined ? $"Computer is a member of {domainName}" : "Computer is in workgroup mode",
            Recommendation = joined ? string.Empty : "Join the computer to the domain using System Properties",
            Severity = ImageCheckSeverity.Error
        };
    }

    public static ImageCheck SCCMClient(bool healthy, string version, string healthMessage)
    {
        return new ImageCheck
        {
            Id = "sccm_client",
            Name = "SCCM Client Health",
            Category = "Management",
            Description = "Verifies SCCM client is installed and healthy",
            Passed = healthy,
            Status = healthy ? $"Healthy (v{version})" : "Unhealthy",
            Details = healthMessage,
            Recommendation = healthy ? string.Empty : "Repair the SCCM client or contact IT support",
            Severity = ImageCheckSeverity.Error
        };
    }

    public static ImageCheck DiskSpace(bool sufficient, long freeSpaceGB, long requiredGB)
    {
        return new ImageCheck
        {
            Id = "disk_space",
            Name = "Disk Space",
            Category = "Storage",
            Description = $"Verifies at least {requiredGB}GB of free disk space",
            Passed = sufficient,
            Status = $"{freeSpaceGB}GB free",
            Details = sufficient
                ? $"System drive has {freeSpaceGB}GB free (minimum: {requiredGB}GB)"
                : $"Only {freeSpaceGB}GB free (minimum: {requiredGB}GB required)",
            Recommendation = sufficient ? string.Empty : "Free up disk space by removing unnecessary files",
            Severity = ImageCheckSeverity.Warning
        };
    }

    public static ImageCheck NetworkConnectivity(bool connected, bool pDriveAvailable)
    {
        var message = connected && pDriveAvailable
            ? "Connected with P:\\ drive access"
            : connected
                ? "Connected but P:\\ drive not available"
                : "No network connectivity";

        return new ImageCheck
        {
            Id = "network",
            Name = "Network Connectivity",
            Category = "Network",
            Description = "Verifies network connectivity and P:\\ drive access",
            Passed = connected,
            Status = message,
            Details = $"Internet: {(connected ? "Yes" : "No")}, P:\\ Drive: {(pDriveAvailable ? "Yes" : "No")}",
            Recommendation = connected ? string.Empty : "Check network cable and DHCP configuration",
            Severity = connected ? (pDriveAvailable ? ImageCheckSeverity.Info : ImageCheckSeverity.Warning) : ImageCheckSeverity.Error
        };
    }

    public static ImageCheck PendingReboot(bool pending, string reason)
    {
        return new ImageCheck
        {
            Id = "pending_reboot",
            Name = "Pending Reboot",
            Category = "System",
            Description = "Checks for pending system restarts",
            Passed = !pending,
            Status = pending ? "Reboot required" : "No reboot pending",
            Details = pending ? $"Reason: {reason}" : "System is up to date",
            Recommendation = pending ? "Restart the computer before continuing" : string.Empty,
            Severity = ImageCheckSeverity.Warning
        };
    }

    public static ImageCheck BitLocker(bool enabled, string protectionStatus)
    {
        return new ImageCheck
        {
            Id = "bitlocker",
            Name = "BitLocker Encryption",
            Category = "Security",
            Description = "Verifies BitLocker drive encryption is enabled",
            Passed = enabled,
            Status = protectionStatus,
            Details = enabled ? "System drive is encrypted with BitLocker" : "BitLocker is not enabled",
            Recommendation = enabled ? string.Empty : "Enable BitLocker encryption through Group Policy or manually",
            Severity = ImageCheckSeverity.Warning
        };
    }
}

/// <summary>
/// Severity level for image checks.
/// </summary>
public enum ImageCheckSeverity
{
    Info,
    Warning,
    Error
}
