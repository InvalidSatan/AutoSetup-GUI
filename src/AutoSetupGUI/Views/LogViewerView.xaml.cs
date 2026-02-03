using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AutoSetupGUI.Models;
using AutoSetupGUI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AutoSetupGUI.Views;

/// <summary>
/// Log viewer with real-time updates and filtering.
/// </summary>
public partial class LogViewerView : Page
{
    private readonly ILoggingService _loggingService;
    private readonly ObservableCollection<LogEntry> _filteredLogs = new();
    private List<LogEntry> _allLogs = new();

    public LogViewerView()
    {
        InitializeComponent();

        _loggingService = App.Services.GetRequiredService<ILoggingService>();

        LogList.ItemsSource = _filteredLogs;

        _loggingService.LogEntryAdded += LoggingService_LogEntryAdded;

        Loaded += LogViewerView_Loaded;
        Unloaded += LogViewerView_Unloaded;
    }

    private void LogViewerView_Loaded(object sender, RoutedEventArgs e)
    {
        // Load existing logs
        _allLogs = _loggingService.GetLogEntries().ToList();
        ApplyFilter();

        // Update status
        var localPath = _loggingService.GetLocalLogPath();
        TxtLogPath.Text = string.IsNullOrEmpty(localPath) ? "" : $"Log: {localPath}";
    }

    private void LogViewerView_Unloaded(object sender, RoutedEventArgs e)
    {
        _loggingService.LogEntryAdded -= LoggingService_LogEntryAdded;
    }

    private void LoggingService_LogEntryAdded(object? sender, LogEntry e)
    {
        Dispatcher.Invoke(() =>
        {
            _allLogs.Add(e);

            if (ShouldShowEntry(e))
            {
                _filteredLogs.Add(e);
                UpdateCount();

                // Auto-scroll to bottom
                if (_filteredLogs.Count > 0)
                {
                    LogList.ScrollIntoView(_filteredLogs[^1]);
                }
            }
        });
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _filteredLogs.Clear();

        var searchText = TxtSearch.Text?.ToLower() ?? "";

        foreach (var entry in _allLogs)
        {
            if (ShouldShowEntry(entry) && MatchesSearch(entry, searchText))
            {
                _filteredLogs.Add(entry);
            }
        }

        UpdateCount();
    }

    private bool ShouldShowEntry(LogEntry entry)
    {
        return entry.Level switch
        {
            LogLevel.Debug => ChkDebug.IsChecked == true,
            LogLevel.Info => ChkInfo.IsChecked == true,
            LogLevel.Warning => ChkWarning.IsChecked == true,
            LogLevel.Error => ChkError.IsChecked == true,
            LogLevel.Success => ChkSuccess.IsChecked == true,
            LogLevel.Header => true,
            LogLevel.Section => true,
            _ => true
        };
    }

    private bool MatchesSearch(LogEntry entry, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return true;

        return entry.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.Source.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateCount()
    {
        TxtLogCount.Text = $"{_filteredLogs.Count} entries (of {_allLogs.Count} total)";
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var localPath = _loggingService.GetLocalLogPath();
        if (!string.IsNullOrEmpty(localPath) && System.IO.Directory.Exists(localPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = localPath,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("Log folder not available.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _filteredLogs.Clear();
        _allLogs.Clear();
        UpdateCount();
    }
}
