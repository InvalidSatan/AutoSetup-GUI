using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AutoSetupGUI.Views;

/// <summary>
/// A simple completion dialog that shows report options.
/// Opens the report externally to avoid file locking issues.
/// </summary>
public partial class PdfViewerWindow : Window
{
    private readonly string _filePath;

    public PdfViewerWindow(string filePath, bool requiresRestart = false)
    {
        InitializeComponent();

        _filePath = filePath;

        // Show restart panel if needed
        if (requiresRestart)
        {
            RestartPanel.Visibility = Visibility.Visible;
        }

        // Show report location info
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists)
        {
            var folderPath = Path.GetDirectoryName(filePath) ?? "";
            TxtReportInfo.Text = $"Report saved to:\n{folderPath}";
        }
        else
        {
            TxtReportInfo.Text = "Report generated.";
        }
    }

    private void BtnOpenReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                MessageBox.Show($"Report file not found:\n{_filePath}", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Use shell execute to open with default PDF handler
            Process.Start(new ProcessStartInfo
            {
                FileName = _filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening report:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                // Open folder and select the file
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_filePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening folder:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
