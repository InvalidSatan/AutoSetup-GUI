using System.IO;
using System.Text.Json;
using System.Windows;
using AutoSetupGUI.Infrastructure;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Services.Implementations;

/// <summary>
/// Service for collecting comprehensive system information.
/// </summary>
public class SystemInfoService : ISystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;
    private readonly WmiHelper _wmiHelper;
    private readonly RegistryHelper _registryHelper;

    public SystemInfoService(
        ILogger<SystemInfoService> logger,
        WmiHelper wmiHelper,
        RegistryHelper registryHelper)
    {
        _logger = logger;
        _wmiHelper = wmiHelper;
        _registryHelper = registryHelper;
    }

    public async Task<SystemInfo> CollectSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Collecting system information...");

        var info = new SystemInfo
        {
            ComputerName = Environment.MachineName,
            CollectedAt = DateTime.Now
        };

        await Task.Run(() =>
        {
            // BIOS Information
            var (serialNumber, biosVersion, releaseDate) = _wmiHelper.GetBiosInfo();
            info.ServiceTag = serialNumber;
            info.BiosVersion = biosVersion;
            info.BiosReleaseDate = releaseDate;

            // Computer System Information
            var (manufacturer, model, domain, partOfDomain) = _wmiHelper.GetComputerSystemInfo();
            info.Manufacturer = manufacturer;
            info.Model = model;
            info.DomainName = domain;
            info.IsDomainJoined = partOfDomain;

            // Operating System Information
            var (osName, osVersion, osBuild, osArch, lastBoot) = _wmiHelper.GetOperatingSystemInfo();
            info.OSName = osName;
            info.OSVersion = osVersion;
            info.OSBuild = osBuild;
            info.OSArchitecture = osArch;
            info.LastBootTime = lastBoot;

            // Processor Information
            var (procName, cores, logicalProcs) = _wmiHelper.GetProcessorInfo();
            info.ProcessorName = procName;
            info.ProcessorCores = cores;
            info.ProcessorLogicalProcessors = logicalProcs;

            // Memory
            info.TotalRAMBytes = _wmiHelper.GetTotalPhysicalMemory();

            // Network Adapters
            CollectNetworkInfo(info);

            // Disks
            CollectDiskInfo(info);

            // SCCM Information
            CollectSCCMInfo(info);

        }, cancellationToken);

        _logger.LogInformation("System information collected successfully");
        return info;
    }

    private void CollectNetworkInfo(SystemInfo info)
    {
        try
        {
            var adapters = _wmiHelper.GetNetworkAdapters();

            foreach (var (name, mac, isPhysical) in adapters)
            {
                info.NetworkAdapters.Add(new NetworkAdapterInfo
                {
                    Name = name,
                    MacAddress = mac,
                    IsEnabled = isPhysical,
                    AdapterType = name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) ? "Ethernet" :
                                  name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                                  name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ? "WiFi" : "Other"
                });

                if (string.IsNullOrEmpty(info.EthernetMac) &&
                    name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
                {
                    info.EthernetMac = mac;
                }
                else if (string.IsNullOrEmpty(info.WifiMac) &&
                         (name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)))
                {
                    info.WifiMac = mac;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting network information");
        }
    }

    private void CollectDiskInfo(SystemInfo info)
    {
        try
        {
            var disks = _wmiHelper.GetLogicalDisks();

            foreach (var (driveLetter, volumeName, fileSystem, size, freeSpace) in disks)
            {
                info.Disks.Add(new DiskInfo
                {
                    DriveLetter = driveLetter,
                    VolumeName = volumeName,
                    FileSystem = fileSystem,
                    TotalSizeBytes = size,
                    FreeSpaceBytes = freeSpace
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting disk information");
        }
    }

    private void CollectSCCMInfo(SystemInfo info)
    {
        try
        {
            var (siteCode, mp, version) = _registryHelper.GetSCCMInfo();
            info.SCCMSiteCode = siteCode;
            info.SCCMManagementPoint = mp;
            info.SCCMClientVersion = version;
            info.SCCMClientInstalled = !string.IsNullOrEmpty(version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting SCCM information");
        }
    }

    public string GetServiceTag()
    {
        var (serialNumber, _, _) = _wmiHelper.GetBiosInfo();
        return serialNumber;
    }

    public string GetComputerName()
    {
        return Environment.MachineName;
    }

    public void CopyToClipboard(SystemInfo info)
    {
        try
        {
            Clipboard.SetText(info.ToClipboardFormat());
            _logger.LogDebug("System info copied to clipboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying to clipboard");
            throw;
        }
    }

    public async Task ExportToFileAsync(SystemInfo info, string filePath, ExportFormat format)
    {
        try
        {
            string content = format switch
            {
                ExportFormat.Json => JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }),
                ExportFormat.Csv => GenerateCsv(info),
                ExportFormat.Text => info.ToDetailedFormat(),
                _ => info.ToDetailedFormat()
            };

            await File.WriteAllTextAsync(filePath, content);
            _logger.LogInformation("System info exported to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting system info to {FilePath}", filePath);
            throw;
        }
    }

    private static string GenerateCsv(SystemInfo info)
    {
        var lines = new List<string>
        {
            "Property,Value",
            $"Service Tag,{info.ServiceTag}",
            $"Computer Name,{info.ComputerName}",
            $"Manufacturer,{info.Manufacturer}",
            $"Model,{info.Model}",
            $"BIOS Version,{info.BiosVersion}",
            $"OS Name,\"{info.OSName}\"",
            $"OS Version,{info.OSVersion}",
            $"OS Build,{info.OSBuild}",
            $"OS Architecture,{info.OSArchitecture}",
            $"Ethernet MAC,{info.EthernetMac}",
            $"WiFi MAC,{info.WifiMac}",
            $"Domain,{info.DomainName}",
            $"Domain Joined,{info.IsDomainJoined}",
            $"Processor,\"{info.ProcessorName}\"",
            $"Processor Cores,{info.ProcessorCores}",
            $"Total RAM,{info.TotalRAMFormatted}",
            $"SCCM Client Version,{info.SCCMClientVersion}",
            $"SCCM Site Code,{info.SCCMSiteCode}"
        };

        return string.Join(Environment.NewLine, lines);
    }
}
