using System.Management;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Helper class for Windows Management Instrumentation (WMI) queries.
/// </summary>
public class WmiHelper
{
    private readonly ILogger<WmiHelper> _logger;

    public WmiHelper(ILogger<WmiHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Queries a WMI class and returns all instances.
    /// </summary>
    public IEnumerable<ManagementObject> Query(string wmiClass, string? wmiNamespace = null)
    {
        var scope = wmiNamespace ?? "root\\cimv2";
        var query = $"SELECT * FROM {wmiClass}";

        _logger.LogDebug("Executing WMI query: {Query} in namespace {Namespace}", query, scope);

        List<ManagementObject> results;
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            results = searcher.Get().Cast<ManagementObject>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing WMI query: {Query}", query);
            throw;
        }

        foreach (var obj in results)
        {
            yield return obj;
        }
    }

    /// <summary>
    /// Queries a WMI class with a WHERE clause.
    /// </summary>
    public IEnumerable<ManagementObject> QueryWhere(string wmiClass, string whereClause, string? wmiNamespace = null)
    {
        var scope = wmiNamespace ?? "root\\cimv2";
        var query = $"SELECT * FROM {wmiClass} WHERE {whereClause}";

        _logger.LogDebug("Executing WMI query: {Query} in namespace {Namespace}", query, scope);

        List<ManagementObject> results;
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            results = searcher.Get().Cast<ManagementObject>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing WMI query: {Query}", query);
            throw;
        }

