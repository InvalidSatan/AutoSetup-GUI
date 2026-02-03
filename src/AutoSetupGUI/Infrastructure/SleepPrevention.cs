using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Prevents the system from sleeping during long-running operations.
/// </summary>
public class SleepPrevention : IDisposable
{
    private readonly ILogger<SleepPrevention> _logger;
    private bool _isActive;
    private bool _disposed;

    [Flags]
    private enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    public SleepPrevention(ILogger<SleepPrevention> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Prevents the system from sleeping and the display from turning off.
    /// </summary>
    public void PreventSleep()
    {
        if (_isActive)
            return;

        _logger.LogDebug("Enabling sleep prevention...");

        var result = SetThreadExecutionState(
            EXECUTION_STATE.ES_CONTINUOUS |
            EXECUTION_STATE.ES_SYSTEM_REQUIRED |
            EXECUTION_STATE.ES_DISPLAY_REQUIRED);

        if (result == 0)
        {
            _logger.LogWarning("Failed to set execution state for sleep prevention");
        }
        else
        {
            _isActive = true;
            _logger.LogDebug("Sleep prevention enabled");
        }
    }

    /// <summary>
    /// Allows the system to sleep normally again.
    /// </summary>
    public void AllowSleep()
    {
        if (!_isActive)
            return;

        _logger.LogDebug("Disabling sleep prevention...");

        SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        _isActive = false;

        _logger.LogDebug("Sleep prevention disabled");
    }

    /// <summary>
    /// Executes an action while preventing sleep, then restores normal sleep behavior.
    /// </summary>
    public async Task<T> ExecuteWithSleepPreventionAsync<T>(Func<Task<T>> action)
    {
        try
        {
            PreventSleep();
            return await action();
        }
        finally
        {
            AllowSleep();
        }
    }

    /// <summary>
    /// Executes an action while preventing sleep, then restores normal sleep behavior.
    /// </summary>
    public async Task ExecuteWithSleepPreventionAsync(Func<Task> action)
    {
        try
        {
            PreventSleep();
            await action();
        }
        finally
        {
            AllowSleep();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        AllowSleep();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
