using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NovaSetup.Services;
using NovaSetup.ViewModels;

namespace NovaSetup.Views;

public partial class MainWindow : Window
{
    private const double DefaultDeveloperConsoleHeight = 180;
    private const double CollapsedDeveloperConsoleHeight = 40;
    private bool _viewModelInitialized;
    private bool _isDeveloperConsoleCollapsed;
    private bool _shouldAutoScrollDeveloperLogs = true;
    private double _expandedDeveloperConsoleHeight = DefaultDeveloperConsoleHeight;
    private ScrollViewer? _developerLogScrollViewer;
    private MainWindowViewModel? _subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Opened += OnWindowOpened;
        Closed += OnWindowClosed;
        DataContextChanged += OnDataContextChanged;
        LoggingService.LiveLogs.CollectionChanged += OnLiveLogsCollectionChanged;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModelInitialized || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _viewModelInitialized = true;
        await viewModel.InitializeAsync();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        EnsureDeveloperLogScrollViewer();
        UpdateDeveloperConsoleState();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        LoggingService.LiveLogs.CollectionChanged -= OnLiveLogsCollectionChanged;
        UnsubscribeFromViewModel();
        DetachDeveloperLogScrollViewer();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SubscribeToViewModel(DataContext as MainWindowViewModel);
        UpdateDeveloperConsoleState();
        _ = Dispatcher.UIThread.InvokeAsync(AutoScrollDeveloperLogsIfNeeded, DispatcherPriority.Background);
    }

    private void SubscribeToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            return;
        }

        UnsubscribeFromViewModel();
        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.IsDeveloperModeEnabled))
        {
            UpdateDeveloperConsoleState();
        }
    }

    private void OnLiveLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Reset)
        {
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(AutoScrollDeveloperLogsIfNeeded, DispatcherPriority.Background);
    }

    private void EnsureDeveloperLogScrollViewer()
    {
        if (_developerLogScrollViewer is not null)
        {
            return;
        }

        _developerLogScrollViewer = DeveloperLogListBox
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault();

        if (_developerLogScrollViewer is null)
        {
            return;
        }

        _developerLogScrollViewer.ScrollChanged += OnDeveloperLogScrollChanged;

        UpdateDeveloperLogAutoScrollState();
    }

    private void DetachDeveloperLogScrollViewer()
    {
        if (_developerLogScrollViewer is not null)
        {
            _developerLogScrollViewer.ScrollChanged -= OnDeveloperLogScrollChanged;
        }

        _developerLogScrollViewer = null;
    }

    private void OnDeveloperLogScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateDeveloperLogAutoScrollState();
    }

    private void UpdateDeveloperLogAutoScrollState()
    {
        if (_developerLogScrollViewer is null)
        {
            _shouldAutoScrollDeveloperLogs = true;
            return;
        }

        var maxOffset = Math.Max(0, _developerLogScrollViewer.Extent.Height - _developerLogScrollViewer.Viewport.Height);
        _shouldAutoScrollDeveloperLogs = maxOffset <= 0 || _developerLogScrollViewer.Offset.Y >= maxOffset - 8;
    }

    private void AutoScrollDeveloperLogsIfNeeded()
    {
        EnsureDeveloperLogScrollViewer();

        if (_developerLogScrollViewer is null ||
            !_shouldAutoScrollDeveloperLogs ||
            LoggingService.LiveLogs.Count == 0 ||
            (_subscribedViewModel?.IsDeveloperModeEnabled != true) ||
            _isDeveloperConsoleCollapsed)
        {
            return;
        }

        try
        {
            // Capture the last log item to avoid TOCTOU race condition
            var lastLogItem = LoggingService.LiveLogs.LastOrDefault();
            if (lastLogItem != null)
            {
                DeveloperLogListBox.ScrollIntoView(lastLogItem);
            }
        }
        catch
        {
            // Silently ignore if collection was modified or scrolling fails
        }
    }

    private void UpdateDeveloperConsoleState()
    {
        var developerModeEnabled = _subscribedViewModel?.IsDeveloperModeEnabled == true;

        if (!developerModeEnabled)
        {
            _isDeveloperConsoleCollapsed = false;
            DeveloperConsolePanel.Height = 0;
            DeveloperConsoleContent.IsVisible = true;
            DeveloperConsoleToggleButton.Content = "Collapse";
            DeveloperConsoleSplitter.IsVisible = false;
            return;
        }

        if (_isDeveloperConsoleCollapsed)
        {
            DeveloperConsolePanel.Height = CollapsedDeveloperConsoleHeight;
            DeveloperConsoleContent.IsVisible = false;
            DeveloperConsoleToggleButton.Content = "Expand";
            DeveloperConsoleSplitter.IsVisible = false;
            return;
        }

        if (DeveloperConsolePanel.Bounds.Height > CollapsedDeveloperConsoleHeight)
        {
            _expandedDeveloperConsoleHeight = DeveloperConsolePanel.Bounds.Height;
        }

        DeveloperConsolePanel.Height = Math.Max(DefaultDeveloperConsoleHeight, _expandedDeveloperConsoleHeight);
        DeveloperConsoleContent.IsVisible = true;
        DeveloperConsoleToggleButton.Content = "Collapse";
        DeveloperConsoleSplitter.IsVisible = true;
    }

    private void OnDeveloperConsoleToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel?.IsDeveloperModeEnabled != true)
        {
            return;
        }

        if (_isDeveloperConsoleCollapsed)
        {
            _isDeveloperConsoleCollapsed = false;
            UpdateDeveloperConsoleState();
            _ = Dispatcher.UIThread.InvokeAsync(AutoScrollDeveloperLogsIfNeeded, DispatcherPriority.Background);
            return;
        }

        if (DeveloperConsolePanel.Bounds.Height > CollapsedDeveloperConsoleHeight)
        {
            _expandedDeveloperConsoleHeight = DeveloperConsolePanel.Bounds.Height;
        }

        _isDeveloperConsoleCollapsed = true;
        UpdateDeveloperConsoleState();
    }
}
