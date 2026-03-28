using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views.Controls;

public partial class SidebarControl : UserControl
{
    public static readonly StyledProperty<string?> CurrentProfileNameProperty =
        AvaloniaProperty.Register<SidebarControl, string?>(nameof(CurrentProfileName));

    public static readonly StyledProperty<bool> IsDashboardSelectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsDashboardSelected));

    public static readonly StyledProperty<bool> IsDashboardUnselectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsDashboardUnselected), true);

    public static readonly StyledProperty<bool> IsAppsSelectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsAppsSelected));

    public static readonly StyledProperty<bool> IsAppsUnselectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsAppsUnselected), true);

    public static readonly StyledProperty<bool> IsUpdatesSelectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsUpdatesSelected));

    public static readonly StyledProperty<bool> IsUpdatesUnselectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsUpdatesUnselected), true);

    public static readonly StyledProperty<bool> HasUpdatesAvailableProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(HasUpdatesAvailable));

    public static readonly StyledProperty<int> UpdateAvailableCountProperty =
        AvaloniaProperty.Register<SidebarControl, int>(nameof(UpdateAvailableCount));

    public static readonly StyledProperty<ICommand?> NavigateDashboardCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateDashboardCommand));

    public static readonly StyledProperty<ICommand?> NavigateAppsCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateAppsCommand));

    public static readonly StyledProperty<ICommand?> NavigateUpdatesCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateUpdatesCommand));

    public static readonly StyledProperty<ICommand?> NavigateAboutCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateAboutCommand));

    public static readonly StyledProperty<ICommand?> NavigateHistoryCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateHistoryCommand));

    public static readonly StyledProperty<ICommand?> HelpCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(HelpCommand));

    public static readonly StyledProperty<ICommand?> ToggleAccountMenuCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(ToggleAccountMenuCommand));

    public static readonly StyledProperty<ICommand?> OpenAccountProfileCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(OpenAccountProfileCommand));

    public static readonly StyledProperty<ICommand?> OpenAccountSettingsCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(OpenAccountSettingsCommand));

    public static readonly StyledProperty<bool> IsAccountMenuOpenProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(
            nameof(IsAccountMenuOpen),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public SidebarControl()
    {
        InitializeComponent();
    }

    public string? CurrentProfileName
    {
        get => GetValue(CurrentProfileNameProperty);
        set => SetValue(CurrentProfileNameProperty, value);
    }

    public bool IsDashboardSelected
    {
        get => GetValue(IsDashboardSelectedProperty);
        set => SetValue(IsDashboardSelectedProperty, value);
    }

    public bool IsDashboardUnselected
    {
        get => GetValue(IsDashboardUnselectedProperty);
        set => SetValue(IsDashboardUnselectedProperty, value);
    }

    public bool IsAppsSelected
    {
        get => GetValue(IsAppsSelectedProperty);
        set => SetValue(IsAppsSelectedProperty, value);
    }

    public bool IsAppsUnselected
    {
        get => GetValue(IsAppsUnselectedProperty);
        set => SetValue(IsAppsUnselectedProperty, value);
    }

    public bool IsUpdatesSelected
    {
        get => GetValue(IsUpdatesSelectedProperty);
        set => SetValue(IsUpdatesSelectedProperty, value);
    }

    public bool IsUpdatesUnselected
    {
        get => GetValue(IsUpdatesUnselectedProperty);
        set => SetValue(IsUpdatesUnselectedProperty, value);
    }

    public bool HasUpdatesAvailable
    {
        get => GetValue(HasUpdatesAvailableProperty);
        set => SetValue(HasUpdatesAvailableProperty, value);
    }

    public int UpdateAvailableCount
    {
        get => GetValue(UpdateAvailableCountProperty);
        set => SetValue(UpdateAvailableCountProperty, value);
    }

    public ICommand? NavigateDashboardCommand
    {
        get => GetValue(NavigateDashboardCommandProperty);
        set => SetValue(NavigateDashboardCommandProperty, value);
    }

    public ICommand? NavigateAppsCommand
    {
        get => GetValue(NavigateAppsCommandProperty);
        set => SetValue(NavigateAppsCommandProperty, value);
    }

    public ICommand? NavigateUpdatesCommand
    {
        get => GetValue(NavigateUpdatesCommandProperty);
        set => SetValue(NavigateUpdatesCommandProperty, value);
    }

    public ICommand? NavigateAboutCommand
    {
        get => GetValue(NavigateAboutCommandProperty);
        set => SetValue(NavigateAboutCommandProperty, value);
    }

    public ICommand? NavigateHistoryCommand
    {
        get => GetValue(NavigateHistoryCommandProperty);
        set => SetValue(NavigateHistoryCommandProperty, value);
    }

    public ICommand? HelpCommand
    {
        get => GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }

    public ICommand? ToggleAccountMenuCommand
    {
        get => GetValue(ToggleAccountMenuCommandProperty);
        set => SetValue(ToggleAccountMenuCommandProperty, value);
    }

    public ICommand? OpenAccountProfileCommand
    {
        get => GetValue(OpenAccountProfileCommandProperty);
        set => SetValue(OpenAccountProfileCommandProperty, value);
    }

    public ICommand? OpenAccountSettingsCommand
    {
        get => GetValue(OpenAccountSettingsCommandProperty);
        set => SetValue(OpenAccountSettingsCommandProperty, value);
    }

    public bool IsAccountMenuOpen
    {
        get => GetValue(IsAccountMenuOpenProperty);
        set => SetValue(IsAccountMenuOpenProperty, value);
    }

    private void AccountMenuPopup_OnClosed(object? sender, EventArgs e)
    {
        IsAccountMenuOpen = false;
    }
}