        foreach (var obj in results)
        {
            yield return obj;
        }
    }

    /// <summary>
    /// Gets a single property value from a WMI class.
    /// </summary>
    public T? GetPropertyValue<T>(string wmiClass, string propertyName, string? wmiNamespace = null)
    {
        try
        {
            foreach (var obj in Query(wmiClass, wmiNamespace))
            {
                using (obj)
                {
                    var value = obj[propertyName];
                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting property {Property} from {Class}", propertyName, wmiClass);
        }

        return default;
    }

    /// <summary>
    /// Gets BIOS information.
    /// </summary>
    public (string SerialNumber, string Version, string ReleaseDate) GetBiosInfo()
    {
        try
        {
            foreach (var obj in Query("Win32_BIOS"))
            {
                using (obj)
                {
                    var serialNumber = obj["SerialNumber"]?.ToString() ?? "Unknown";
                    var version = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                    var releaseDate = obj["ReleaseDate"]?.ToString() ?? "Unknown";

                    // Parse WMI datetime format
                    if (releaseDate.Length >= 8)
                    {
                        releaseDate = $"{releaseDate.Substring(0, 4)}-{releaseDate.Substring(4, 2)}-{releaseDate.Substring(6, 2)}";
                    }

                    return (serialNumber, version, releaseDate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BIOS info");
        }

        return ("Unknown", "Unknown", "Unknown");
    }

    /// <summary>
    /// Gets computer system information.
    /// </summary>
    public (string Manufacturer, string Model, string Domain, bool PartOfDomain) GetComputerSystemInfo()
    {
        try
        {
            foreach (var obj in Query("Win32_ComputerSystem"))
            {
                using (obj)
                {
                    var manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                    var model = obj["Model"]?.ToString() ?? "Unknown";
                    var domain = obj["Domain"]?.ToString() ?? "WORKGROUP";
                    var partOfDomain = (bool)(obj["PartOfDomain"] ?? false);

                    return (manufacturer, model, domain, partOfDomain);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting computer system info");
        }

        return ("Unknown", "Unknown", "WORKGROUP", false);
    }

    /// <summary>
    /// Gets operating system information.
    /// </summary>
    public (string Caption, string Version, string BuildNumber, string Architecture, DateTime LastBootTime) GetOperatingSystemInfo()
    {
        try
        {
            foreach (var obj in Query("Win32_OperatingSystem"))
            {
                using (obj)
                {
                    var caption = obj["Caption"]?.ToString() ?? "Unknown";
                    var version = obj["Version"]?.ToString() ?? "Unknown";
                    var buildNumber = obj["BuildNumber"]?.ToString() ?? "Unknown";
                    var architecture = obj["OSArchitecture"]?.ToString() ?? "Unknown";

                    var lastBootStr = obj["LastBootUpTime"]?.ToString();
                    var lastBoot = DateTime.Now;

                    if (!string.IsNullOrEmpty(lastBootStr))
                    {
                        lastBoot = ManagementDateTimeConverter.ToDateTime(lastBootStr);
                    }

                    return (caption, version, buildNumber, architecture, lastBoot);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OS info");
        }

        return ("Unknown", "Unknown", "Unknown", "Unknown", DateTime.Now);
    }

    /// <summary>
    /// Gets processor information.
    /// </summary>
    public (string Name, int Cores, int LogicalProcessors) GetProcessorInfo()
    {
        try
        {
            foreach (var obj in Query("Win32_Processor"))
            {
                using (obj)
                {
                    var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    var cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    var logicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);

                    return (name, cores, logicalProcessors);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processor info");
        }

        return ("Unknown", 0, 0);
    }

    /// <summary>
    /// Gets total physical memory.
    /// </summary>
    public long GetTotalPhysicalMemory()
    {
        try
        {
            long total = 0;
            foreach (var obj in Query("Win32_PhysicalMemory"))
            {
                using (obj)
                {
                    total += Convert.ToInt64(obj["Capacity"] ?? 0);
                }
            }
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting physical memory");
            return 0;
        }
    }

    /// <summary>
    /// Gets network adapter information.
    /// </summary>
    public IEnumerable<(string Name, string MacAddress, bool IsPhysical)> GetNetworkAdapters()
    {
        var results = new List<(string, string, bool)>();

        try
        {
            foreach (var obj in Query("Win32_NetworkAdapter"))
            {
                using (obj)
                {
                    var macAddress = obj["MACAddress"]?.ToString();
                    if (string.IsNullOrEmpty(macAddress))
                        continue;

                    var name = obj["Name"]?.ToString() ?? "Unknown Adapter";
                    var isPhysical = (bool)(obj["PhysicalAdapter"] ?? false);

                    // Filter for likely real adapters
                    if (isPhysical &&
                        (name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Realtek", StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add((name, macAddress, isPhysical));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network adapters");
        }

        return results;
    }

    /// <summary>
    /// Gets logical disk information.
    /// </summary>
    public IEnumerable<(string DriveLetter, string VolumeName, string FileSystem, long Size, long FreeSpace)> GetLogicalDisks()
    {
        var results = new List<(string, string, string, long, long)>();

        try
        {
            // DriveType 3 = Local Disk
            foreach (var obj in QueryWhere("Win32_LogicalDisk", "DriveType = 3"))
            {
                using (obj)
                {
                    var driveLetter = obj["DeviceID"]?.ToString() ?? "?:";
                    var volumeName = obj["VolumeName"]?.ToString() ?? "";
                    var fileSystem = obj["FileSystem"]?.ToString() ?? "Unknown";
                    var size = Convert.ToInt64(obj["Size"] ?? 0);
                    var freeSpace = Convert.ToInt64(obj["FreeSpace"] ?? 0);

                    results.Add((driveLetter, volumeName, fileSystem, size, freeSpace));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logical disks");
        }

        return results;
    }

    /// <summary>
    /// Invokes a WMI method.
    /// </summary>
    public object? InvokeMethod(string wmiClass, string methodName, Dictionary<string, object>? parameters = null, string? wmiNamespace = null)
    {
        var scope = wmiNamespace ?? "root\\cimv2";

        _logger.LogDebug("Invoking WMI method: {Class}.{Method} in namespace {Namespace}", wmiClass, methodName, scope);

        try
        {
            using var managementClass = new ManagementClass(scope, wmiClass, null);
            using var inParams = managementClass.GetMethodParameters(methodName);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    inParams[param.Key] = param.Value;
                }
            }

            using var outParams = managementClass.InvokeMethod(methodName, inParams, null);
            return outParams?["ReturnValue"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking WMI method: {Class}.{Method}", wmiClass, methodName);
            throw;
        }
    }

    /// <summary>
    /// Checks if a WMI namespace is accessible.
    /// </summary>
    public bool IsNamespaceAccessible(string wmiNamespace)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wmiNamespace, "SELECT * FROM __NAMESPACE");
            searcher.Get();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
