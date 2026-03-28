using System.Collections.ObjectModel;
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

        _availableUpdates.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasUpdates));
            OnPropertyChanged(nameof(HasNoUpdates));
            OnPropertyChanged(nameof(SummaryText));
            _updateAllCommand.RaiseCanExecuteChanged();
            _updateSingleCommand.RaiseCanExecuteChanged();
        };
    }

    public ObservableCollection<AppItem> AvailableUpdates => _availableUpdates;

    public bool HasUpdates => _availableUpdates.Count > 0;

    public bool HasNoUpdates => !HasUpdates;

    public string SummaryText => HasUpdates
        ? _availableUpdates.Count == 1
            ? "1 update available"
            : $"{_availableUpdates.Count} updates available"
        : "All apps are up to date";

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
        _availableUpdates.Clear();
        foreach (var app in apps ?? Enumerable.Empty<AppItem>())
        {
            _availableUpdates.Add(app);
        }
    }
}
