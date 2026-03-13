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

    public static readonly StyledProperty<bool> IsMyListsSelectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsMyListsSelected));

    public static readonly StyledProperty<bool> IsMyListsUnselectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsMyListsUnselected), true);

    public static readonly StyledProperty<bool> IsHistorySelectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsHistorySelected));

    public static readonly StyledProperty<bool> IsHistoryUnselectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsHistoryUnselected), true);

    public static readonly StyledProperty<bool> IsLogsSelectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsLogsSelected));

    public static readonly StyledProperty<bool> IsLogsUnselectedProperty =
        AvaloniaProperty.Register<SidebarControl, bool>(nameof(IsLogsUnselected), true);

    public static readonly StyledProperty<ICommand?> NavigateDashboardCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateDashboardCommand));

    public static readonly StyledProperty<ICommand?> NavigateAppsCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateAppsCommand));

    public static readonly StyledProperty<ICommand?> NavigateMyListsCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateMyListsCommand));

    public static readonly StyledProperty<ICommand?> NavigateHistoryCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateHistoryCommand));

    public static readonly StyledProperty<ICommand?> NavigateLogsCommandProperty =
        AvaloniaProperty.Register<SidebarControl, ICommand?>(nameof(NavigateLogsCommand));

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

    public bool IsMyListsSelected
    {
        get => GetValue(IsMyListsSelectedProperty);
        set => SetValue(IsMyListsSelectedProperty, value);
    }

    public bool IsMyListsUnselected
    {
        get => GetValue(IsMyListsUnselectedProperty);
        set => SetValue(IsMyListsUnselectedProperty, value);
    }

    public bool IsHistorySelected
    {
        get => GetValue(IsHistorySelectedProperty);
        set => SetValue(IsHistorySelectedProperty, value);
    }

    public bool IsHistoryUnselected
    {
        get => GetValue(IsHistoryUnselectedProperty);
        set => SetValue(IsHistoryUnselectedProperty, value);
    }

    public bool IsLogsSelected
    {
        get => GetValue(IsLogsSelectedProperty);
        set => SetValue(IsLogsSelectedProperty, value);
    }

    public bool IsLogsUnselected
    {
        get => GetValue(IsLogsUnselectedProperty);
        set => SetValue(IsLogsUnselectedProperty, value);
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

    public ICommand? NavigateMyListsCommand
    {
        get => GetValue(NavigateMyListsCommandProperty);
        set => SetValue(NavigateMyListsCommandProperty, value);
    }

    public ICommand? NavigateHistoryCommand
    {
        get => GetValue(NavigateHistoryCommandProperty);
        set => SetValue(NavigateHistoryCommandProperty, value);
    }

    public ICommand? NavigateLogsCommand
    {
        get => GetValue(NavigateLogsCommandProperty);
        set => SetValue(NavigateLogsCommandProperty, value);
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
