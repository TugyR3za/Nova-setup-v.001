using Avalonia;
using NovaSetup.Services;

namespace NovaSetup;

class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        if (IsScheduledUpdateHeadlessMode(args))
        {
            await RunHeadlessScheduledUpdateAsync();
            return;
        }

        if (OperatingSystem.IsWindows() && !ElevationService.IsRunningAsAdministrator())
        {
            if (ElevationService.RelaunchAsAdministrator(args))
            {
                return;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static bool IsScheduledUpdateHeadlessMode(string[] args)
    {
        return args.Any(arg =>
            string.Equals(arg, "--scheduled-update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunHeadlessScheduledUpdateAsync()
    {
        var loggingService = new LoggingService();

        try
        {
            loggingService.LogInfo("[ScheduledUpdates] Running in headless scheduled-update mode.");

            var settingsService = new SettingsService(loggingService);
            var settings = await settingsService.LoadSettingsAsync();
            if (!settings.ScheduledUpdatesEnabled)
            {
                loggingService.LogInfo("[ScheduledUpdates] Scheduled updates are disabled. Exiting headless mode.");
                Environment.Exit(0);
                return;
            }

            var platformService = new PlatformService();
            var detectionService = new DetectionService(loggingService);
            var historyService = new HistoryService(loggingService);
            var installerService = new InstallerService(loggingService, detectionService, historyService);
            var appUpdateService = new AppUpdateService(loggingService);
            var taskSchedulerService = new TaskSchedulerService(loggingService);
            var catalogService = new CatalogService(platformService, loggingService);

            var platform = platformService.GetCurrentPlatformInfo();
            var scheduledUpdateService = new ScheduledUpdateService(
                settings,
                appUpdateService,
                settingsService,
                detectionService,
                installerService,
                taskSchedulerService,
                () => catalogService.LoadAppsAsync(platform.Id),
                loggingService);

            await scheduledUpdateService.RunScheduledUpdateAsync();
        }
        catch (Exception ex)
        {
            loggingService.LogError($"[ScheduledUpdates] Headless scheduled update failed: {ex.Message}");
        }

        Environment.Exit(0);
    }
}
