using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Threading;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class ScheduledUpdateService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DailyMinimumInterval = TimeSpan.FromHours(20);
    private static readonly TimeSpan WeeklyMinimumInterval = TimeSpan.FromDays(6);
    private static readonly TimeSpan MonthlyMinimumInterval = TimeSpan.FromDays(25);

    private readonly AppSettings _settings;
    private readonly AppUpdateService _appUpdateService;
    private readonly SettingsService _settingsService;
    private readonly DetectionService _detectionService;
    private readonly InstallerService _installerService;
    private readonly TaskSchedulerService _taskSchedulerService;
    private readonly Func<Task<List<AppItem>>> _appSnapshotProviderAsync;
    private readonly LoggingService? _loggingService;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _stopCts;
    private Task? _timerLoopTask;
    private bool _isStarted;
    private bool _isSettingsSubscribed;

    public ScheduledUpdateService(
        AppSettings settings,
        AppUpdateService appUpdateService,
        SettingsService settingsService,
        DetectionService detectionService,
        InstallerService installerService,
        TaskSchedulerService taskSchedulerService,
        Func<Task<List<AppItem>>> appSnapshotProviderAsync,
        LoggingService? loggingService = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _appUpdateService = appUpdateService ?? throw new ArgumentNullException(nameof(appUpdateService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
        _installerService = installerService ?? throw new ArgumentNullException(nameof(installerService));
        _taskSchedulerService = taskSchedulerService ?? throw new ArgumentNullException(nameof(taskSchedulerService));
        _appSnapshotProviderAsync = appSnapshotProviderAsync ?? throw new ArgumentNullException(nameof(appSnapshotProviderAsync));
        _loggingService = loggingService;
    }

    public DateTime NextScheduledRun =>
        OperatingSystem.IsWindows()
            ? _taskSchedulerService.GetNextRunTime() ?? DateTime.MinValue
            : CalculateNextScheduledRun(_settings);

    public bool IsTaskRegistered => OperatingSystem.IsWindows() && _taskSchedulerService.IsTaskRegistered();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _isStarted = true;
        EnsureSettingsSubscription();

        if (OperatingSystem.IsWindows())
        {
            SyncRegistration();
            return Task.CompletedTask;
        }

        if (_timerLoopTask is not null && !_timerLoopTask.IsCompleted)
        {
            return _timerLoopTask;
        }

        StopFallbackTimer();
        _stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(PollInterval);
        _timerLoopTask = RunTimerLoopAsync(_stopCts.Token);
        return _timerLoopTask;
    }

    public void Stop()
    {
        StopFallbackTimer();
        if (_isSettingsSubscribed)
        {
            _settings.PropertyChanged -= HandleSettingsChanged;
            _isSettingsSubscribed = false;
        }

        _isStarted = false;
    }

    public async Task RunScheduledUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!await _runGate.WaitAsync(0, cancellationToken))
        {
            _loggingService?.LogDebug("[ScheduledUpdates] A scheduled update run is already in progress.");
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _loggingService?.LogInfo("[ScheduledUpdates] Starting scheduled update check...");

            var appSnapshot = await _appSnapshotProviderAsync();
            if (appSnapshot.Count == 0)
            {
                _loggingService?.LogInfo("[ScheduledUpdates] All apps are up to date.");
                await PersistLastRunAsync(cancellationToken);
                return;
            }

            var platformId = GetCurrentPlatformId();
            var detectedStates = await Task.Run(
                () => _detectionService.DetectInstalledAppStates(appSnapshot, platformId),
                cancellationToken);

            foreach (var app in appSnapshot)
            {
                if (detectedStates.TryGetValue(app.Id, out var state))
                {
                    app.IsInstalled = state.IsInstalled;
                    app.InstalledVersion = state.InstalledVersion;
                }
                else
                {
                    app.IsInstalled = false;
                    app.InstalledVersion = string.Empty;
                }
            }

            var latestVersions = await Task.Run(
                () => _appUpdateService.ResolveLatestCatalogVersions(appSnapshot, platformId),
                cancellationToken);

            foreach (var app in appSnapshot)
            {
                if (latestVersions.TryGetValue(app.Id, out var latestVersion) &&
                    !string.IsNullOrWhiteSpace(latestVersion))
                {
                    app.Version = latestVersion;
                }
            }

            var appsWithUpdates = _appUpdateService.GetAppsWithUpdates(appSnapshot);
            if (appsWithUpdates.Count == 0)
            {
                _loggingService?.LogInfo("[ScheduledUpdates] All apps are up to date.");
                await PersistLastRunAsync(cancellationToken);
                return;
            }

            var updatedCount = 0;
            foreach (var app in appsWithUpdates)
            {
                try
                {
                    await _appUpdateService.UpdateAllAsync([app], _installerService);
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    _loggingService?.LogError($"[ScheduledUpdates] Update failed for {app.Name}: {ex.Message}");
                }
            }

            await PersistLastRunAsync(cancellationToken);
            _loggingService?.LogInfo($"[ScheduledUpdates] Scheduled update complete. {updatedCount} app(s) updated.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _loggingService?.LogDebug("[ScheduledUpdates] Scheduled update run canceled.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"[ScheduledUpdates] Scheduled update failed: {ex.Message}");
        }
        finally
        {
            _runGate.Release();
        }
    }

    public static DateTime CalculateNextScheduledRun(AppSettings settings, DateTime? nowLocal = null)
    {
        settings ??= AppSettings.CreateDefault();
        var now = nowLocal ?? DateTime.Now;
        var scheduledHour = Math.Clamp(settings.ScheduledUpdateHour, 0, 23);
        var scheduledTimeToday = new DateTime(now.Year, now.Month, now.Day, scheduledHour, 0, 0, now.Kind);

        return settings.ScheduledUpdateFrequency switch
        {
            AppSettings.ScheduledFrequencyDaily => scheduledTimeToday > now
                ? scheduledTimeToday
                : scheduledTimeToday.AddDays(1),
            AppSettings.ScheduledFrequencyMonthly => CalculateNextMonthlyRun(now, scheduledHour),
            _ => CalculateNextWeeklyRun(now, settings.ScheduledUpdateDay, scheduledHour)
        };
    }

    private async Task RunTimerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_timer is not null && await _timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!_settings.ScheduledUpdatesEnabled || !ShouldRunNow())
                {
                    continue;
                }

                await RunScheduledUpdateAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _loggingService?.LogDebug("[ScheduledUpdates] Timer loop stopped.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"[ScheduledUpdates] Background timer failed: {ex.Message}");
        }
    }

    private bool ShouldRunNow()
    {
        var now = DateTime.Now;
        var lastRunUtc = NormalizeToUtc(_settings.LastScheduledUpdateRun);
        var currentHourMatches = now.Hour == Math.Clamp(_settings.ScheduledUpdateHour, 0, 23);

        return _settings.ScheduledUpdateFrequency switch
        {
            AppSettings.ScheduledFrequencyDaily =>
                currentHourMatches &&
                DateTime.UtcNow - lastRunUtc > DailyMinimumInterval,
            AppSettings.ScheduledFrequencyMonthly =>
                now.Day == 1 &&
                currentHourMatches &&
                DateTime.UtcNow - lastRunUtc > MonthlyMinimumInterval,
            _ =>
                now.DayOfWeek == _settings.ScheduledUpdateDay &&
                currentHourMatches &&
                DateTime.UtcNow - lastRunUtc > WeeklyMinimumInterval
        };
    }

    private void HandleSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isStarted || !OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppSettings.ScheduledUpdatesEnabled):
            case nameof(AppSettings.ScheduledUpdateFrequency):
            case nameof(AppSettings.ScheduledUpdateHour):
            case nameof(AppSettings.ScheduledUpdateDay):
            case nameof(AppSettings.RunMissedUpdatesASAP):
                SyncRegistration();
                break;
        }
    }

    private void EnsureSettingsSubscription()
    {
        if (_isSettingsSubscribed)
        {
            return;
        }

        _settings.PropertyChanged += HandleSettingsChanged;
        _isSettingsSubscribed = true;
    }

    private void SyncRegistration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!_settings.ScheduledUpdatesEnabled)
        {
            _taskSchedulerService.UnregisterTask();
            return;
        }

        _taskSchedulerService.RegisterTask(_settings);
    }

    private async Task PersistLastRunAsync(CancellationToken cancellationToken)
    {
        var runTimestampUtc = DateTime.UtcNow;

        if (Application.Current is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => _settings.LastScheduledUpdateRun = runTimestampUtc);
        }
        else
        {
            _settings.LastScheduledUpdateRun = runTimestampUtc;
        }

        await _settingsService.SaveSettingsAsync(_settings, cancellationToken);
    }

    private void StopFallbackTimer()
    {
        try
        {
            _stopCts?.Cancel();
        }
        catch
        {
            // Best-effort cancellation only.
        }

        _timer?.Dispose();
        _timer = null;
        _stopCts?.Dispose();
        _stopCts = null;
        _timerLoopTask = null;
    }

    private static DateTime CalculateNextWeeklyRun(DateTime now, DayOfWeek scheduledDay, int scheduledHour)
    {
        var scheduledThisHour = new DateTime(now.Year, now.Month, now.Day, scheduledHour, 0, 0, now.Kind);
        var dayOffset = ((int)scheduledDay - (int)now.DayOfWeek + 7) % 7;
        var candidate = scheduledThisHour.AddDays(dayOffset);
        return candidate > now ? candidate : candidate.AddDays(7);
    }

    private static DateTime CalculateNextMonthlyRun(DateTime now, int scheduledHour)
    {
        var candidate = new DateTime(now.Year, now.Month, 1, scheduledHour, 0, 0, now.Kind);
        if (candidate > now)
        {
            return candidate;
        }

        var nextMonth = now.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, 1, scheduledHour, 0, 0, nextMonth.Kind);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        if (value == DateTime.MinValue)
        {
            return DateTime.MinValue;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string GetCurrentPlatformId()
    {
        if (OperatingSystem.IsWindows())
        {
            return PlatformService.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return PlatformService.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return PlatformService.Unknown;
    }
}
