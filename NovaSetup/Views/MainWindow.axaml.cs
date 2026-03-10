using Avalonia.Controls;
using NovaSetup.ViewModels;

namespace NovaSetup.Views;

public partial class MainWindow : Window
{
    private bool _viewModelInitialized;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (_viewModelInitialized || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _viewModelInitialized = true;
        await viewModel.InitializeAsync();
    }
}
