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
        LoggingService? loggingService = null;

        try
        {
            loggingService = new LoggingService();
            loggingService.LogInfo("[ScheduledUpdates] Running in headless scheduled-update mode.");

            var settingsService = new SettingsService(loggingService);
            var settings = await settingsService.LoadSettingsAsync();
            var catalogService = new CatalogService(loggingService);
            var apps = await catalogService.LoadAppsAsync(PlatformService.GetCurrentPlatform());
            var detectionService = new DetectionService(loggingService);
            detectionService.SettingsAccessor = () => settings;
            await detectionService.DetectInstalledAppsAsync(apps);
            var scriptRunnerService = new ScriptRunnerService(loggingService)
            {
                SettingsAccessor = () => settings
            };
            var installerService = new InstallerService(settings, loggingService, scriptRunnerService);
            installerService.SettingsAccessor = () => settings;
            var appUpdateService = new AppUpdateService(loggingService);
            var scheduledUpdateService = new ScheduledUpdateService(
                settings,
                appUpdateService,
                settingsService,
                loggingService);
            scheduledUpdateService.ConfigureRunContext(
                () => Task.FromResult(apps),
                detectionService,
                installerService);

            await scheduledUpdateService.RunScheduledUpdateAsync();
        }
        catch (Exception ex)
        {
            loggingService?.LogError($"[ScheduledUpdates] Headless scheduled update failed: {ex.Message}");
            var errorDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NovaSetup");
            Directory.CreateDirectory(errorDirectory);
            File.AppendAllText(
                Path.Combine(errorDirectory, "headless-error.log"),
                $"{DateTime.UtcNow:O} HEADLESS ERROR: {ex}{Environment.NewLine}");
        }
        finally
        {
            Environment.Exit(0);
        }
    }
}
