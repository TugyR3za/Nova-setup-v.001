using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NovaSetup.Services;
using NovaSetup.ViewModels;
using NovaSetup.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NovaSetup;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loggingService = new LoggingService();
            var platformService = new PlatformService();
            var settingsService = new SettingsService(loggingService);
            var profileService = new ProfileService(settingsService, loggingService);
            var catalogService = new CatalogService(platformService, loggingService);
            var selectionService = new SelectionService(profileService, loggingService);
            var detectionService = new DetectionService(loggingService);
            var installerService = new InstallerService(loggingService, detectionService);
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
                profileService);

            var splashScreen = new SplashScreen();
            desktop.MainWindow = splashScreen;
            splashScreen.Show();

            _ = StartDesktopAsync(desktop, splashScreen, mainWindowViewModel, loggingService);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartDesktopAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashScreen splashScreen,
        MainWindowViewModel mainWindowViewModel,
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

        var remainingDelay = TimeSpan.FromMilliseconds(1200) - startupTimer.Elapsed;
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
}
