using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using AutoSetupGUI.Services;
using AutoSetupGUI.Services.Interfaces;
using AutoSetupGUI.Services.Implementations;
using AutoSetupGUI.ViewModels;
using AutoSetupGUI.Infrastructure;

namespace AutoSetupGUI;

/// <summary>
/// University Auto Setup v2.0 Application
/// Professional-grade Windows machine setup application for Appalachian State University
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static IConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// Indicates whether we're recovering from an interrupted session.
    /// </summary>
    public static bool IsRecoveryMode { get; private set; }

    /// <summary>
    /// The recovered state from a previous interrupted session.
    /// </summary>
    public static TaskExecutionState? RecoveredState { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // FIRST: Check if running from network share - copy locally and relaunch
        // This MUST happen before anything else to ensure resilience to network driver updates
        if (NetworkResilienceManager.EnsureRunningLocally(e.Args))
        {
            // Relaunching from local copy - exit this instance
            Shutdown();
            return;
        }

        // Set up global exception handlers
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Check for administrative privileges - auto-elevate if needed
        if (!AdminPrivilegeChecker.IsRunningAsAdmin())
        {
            // Attempt to restart with admin privileges silently
            if (AdminPrivilegeChecker.RestartAsAdmin())
            {
                Shutdown();
                return;
            }

            // If elevation failed (user declined UAC or other error), show message and continue anyway
            MessageBox.Show(
                "This application requires administrative privileges for full functionality.\n\n" +
                "Some features may not work correctly without admin rights.",
                "Administrative Rights Recommended",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Check for recovery from interrupted session
        CheckForRecovery();

        // Build configuration
        Configuration = BuildConfiguration();

        // Configure Serilog
        ConfigureSerilog();

        // Build service provider
        Services = ConfigureServices();

        Log.Information("University Auto Setup v2.0 starting...");

        if (IsRecoveryMode)
        {
            Log.Information("Recovery mode active - restoring from interrupted session");
        }

        if (NetworkResilienceManager.IsRunningFromLocalCopy())
        {
            Log.Information("Running from local copy at: {Path}", NetworkResilienceManager.LocalAppPath);

            // Show notification that we're running from local copy
            MessageBox.Show(
                $"For stability during driver updates, this application has been copied to a local folder and is running from:\n\n{NetworkResilienceManager.LocalAppPath}\n\nThis prevents crashes if network drivers are updated.",
                "Running from Local Copy",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Create and show the main window AFTER services are configured
        var mainWindow = new Views.MainWindow();
        mainWindow.Show();
    }

    /// <summary>
    /// Checks if we need to recover from an interrupted session.
    /// </summary>
    private void CheckForRecovery()
    {
        try
        {
            // Check if there's an active state file indicating interrupted tasks
            if (NetworkResilienceManager.HasActiveState())
            {
                var state = NetworkResilienceManager.LoadState();
                if (state != null && state.IsRunning && !state.IsComplete)
                {
                    // Check if it was recent (within the last 30 minutes)
                    var timeSinceLastUpdate = DateTime.Now - state.LastUpdateTime;
                    if (timeSinceLastUpdate < TimeSpan.FromMinutes(30))
                    {
                        IsRecoveryMode = true;
                        RecoveredState = state;
                    }
                    else
                    {
                        // Stale state - clear it
                        NetworkResilienceManager.ClearState();
                    }
                }
            }
        }
        catch
        {
            // Ignore recovery check errors
            IsRecoveryMode = false;
            RecoveredState = null;
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;

        // Log safely - network might be down
        try { Log.Error(exception, "Unhandled domain exception"); } catch { }

        // Only show message box if not a network-related error (which might cascade)
        if (!IsNetworkRelatedError(exception))
        {
            try
            {
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{exception?.Message}\n\nThe application will continue running.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { /* UI might not be available */ }
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Log safely - network might be down
        try { Log.Error(e.Exception, "Unhandled dispatcher exception"); } catch { }

        // Don't crash - always handle it
        e.Handled = true;

        // Only show message box if not a network-related error
        if (!IsNetworkRelatedError(e.Exception))
        {
            try
            {
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { /* UI might not be available */ }
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Log safely
        try { Log.Error(e.Exception, "Unobserved task exception"); } catch { }
        e.SetObserved(); // Prevent crash - always observe it
    }

    /// <summary>
    /// Checks if the exception is likely network-related (to avoid cascading UI errors).
    /// </summary>
    private static bool IsNetworkRelatedError(Exception? exception)
    {
        if (exception == null) return false;

        var message = exception.Message?.ToLowerInvariant() ?? "";
        var typeName = exception.GetType().Name.ToLowerInvariant();

        return message.Contains("network") ||
               message.Contains("socket") ||
               message.Contains("connection") ||
               message.Contains("remote") ||
               message.Contains("unreachable") ||
               message.Contains("unc path") ||
               typeName.Contains("socket") ||
               typeName.Contains("network") ||
               typeName.Contains("io") ||
               exception.InnerException != null && IsNetworkRelatedError(exception.InnerException);
    }

    private static IConfiguration BuildConfiguration()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;

        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();
    }

    private static void ConfigureSerilog()
    {
        var logPath = Configuration["Logging:LocalPath"] ?? @"C:\Temp\UniversityAutoSetup\Logs";
        var logFile = Path.Combine(logPath, $"AutoSetupGUI_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        // Ensure log directory exists
        Directory.CreateDirectory(logPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logFile,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(Configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // Infrastructure
        services.AddSingleton<WmiHelper>();
        services.AddSingleton<RegistryHelper>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<PowerShellExecutor>();
        services.AddSingleton<SleepPrevention>();

        // Services
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<ISCCMService, SCCMService>();
        services.AddSingleton<IGroupPolicyService, GroupPolicyService>();
        services.AddSingleton<IDellUpdateService, DellUpdateService>();
        services.AddSingleton<IImageCheckService, ImageCheckService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<TaskOrchestrator>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SystemInfoViewModel>();
        services.AddTransient<TasksViewModel>();
        services.AddTransient<LogViewerViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("University Auto Setup v2.0 shutting down...");

        // Clean up local copy if running from one and tasks are complete
        var isLocalCopy = NetworkResilienceManager.IsRunningFromLocalCopy();
        var hasActiveState = NetworkResilienceManager.HasActiveState();

        Log.Information("Exit check - IsRunningFromLocalCopy: {IsLocal}, HasActiveState: {HasState}",
            isLocalCopy, hasActiveState);

        if (isLocalCopy)
        {
            // Only clean up if there's no active/incomplete state (tasks finished)
            if (!hasActiveState)
            {
                Log.Information("Scheduling cleanup of local copy at: {Path}", NetworkResilienceManager.LocalAppPath);
                Log.Information("Cleanup log will be at: {Path}", Path.Combine(Path.GetTempPath(), "autosetup_cleanup.log"));
                NetworkResilienceManager.ScheduleSelfCleanup();
            }
            else
            {
                Log.Information("Skipping cleanup - HasActiveState returned true (tasks may still be running)");
            }
        }
        else
        {
            Log.Information("Not running from local copy - no cleanup needed");
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
