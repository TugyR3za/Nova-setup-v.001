using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NovaSetup.Models;

namespace NovaSetup.Views.Controls;

public partial class AppRowControl : UserControl
{
    public static readonly StyledProperty<AppItem?> ItemProperty =
        AvaloniaProperty.Register<AppRowControl, AppItem?>(nameof(Item));

    public static readonly StyledProperty<ICommand?> OpenPublisherCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(OpenPublisherCommand));

    public static readonly StyledProperty<ICommand?> ShowAppDetailsCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(ShowAppDetailsCommand));

    public static readonly StyledProperty<ICommand?> InstallAppCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(InstallAppCommand));

    public static readonly StyledProperty<ICommand?> UpdateAppCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(UpdateAppCommand));

    public static readonly StyledProperty<ICommand?> UninstallCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(UninstallCommand));

    public static readonly StyledProperty<ICommand?> OpenInstallLocationCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(OpenInstallLocationCommand));

    public static readonly StyledProperty<ICommand?> CopyToClipboardCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(CopyToClipboardCommand));

    public static readonly StyledProperty<ICommand?> ToggleSilentInstallPreferenceCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(ToggleSilentInstallPreferenceCommand));

    public static readonly StyledProperty<ICommand?> ToggleScanningPreferenceCommandProperty =
        AvaloniaProperty.Register<AppRowControl, ICommand?>(nameof(ToggleScanningPreferenceCommand));

    public static readonly StyledProperty<bool> SelectionEnabledProperty =
        AvaloniaProperty.Register<AppRowControl, bool>(nameof(SelectionEnabled), true);

    public static readonly StyledProperty<bool> IsPreferencesMenuOpenProperty =
        AvaloniaProperty.Register<AppRowControl, bool>(
            nameof(IsPreferencesMenuOpen),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public AppRowControl()
    {
        InitializeComponent();
    }

    public AppItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
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

    public bool SelectionEnabled
    {
        get => GetValue(SelectionEnabledProperty);
        set => SetValue(SelectionEnabledProperty, value);
    }

    public bool IsPreferencesMenuOpen
    {
        get => GetValue(IsPreferencesMenuOpenProperty);
        set => SetValue(IsPreferencesMenuOpenProperty, value);
    }

    private void PreferencesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IsPreferencesMenuOpen = !IsPreferencesMenuOpen;
    }

    private void PreferencesPopup_OnClosed(object? sender, EventArgs e)
    {
        IsPreferencesMenuOpen = false;
    }
}
