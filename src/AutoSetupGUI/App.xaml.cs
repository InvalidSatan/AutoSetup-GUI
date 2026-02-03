using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
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

        // Check for administrative privileges
        if (!AdminPrivilegeChecker.IsRunningAsAdmin())
        {
            MessageBox.Show(
                "This application requires administrative privileges to function properly.\n\n" +
                "Please run the application as Administrator.",
                "Administrative Rights Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Attempt to restart with admin privileges
            if (AdminPrivilegeChecker.RestartAsAdmin())
            {
                Shutdown();
                return;
            }
        }

        // Build configuration
        Configuration = BuildConfiguration();

        // Configure Serilog
        ConfigureSerilog();

        // Build service provider
        Services = ConfigureServices();

        Log.Information("University Auto Setup v3.0 starting...");
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
