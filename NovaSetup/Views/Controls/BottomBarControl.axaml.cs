using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views.Controls;

public partial class BottomBarControl : UserControl
{
    public static readonly StyledProperty<string?> FooterTextProperty =
        AvaloniaProperty.Register<BottomBarControl, string?>(nameof(FooterText));

    public static readonly StyledProperty<ICommand?> SaveListCommandProperty =
        AvaloniaProperty.Register<BottomBarControl, ICommand?>(nameof(SaveListCommand));

    public static readonly StyledProperty<ICommand?> InstallSelectedCommandProperty =
        AvaloniaProperty.Register<BottomBarControl, ICommand?>(nameof(InstallSelectedCommand));

    public BottomBarControl()
    {
        InitializeComponent();
    }

    public string? FooterText
    {
        get => GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
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
