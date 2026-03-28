using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views.Controls;

public partial class BottomBarControl : UserControl
{
    public static readonly StyledProperty<int> SelectedAppsCountProperty =
        AvaloniaProperty.Register<BottomBarControl, int>(nameof(SelectedAppsCount));

    public static readonly StyledProperty<int> SelectedAppsSizeMBProperty =
        AvaloniaProperty.Register<BottomBarControl, int>(nameof(SelectedAppsSizeMB));

    public static readonly StyledProperty<int> SelectedAppsTimeMinsProperty =
        AvaloniaProperty.Register<BottomBarControl, int>(nameof(SelectedAppsTimeMins));

    public static readonly StyledProperty<ICommand?> SaveListCommandProperty =
        AvaloniaProperty.Register<BottomBarControl, ICommand?>(nameof(SaveListCommand));

    public static readonly StyledProperty<ICommand?> InstallSelectedCommandProperty =
        AvaloniaProperty.Register<BottomBarControl, ICommand?>(nameof(InstallSelectedCommand));

    public BottomBarControl()
    {
        InitializeComponent();
    }

    public int SelectedAppsCount
    {
        get => GetValue(SelectedAppsCountProperty);
        set => SetValue(SelectedAppsCountProperty, value);
    }

    public int SelectedAppsSizeMB
    {
        get => GetValue(SelectedAppsSizeMBProperty);
        set => SetValue(SelectedAppsSizeMBProperty, value);
    }

    public int SelectedAppsTimeMins
    {
        get => GetValue(SelectedAppsTimeMinsProperty);
        set => SetValue(SelectedAppsTimeMinsProperty, value);
    }

    public ICommand? SaveListCommand
    {
        get => GetValue(SaveListCommandProperty);
        set => SetValue(SaveListCommandProperty, value);
    }

    public ICommand? InstallSelectedCommand
    {
        get => GetValue(InstallSelectedCommandProperty);
        set => SetValue(InstallSelectedCommandProperty, value);
    }
}
