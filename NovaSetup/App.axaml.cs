using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NovaSetup.Services;
using NovaSetup.ViewModels;
using NovaSetup.Views;

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
            var catalogService = new CatalogService(platformService, loggingService);
            var selectionService = new SelectionService(loggingService);
            var detectionService = new DetectionService(loggingService);
            var installerService = new InstallerService(loggingService);
            var browserService = new BrowserService(loggingService);

            var mainWindowViewModel = new MainWindowViewModel(
                platformService,
                catalogService,
                selectionService,
                detectionService,
                installerService,
                loggingService,
                browserService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
