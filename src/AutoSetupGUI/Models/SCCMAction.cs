namespace AutoSetupGUI.Models;

/// <summary>
/// Defines an SCCM client action.
/// </summary>
public class SCCMAction
{
    public string Name { get; set; } = string.Empty;
    public string ScheduleId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public SCCMAction() { }

    public SCCMAction(string name, string scheduleId, string description = "")
    {
        Name = name;
        ScheduleId = scheduleId;
        Description = description;
    }

    /// <summary>
    /// Default SCCM actions used in the University Auto Setup tool.
    /// </summary>
    public static SCCMAction[] DefaultActions => new[]
    {
        new SCCMAction(
            "Machine Policy Retrieval",
            "{00000000-0000-0000-0000-000000000021}",
            "Downloads the latest machine policy from SCCM"
        ),
        new SCCMAction(
            "Machine Policy Evaluation",
            "{00000000-0000-0000-0000-000000000022}",
            "Evaluates downloaded machine policies"
        ),
        new SCCMAction(
            "Hardware Inventory",
            "{00000000-0000-0000-0000-000000000001}",
            "Collects hardware inventory and sends to SCCM"
        ),
        new SCCMAction(
            "Software Updates Scan",
            "{00000000-0000-0000-0000-000000000113}",
            "Scans for applicable software updates"
        ),
        new SCCMAction(
            "Software Updates Deployment",
            "{00000000-0000-0000-0000-000000000108}",
            "Evaluates software update deployments"
        )
    };
}

/// <summary>
/// Result of executing an SCCM action.
/// </summary>
public class SCCMActionResult
{
    public SCCMAction Action { get; set; } = new();
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public uint? ReturnValue { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// SCCM client health status.
/// </summary>
public class SCCMClientHealth
{
    public bool IsInstalled { get; set; }
    public bool IsServiceRunning { get; set; }
    public bool IsWmiAccessible { get; set; }
    public string ClientVersion { get; set; } = string.Empty;
    public string SiteCode { get; set; } = string.Empty;
    public string ManagementPoint { get; set; } = string.Empty;
    public string ServiceStatus { get; set; } = string.Empty;
    public bool OverallHealthy => IsInstalled && IsServiceRunning && IsWmiAccessible;
    public string HealthMessage { get; set; } = string.Empty;
}
