using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace NovaSetup.Views.Controls;

public partial class AppGridControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<AppGridControl, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<ICommand?> OpenPublisherCommandProperty =
        AvaloniaProperty.Register<AppGridControl, ICommand?>(nameof(OpenPublisherCommand));

    public static readonly StyledProperty<ICommand?> ShowAppDetailsCommandProperty =
        AvaloniaProperty.Register<AppGridControl, ICommand?>(nameof(ShowAppDetailsCommand));

    public AppGridControl()
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
}
