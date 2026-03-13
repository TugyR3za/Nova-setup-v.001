using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
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
}
