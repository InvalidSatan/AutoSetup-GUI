using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AutoSetupGUI.Views;

/// <summary>
/// A simple PDF report viewer window that avoids Edge's first-run experience.
/// Shows the report details and provides buttons to open externally.
/// </summary>
public partial class PdfViewerWindow : Window
{
    private readonly string _filePath;
    private readonly bool _requiresRestart;

    public PdfViewerWindow(string filePath, bool requiresRestart = false)
    {
        InitializeComponent();

        _filePath = filePath;
        _requiresRestart = requiresRestart;

        TxtFilePath.Text = filePath;

        // Show restart panel if needed
        if (requiresRestart)
        {
            RestartPanel.Visibility = Visibility.Visible;
        }

        // Show report info
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists)
        {
            TxtReportInfo.Text = $"Report saved to:\n{filePath}\n\nSize: {fileInfo.Length / 1024.0:F1} KB\nGenerated: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}";
        }
        else
        {
            TxtReportInfo.Text = $"Report path:\n{filePath}";
        }

        // Try to find a non-Edge PDF reader
        var pdfReader = FindPdfReader();
        if (!string.IsNullOrEmpty(pdfReader))
        {
            TxtHint.Text = $"PDF will open with: {Path.GetFileNameWithoutExtension(pdfReader)}";
        }
    }

    /// <summary>
    /// Find a PDF reader that isn't Edge.
    /// </summary>
    private static string? FindPdfReader()
    {
        var possiblePaths = new[]
        {
            // Adobe Acrobat Reader DC
            @"C:\Program Files\Adobe\Acrobat DC\Acrobat\Acrobat.exe",
            @"C:\Program Files (x86)\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe",
            @"C:\Program Files\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe",

            // Foxit Reader
            @"C:\Program Files\Foxit Software\Foxit PDF Reader\FoxitPDFReader.exe",
            @"C:\Program Files (x86)\Foxit Software\Foxit Reader\FoxitReader.exe",

            // SumatraPDF
            @"C:\Program Files\SumatraPDF\SumatraPDF.exe",
            @"C:\Program Files (x86)\SumatraPDF\SumatraPDF.exe",

            // PDF-XChange
            @"C:\Program Files\Tracker Software\PDF Editor\PDFXEdit.exe",
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                MessageBox.Show($"Report file not found:\n{_filePath}", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Try to use a non-Edge PDF reader first
            var pdfReader = FindPdfReader();
            if (!string.IsNullOrEmpty(pdfReader))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = pdfReader,
                    Arguments = $"\"{_filePath}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                // Fall back to shell execute (will use default PDF handler, likely Edge)
                Process.Start(new ProcessStartInfo
                {
                    FileName = _filePath,
                    UseShellExecute = true
                });
            }
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

    private void BtnRestartNow_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to restart the computer now?\n\n" +
            "Make sure all work is saved before restarting.",
            "Confirm Restart",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Initiate restart with 30 second delay and message
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 30 /c \"Restarting to complete Dell updates...\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                // Close the viewer and application
                Close();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initiating restart:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
