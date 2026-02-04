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
/// University Auto Setup v3.0 Application
/// Professional-grade Windows machine setup application for Appalachian State University
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static IConfiguration Configuration { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        // Build configuration
        Configuration = BuildConfiguration();

        // Configure Serilog
        ConfigureSerilog();

        // Build service provider
        Services = ConfigureServices();

        Log.Information("University Auto Setup v3.0 starting...");
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Log.Error(exception, "Unhandled domain exception");

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{exception?.Message}\n\nThe application will continue running.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception");

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true; // Prevent crash
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved(); // Prevent crash
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
        Log.Information("University Auto Setup v3.0 shutting down...");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
