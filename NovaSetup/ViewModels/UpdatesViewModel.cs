using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using NovaSetup.Models;

namespace NovaSetup.ViewModels;

public sealed class UpdatesViewModel : ObservableObject
{
    private readonly ObservableCollection<AppItem> _availableUpdates = new();
    private readonly AsyncRelayCommand _updateAllCommand;
    private readonly AsyncRelayCommand _checkNowCommand;
    private readonly RelayCommand _updateSingleCommand;
    private bool _isBusy;
    private bool _isUpdatingSelectAll;

    public UpdatesViewModel(
        Func<Task> updateAllAsync,
        Func<Task> checkNowAsync,
        Func<AppItem, Task> updateSingleAsync)
    {
        ArgumentNullException.ThrowIfNull(updateAllAsync);
        ArgumentNullException.ThrowIfNull(checkNowAsync);
        ArgumentNullException.ThrowIfNull(updateSingleAsync);

        _updateAllCommand = new AsyncRelayCommand(updateAllAsync, () => !_isBusy && HasUpdates);
        _checkNowCommand = new AsyncRelayCommand(checkNowAsync, () => !_isBusy);
        _updateSingleCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is AppItem app && !_isBusy)
                {
                    _ = updateSingleAsync(app);
                }
            },
            parameter => !_isBusy && parameter is AppItem);

        _availableUpdates.CollectionChanged += HandleAvailableUpdatesChanged;
    }

    public ObservableCollection<AppItem> AvailableUpdates => _availableUpdates;

    public bool HasUpdates => _availableUpdates.Count > 0;

    public bool HasNoUpdates => !HasUpdates;

    public string SummaryText => HasUpdates
        ? _availableUpdates.Count == 1
            ? "1 update available"
            : $"{_availableUpdates.Count} updates available"
        : "All apps are up to date";

    public int SelectedUpdateCount => _availableUpdates.Count(app => app.IsSelectedForUpdate);

    public bool HasSelectedUpdates => SelectedUpdateCount > 0;

    public string SelectedUpdatesText => HasSelectedUpdates
        ? SelectedUpdateCount == 1
            ? "1 selected"
            : $"{SelectedUpdateCount} selected"
        : string.Empty;

    public bool AreAllVisibleUpdatesSelected
    {
        get => HasUpdates && _availableUpdates.All(app => app.IsSelectedForUpdate);
        set
        {
            if (_isUpdatingSelectAll)
            {
                return;
            }

            _isUpdatingSelectAll = true;
            try
            {
                foreach (var app in _availableUpdates)
                {
                    app.IsSelectedForUpdate = value;
                }
            }
            finally
            {
                _isUpdatingSelectAll = false;
            }

            NotifyUpdateSelectionStateChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _updateAllCommand.RaiseCanExecuteChanged();
                _checkNowCommand.RaiseCanExecuteChanged();
                _updateSingleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand UpdateAllCommand => _updateAllCommand;

    public ICommand CheckNowCommand => _checkNowCommand;

    public ICommand UpdateSingleCommand => _updateSingleCommand;

    public void SetAvailableUpdates(IEnumerable<AppItem> apps)
    {
        foreach (var app in _availableUpdates)
        {
            app.PropertyChanged -= HandleAvailableUpdatePropertyChanged;
        }

        _availableUpdates.Clear();
        foreach (var app in apps ?? Enumerable.Empty<AppItem>())
        {
            _availableUpdates.Add(app);
        }
    }

    private void HandleAvailableUpdatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<AppItem>())
            {
                item.PropertyChanged -= HandleAvailableUpdatePropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<AppItem>())
            {
                item.PropertyChanged -= HandleAvailableUpdatePropertyChanged;
                item.PropertyChanged += HandleAvailableUpdatePropertyChanged;
            }
        }

        OnPropertyChanged(nameof(HasUpdates));
        OnPropertyChanged(nameof(HasNoUpdates));
        OnPropertyChanged(nameof(SummaryText));
        NotifyUpdateSelectionStateChanged();
        _updateAllCommand.RaiseCanExecuteChanged();
        _updateSingleCommand.RaiseCanExecuteChanged();
    }

    private void HandleAvailableUpdatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItem.IsSelectedForUpdate))
        {
            NotifyUpdateSelectionStateChanged();
        }
    }

    private void NotifyUpdateSelectionStateChanged()
    {
        OnPropertyChanged(nameof(SelectedUpdateCount));
        OnPropertyChanged(nameof(HasSelectedUpdates));
        OnPropertyChanged(nameof(SelectedUpdatesText));
        OnPropertyChanged(nameof(AreAllVisibleUpdatesSelected));
    }
}
