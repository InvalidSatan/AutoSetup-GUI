using AutoSetupGUI.ViewModels.Base;
using CommunityToolkit.Mvvm.Input;

namespace AutoSetupGUI.ViewModels;

/// <summary>
/// Main view model for the application shell.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    [RelayCommand]
    private void Refresh()
    {
        // Refresh current view data
    }

    [RelayCommand]
    private void RunAll()
    {
        // Run all selected tasks
    }

    [RelayCommand]
    private void CopyInfo()
    {
        // Copy system info to clipboard
    }

    [RelayCommand]
    private void SaveLogs()
    {
        // Export logs
    }

    [RelayCommand]
    private void Close()
    {
        System.Windows.Application.Current.Shutdown();
    }
}
