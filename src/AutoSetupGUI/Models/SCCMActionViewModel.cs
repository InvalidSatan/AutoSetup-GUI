using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AutoSetupGUI.Models;

/// <summary>
/// Observable view model for SCCM action display in the UI.
/// </summary>
public class SCCMActionViewModel : INotifyPropertyChanged
{
    private TaskStatus _status = TaskStatus.Pending;
    private string _statusText = "Pending";

    public SCCMAction Action { get; }

    public string Name => Action.Name;
    public string ScheduleId => Action.ScheduleId;

    public TaskStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public SolidColorBrush StatusBackground
    {
        get
        {
            var color = Status switch
            {
                TaskStatus.Success => Color.FromRgb(232, 245, 232),   // Light green
                TaskStatus.Warning => Color.FromRgb(255, 248, 225),   // Light gold
                TaskStatus.Error => Color.FromRgb(255, 235, 232),     // Light red
                TaskStatus.Running => Color.FromRgb(225, 240, 250),   // Light blue
                _ => Color.FromRgb(240, 240, 240)                     // Light gray
            };
            return new SolidColorBrush(color);
        }
    }

    public SolidColorBrush StatusForeground
    {
        get
        {
            var color = Status switch
            {
                TaskStatus.Success => Color.FromRgb(105, 170, 97),    // Grass Green
                TaskStatus.Warning => Color.FromRgb(215, 165, 39),    // Dark Gold
                TaskStatus.Error => Color.FromRgb(198, 96, 42),       // Brick Orange
                TaskStatus.Running => Color.FromRgb(3, 101, 156),     // Lake Blue
                _ => Color.FromRgb(76, 72, 71)                        // Dark Gray
            };
            return new SolidColorBrush(color);
        }
    }

    public SCCMActionViewModel(SCCMAction action)
    {
        Action = action;
    }

    /// <summary>
    /// Updates the status from an SCCMActionResult.
    /// </summary>
    public void UpdateFromResult(SCCMActionResult result)
    {
        Status = result.Status;
        StatusText = result.Status switch
        {
            TaskStatus.Success => "Success",
            TaskStatus.Warning => result.Message ?? "Warning",
            TaskStatus.Error => result.Message ?? "Error",
            TaskStatus.Running => "Running...",
            _ => "Pending"
        };
    }

    /// <summary>
    /// Resets the status to pending.
    /// </summary>
    public void Reset()
    {
        Status = TaskStatus.Pending;
        StatusText = "Pending";
    }

    /// <summary>
    /// Sets the status to running.
    /// </summary>
    public void SetRunning()
    {
        Status = TaskStatus.Running;
        StatusText = "Running...";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
