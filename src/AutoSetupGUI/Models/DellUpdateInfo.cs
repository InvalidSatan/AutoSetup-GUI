using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AutoSetupGUI.Models;

/// <summary>
/// Represents a single Dell update with its progress status.
/// </summary>
public class DellUpdateInfo : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _version = string.Empty;
    private string _category = string.Empty;
    private long _sizeBytes;
    private DellUpdateStatus _status = DellUpdateStatus.Pending;
    private int _downloadProgress;
    private int _installProgress;
    private string _statusMessage = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Version
    {
        get => _version;
        set { _version = value; OnPropertyChanged(); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set { _sizeBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeFormatted)); }
    }

    public string SizeFormatted => SizeBytes switch
    {
        >= 1073741824 => $"{SizeBytes / 1073741824.0:F1} GB",
        >= 1048576 => $"{SizeBytes / 1048576.0:F1} MB",
        >= 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes} B"
    };

    public DellUpdateStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(StatusForeground));
            OnPropertyChanged(nameof(IsInProgress));
            OnPropertyChanged(nameof(OverallProgress));
        }
    }

    public int DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverallProgress)); }
    }

    public int InstallProgress
    {
        get => _installProgress;
        set { _installProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverallProgress)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    // Computed properties for UI binding
    public string StatusText => Status switch
    {
        DellUpdateStatus.Pending => "Pending",
        DellUpdateStatus.Downloading => $"Downloading {DownloadProgress}%",
        DellUpdateStatus.Downloaded => "Downloaded",
        DellUpdateStatus.Installing => $"Installing {InstallProgress}%",
        DellUpdateStatus.Installed => "Installed",
        DellUpdateStatus.Failed => "Failed",
        DellUpdateStatus.Skipped => "Skipped",
        _ => "Unknown"
    };

    public bool IsInProgress => Status == DellUpdateStatus.Downloading || Status == DellUpdateStatus.Installing;

    public int OverallProgress => Status switch
    {
        DellUpdateStatus.Pending => 0,
        DellUpdateStatus.Downloading => DownloadProgress / 2,
        DellUpdateStatus.Downloaded => 50,
        DellUpdateStatus.Installing => 50 + (InstallProgress / 2),
        DellUpdateStatus.Installed => 100,
        DellUpdateStatus.Failed => 0,
        DellUpdateStatus.Skipped => 0,
        _ => 0
    };

    public Brush StatusBackground => Status switch
    {
        DellUpdateStatus.Installed => new SolidColorBrush(Color.FromRgb(232, 245, 232)),
        DellUpdateStatus.Failed => new SolidColorBrush(Color.FromRgb(255, 235, 232)),
        DellUpdateStatus.Downloading or DellUpdateStatus.Installing => new SolidColorBrush(Color.FromRgb(225, 240, 250)),
        DellUpdateStatus.Downloaded => new SolidColorBrush(Color.FromRgb(240, 248, 255)),
        DellUpdateStatus.Skipped => new SolidColorBrush(Color.FromRgb(255, 248, 225)),
        _ => new SolidColorBrush(Color.FromRgb(240, 240, 240))
    };

    public Brush StatusForeground => Status switch
    {
        DellUpdateStatus.Installed => new SolidColorBrush(Color.FromRgb(105, 170, 97)),
        DellUpdateStatus.Failed => new SolidColorBrush(Color.FromRgb(198, 96, 42)),
        DellUpdateStatus.Downloading or DellUpdateStatus.Installing => new SolidColorBrush(Color.FromRgb(3, 101, 156)),
        DellUpdateStatus.Downloaded => new SolidColorBrush(Color.FromRgb(70, 130, 180)),
        DellUpdateStatus.Skipped => new SolidColorBrush(Color.FromRgb(215, 165, 39)),
        _ => new SolidColorBrush(Color.FromRgb(76, 72, 71))
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Status of a Dell update operation.
/// </summary>
public enum DellUpdateStatus
{
    Pending,
    Downloading,
    Downloaded,
    Installing,
    Installed,
    Failed,
    Skipped
}
