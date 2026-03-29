using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NovaSetup.Services;
using NovaSetup.ViewModels;
using NovaSetup.Views;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NovaSetup;

public partial class App : Application
{
    private ScheduledUpdateService? _scheduledUpdateService;
    private CancellationTokenSource? _scheduledUpdateServiceCts;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += HandleDesktopExit;

            var loggingService = new LoggingService();
            var platformService = new PlatformService();
            var settingsService = new SettingsService(loggingService);
            var profileService = new ProfileService(settingsService, loggingService);
            var appPreferencesService = new AppPreferencesService(loggingService);
            var historyService = new HistoryService(loggingService);
            var catalogService = new CatalogService(platformService, loggingService);
            var selectionService = new SelectionService(profileService, loggingService);
            var detectionService = new DetectionService(loggingService);
            var scriptRunnerService = new ScriptRunnerService(loggingService);
            var installerService = new InstallerService(loggingService, detectionService, historyService, scriptRunnerService);
            var browserService = new BrowserService(loggingService);

            var mainWindowViewModel = new MainWindowViewModel(
                platformService,
                catalogService,
                selectionService,
                detectionService,
                installerService,
                loggingService,
                browserService,
                settingsService,
                profileService,
                appPreferencesService,
                historyService);
            detectionService.SettingsAccessor = () => mainWindowViewModel.Settings;
            installerService.SettingsAccessor = () => mainWindowViewModel.Settings;
            scriptRunnerService.SettingsAccessor = () => mainWindowViewModel.Settings;

            var splashScreen = new SplashScreen();
            desktop.MainWindow = splashScreen;
            splashScreen.Show();

            _ = StartDesktopAsync(
                desktop,
                splashScreen,
                mainWindowViewModel,
                settingsService,
                detectionService,
                installerService,
                loggingService);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartDesktopAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashScreen splashScreen,
        MainWindowViewModel mainWindowViewModel,
        SettingsService settingsService,
        DetectionService detectionService,
        InstallerService installerService,
        LoggingService loggingService)
    {
        var startupTimer = Stopwatch.StartNew();
        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel
        };

        try
        {
            await mainWindowViewModel.InitializeWithSplashAsync(async message =>
            {
                await Dispatcher.UIThread.InvokeAsync(() => splashScreen.UpdateStatus(message));
            });
        }
        catch (Exception ex)
        {
            loggingService.LogError($"Startup preload failed: {ex.Message}");
        }

        InitializeScheduledUpdates(
            mainWindowViewModel,
            settingsService,
            detectionService,
            installerService,
            loggingService);

        var remainingDelay = TimeSpan.FromMilliseconds(400) - startupTimer.Elapsed;
        if (remainingDelay > TimeSpan.Zero)
        {
            await Task.Delay(remainingDelay);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            splashScreen.Close();
        });
    }

    private void InitializeScheduledUpdates(
        MainWindowViewModel mainWindowViewModel,
        SettingsService settingsService,
        DetectionService detectionService,
        InstallerService installerService,
        LoggingService loggingService)
    {
        _scheduledUpdateService?.Stop();
        _scheduledUpdateServiceCts?.Cancel();
        _scheduledUpdateServiceCts?.Dispose();
        _scheduledUpdateServiceCts = new CancellationTokenSource();

        var appUpdateService = new AppUpdateService(loggingService);
        var taskSchedulerService = new TaskSchedulerService(loggingService);
        _scheduledUpdateService = new ScheduledUpdateService(
            mainWindowViewModel.Settings,
            appUpdateService,
            settingsService,
            detectionService,
            installerService,
            taskSchedulerService,
            async () => await Dispatcher.UIThread.InvokeAsync(mainWindowViewModel.CreateScheduledUpdateSnapshot),
            loggingService);

        mainWindowViewModel.AttachScheduledUpdateService(_scheduledUpdateService);
        _ = _scheduledUpdateService.StartAsync(_scheduledUpdateServiceCts.Token);
    }

    private void HandleDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _scheduledUpdateService?.Stop();
        _scheduledUpdateService = null;

        if (_scheduledUpdateServiceCts is not null)
        {
            _scheduledUpdateServiceCts.Cancel();
            _scheduledUpdateServiceCts.Dispose();
            _scheduledUpdateServiceCts = null;
        }
    }
}
