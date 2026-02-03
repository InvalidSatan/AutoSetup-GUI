namespace AutoSetupGUI.Models;

/// <summary>
/// Defines a setup task that can be executed.
/// </summary>
public class TaskDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsSelected { get; set; } = true;
    public int Order { get; set; }
    public List<string> SubTasks { get; set; } = new();
}

/// <summary>
/// Result of executing a task.
/// </summary>
public class TaskResult
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public string DetailedOutput { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    public int? ExitCode { get; set; }
    public bool RequiresRestart { get; set; }
    public List<SubTaskResult> SubTaskResults { get; set; } = new();

    public string DurationFormatted => Duration.HasValue
        ? $"{Duration.Value.Minutes}m {Duration.Value.Seconds}s"
        : "--";
}

/// <summary>
/// Result of a sub-task (e.g., individual SCCM action).
/// </summary>
public class SubTaskResult
{
    public string Name { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Status of a task or sub-task.
/// </summary>
public enum TaskStatus
{
    Pending,
    Running,
    Success,
    Warning,
    Error,
    Skipped
}
