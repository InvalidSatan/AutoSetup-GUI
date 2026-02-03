using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoSetupGUI.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels in the application.
/// Uses CommunityToolkit.Mvvm for MVVM implementation.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Called when the view is loaded/navigated to.
    /// Override to perform initialization logic.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the view is unloaded/navigated away from.
    /// Override to perform cleanup logic.
    /// </summary>
    public virtual Task CleanupAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the busy state with an optional message.
    /// </summary>
    protected void SetBusy(string message = "Working...")
    {
        IsBusy = true;
        StatusMessage = message;
    }

    /// <summary>
    /// Clears the busy state.
    /// </summary>
    protected void ClearBusy()
    {
        IsBusy = false;
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Sets an error state with a message.
    /// </summary>
    protected void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    /// <summary>
    /// Clears the error state.
    /// </summary>
    protected void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    /// <summary>
    /// Executes an action with busy state management and error handling.
    /// </summary>
    protected async Task ExecuteWithBusyAsync(Func<Task> action, string busyMessage = "Working...")
    {
        try
        {
            ClearError();
            SetBusy(busyMessage);
            await action();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            ClearBusy();
        }
    }

    /// <summary>
    /// Executes an action with busy state management and error handling, returning a result.
    /// </summary>
    protected async Task<T?> ExecuteWithBusyAsync<T>(Func<Task<T>> action, string busyMessage = "Working...")
    {
        try
        {
            ClearError();
            SetBusy(busyMessage);
            return await action();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            return default;
        }
        finally
        {
            ClearBusy();
        }
    }
}
