using System.Collections.ObjectModel;
using System.Windows.Input;
using NovaSetup.Models;
using NovaSetup.Services;

namespace NovaSetup.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private readonly HistoryService _historyService;
    private readonly LoggingService? _loggingService;
    private readonly ObservableCollection<InstallRecord> _records = new();
    private readonly AsyncRelayCommand _clearHistoryCommand;

    private bool _isBusy;
    private int _totalInstallCount;

    public HistoryViewModel(HistoryService historyService, LoggingService? loggingService = null)
    {
        _historyService = historyService;
        _loggingService = loggingService;
        _clearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync, () => !_isBusy && HasRecords);
        _records.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRecords));
            OnPropertyChanged(nameof(HasNoRecords));
            _clearHistoryCommand.RaiseCanExecuteChanged();
        };
    }

    public ObservableCollection<InstallRecord> Records => _records;

    public bool HasRecords => _records.Count > 0;

    public bool HasNoRecords => !HasRecords;

    public string TotalInstallsText => $"Total installs: {_totalInstallCount}";

    public ICommand ClearHistoryCommand => _clearHistoryCommand;

    public async Task RefreshAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        _clearHistoryCommand.RaiseCanExecuteChanged();

        try
        {
            var history = await _historyService.GetHistoryAsync();
            var totalInstallCount = await _historyService.GetTotalInstallCountAsync();

            _records.Clear();
            foreach (var record in history)
            {
                _records.Add(record);
            }

            _totalInstallCount = totalInstallCount;
            OnPropertyChanged(nameof(TotalInstallsText));
        }
        finally
        {
            _isBusy = false;
            _clearHistoryCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task ClearHistoryAsync()
    {
        await _historyService.ClearHistoryAsync();
        await RefreshAsync();
        _loggingService?.LogInfo("Install history cleared.");
    }
}
