namespace AutoSetupGUI.Models;

/// <summary>
/// Comprehensive system information for asset management.
/// </summary>
public class SystemInfo
{
    // Asset Identification
    public string ServiceTag { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    // BIOS Information
    public string BiosVersion { get; set; } = string.Empty;
    public string BiosReleaseDate { get; set; } = string.Empty;

    // Operating System
    public string OSName { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string OSBuild { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
    public DateTime LastBootTime { get; set; }

    // Network Information
    public string EthernetMac { get; set; } = string.Empty;
    public string WifiMac { get; set; } = string.Empty;
    public List<NetworkAdapterInfo> NetworkAdapters { get; set; } = new();

    // Domain Information
    public string DomainName { get; set; } = string.Empty;
    public bool IsDomainJoined { get; set; }
    public string OrganizationalUnit { get; set; } = string.Empty;

    // Hardware Specifications
    public string ProcessorName { get; set; } = string.Empty;
    public int ProcessorCores { get; set; }
    public int ProcessorLogicalProcessors { get; set; }
    public long TotalRAMBytes { get; set; }
    public string TotalRAMFormatted => FormatBytes(TotalRAMBytes);
    public List<DiskInfo> Disks { get; set; } = new();

    // SCCM Information
    public string SCCMClientVersion { get; set; } = string.Empty;
    public string SCCMSiteCode { get; set; } = string.Empty;
    public string SCCMManagementPoint { get; set; } = string.Empty;
    public bool SCCMClientInstalled { get; set; }

    // Timestamps
    public DateTime CollectedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Formats the system info for clipboard copying (ticket-friendly format).
    /// </summary>
    public string ToClipboardFormat()
    {
        return $"""
            Service Tag: {ServiceTag}
            Computer Name: {ComputerName}
            Manufacturer: {Manufacturer}
            Model: {Model}
            BIOS Version: {BiosVersion}
            OS: {OSName} ({OSBuild})
            Ethernet MAC: {EthernetMac}
            WiFi MAC: {WifiMac}
            Domain: {DomainName}
            Processor: {ProcessorName}
            RAM: {TotalRAMFormatted}
            SCCM Client: {(SCCMClientInstalled ? SCCMClientVersion : "Not Installed")}
            """;
    }

    /// <summary>
    /// Formats the system info for detailed display.
    /// </summary>
    public string ToDetailedFormat()
    {
        var diskInfo = string.Join("\n", Disks.Select(d =>
            $"  {d.DriveLetter}: {d.FreeSpaceFormatted} free of {d.TotalSizeFormatted}"));

        return $"""
            ═══════════════════════════════════════════════════════════════
                               SYSTEM INFORMATION
            ═══════════════════════════════════════════════════════════════

            ASSET IDENTIFICATION
            ─────────────────────────────────────────────────────────────────
            Service Tag:     {ServiceTag}
            Computer Name:   {ComputerName}
            Manufacturer:    {Manufacturer}
            Model:           {Model}

            BIOS
            ─────────────────────────────────────────────────────────────────
            Version:         {BiosVersion}
            Release Date:    {BiosReleaseDate}

            OPERATING SYSTEM
            ─────────────────────────────────────────────────────────────────
            Name:            {OSName}
            Version:         {OSVersion}
            Build:           {OSBuild}
            Architecture:    {OSArchitecture}
            Last Boot:       {LastBootTime:yyyy-MM-dd HH:mm:ss}

            NETWORK
            ─────────────────────────────────────────────────────────────────
            Ethernet MAC:    {EthernetMac}
            WiFi MAC:        {WifiMac}

            DOMAIN
            ─────────────────────────────────────────────────────────────────
            Domain:          {DomainName}
            Domain Joined:   {(IsDomainJoined ? "Yes" : "No")}
            OU:              {OrganizationalUnit}

            HARDWARE
            ─────────────────────────────────────────────────────────────────
            Processor:       {ProcessorName}
            Cores:           {ProcessorCores} ({ProcessorLogicalProcessors} logical)
            Total RAM:       {TotalRAMFormatted}

            STORAGE
            ─────────────────────────────────────────────────────────────────
            {diskInfo}

            SCCM CLIENT
            ─────────────────────────────────────────────────────────────────
            Installed:       {(SCCMClientInstalled ? "Yes" : "No")}
            Version:         {SCCMClientVersion}
            Site Code:       {SCCMSiteCode}
            Management Point: {SCCMManagementPoint}

            ═══════════════════════════════════════════════════════════════
            Collected: {CollectedAt:yyyy-MM-dd HH:mm:ss}
            ═══════════════════════════════════════════════════════════════
            """;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Network adapter information.
/// </summary>
public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string AdapterType { get; set; } = string.Empty; // Ethernet, WiFi, etc.
}

/// <summary>
/// Disk drive information.
/// </summary>
public class DiskInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public string VolumeName { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public long FreeSpaceBytes { get; set; }
    public long UsedSpaceBytes => TotalSizeBytes - FreeSpaceBytes;
    public double UsedPercentage => TotalSizeBytes > 0 ? (double)UsedSpaceBytes / TotalSizeBytes * 100 : 0;

    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);
    public string FreeSpaceFormatted => FormatBytes(FreeSpaceBytes);
    public string UsedSpaceFormatted => FormatBytes(UsedSpaceBytes);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
