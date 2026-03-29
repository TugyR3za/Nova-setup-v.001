using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views.Controls;

public partial class AppListControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<AppListControl, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<ICommand?> OpenPublisherCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(OpenPublisherCommand));

    public static readonly StyledProperty<ICommand?> ShowAppDetailsCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(ShowAppDetailsCommand));

    public static readonly StyledProperty<ICommand?> InstallAppCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(InstallAppCommand));

    public static readonly StyledProperty<ICommand?> UpdateAppCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(UpdateAppCommand));

    public static readonly StyledProperty<ICommand?> UninstallCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(UninstallCommand));

    public static readonly StyledProperty<ICommand?> OpenInstallLocationCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(OpenInstallLocationCommand));

    public static readonly StyledProperty<ICommand?> CopyToClipboardCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(CopyToClipboardCommand));

    public static readonly StyledProperty<ICommand?> ToggleSilentInstallPreferenceCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(ToggleSilentInstallPreferenceCommand));

    public static readonly StyledProperty<ICommand?> ToggleScanningPreferenceCommandProperty =
        AvaloniaProperty.Register<AppListControl, ICommand?>(nameof(ToggleScanningPreferenceCommand));

    public AppListControl()
    {
        InitializeComponent();
    }

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public ICommand? OpenPublisherCommand
    {
        get => GetValue(OpenPublisherCommandProperty);
        set => SetValue(OpenPublisherCommandProperty, value);
    }

    public ICommand? ShowAppDetailsCommand
    {
        get => GetValue(ShowAppDetailsCommandProperty);
        set => SetValue(ShowAppDetailsCommandProperty, value);
    }

    public ICommand? InstallAppCommand
    {
        get => GetValue(InstallAppCommandProperty);
        set => SetValue(InstallAppCommandProperty, value);
    }

    public ICommand? UpdateAppCommand
    {
        get => GetValue(UpdateAppCommandProperty);
        set => SetValue(UpdateAppCommandProperty, value);
    }

    public ICommand? UninstallCommand
    {
        get => GetValue(UninstallCommandProperty);
        set => SetValue(UninstallCommandProperty, value);
    }

    public ICommand? OpenInstallLocationCommand
    {
        get => GetValue(OpenInstallLocationCommandProperty);
        set => SetValue(OpenInstallLocationCommandProperty, value);
    }

    public ICommand? CopyToClipboardCommand
    {
        get => GetValue(CopyToClipboardCommandProperty);
        set => SetValue(CopyToClipboardCommandProperty, value);
    }

    public ICommand? ToggleSilentInstallPreferenceCommand
    {
        get => GetValue(ToggleSilentInstallPreferenceCommandProperty);
        set => SetValue(ToggleSilentInstallPreferenceCommandProperty, value);
    }

    public ICommand? ToggleScanningPreferenceCommand
    {
        get => GetValue(ToggleScanningPreferenceCommandProperty);
        set => SetValue(ToggleScanningPreferenceCommandProperty, value);
    }
}
