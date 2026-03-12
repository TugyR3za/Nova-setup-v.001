using Avalonia.Controls;
using System;
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

    private void AccountMenuPopup_OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CloseAccountMenuOverlay();
        }
    }
}
