using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class TaskSchedulerService
{
    private const string TaskName = "NovaSetup Scheduled Updates";
    private const string TaskDescription = "Automatically updates installed apps via NovaSetup";

    private readonly LoggingService? _loggingService;

    public TaskSchedulerService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public void RegisterTask(AppSettings settings)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!ElevationService.IsRunningAsAdministrator())
        {
            _loggingService?.LogWarning(
                "[TaskScheduler] Scheduled update task registration requires administrator rights.");
            return;
        }

        try
        {
            var executablePath = ResolveNovaExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                _loggingService?.LogError("[TaskScheduler] NovaSetup.exe could not be resolved for task registration.");
                return;
            }

            using var taskService = new TaskService();
            var definition = taskService.NewTask();
            definition.RegistrationInfo.Description = TaskDescription;
            definition.Principal.UserId = "SYSTEM";
            definition.Principal.LogonType = TaskLogonType.ServiceAccount;
            definition.Principal.RunLevel = TaskRunLevel.Highest;
            definition.Settings.StartWhenAvailable = settings.RunMissedUpdatesASAP;
            definition.Settings.ExecutionTimeLimit = settings.RunMissedUpdatesASAP
                ? TimeSpan.Zero
                : TimeSpan.FromHours(4);
            definition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.Hidden = false;

            definition.Triggers.Add(BuildTrigger(settings));
            definition.Actions.Add(new ExecAction(
                executablePath,
                "--scheduled-update --silent",
                Path.GetDirectoryName(executablePath)));

            taskService.RootFolder.RegisterTaskDefinition(
                TaskName,
                definition,
                TaskCreation.CreateOrUpdate,
                "SYSTEM",
                null,
                TaskLogonType.ServiceAccount);

            _loggingService?.LogInfo("[TaskScheduler] Registered Windows scheduled update task.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"[TaskScheduler] Failed to register task: {ex.Message}");
        }
    }

    public void UnregisterTask()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!ElevationService.IsRunningAsAdministrator())
        {
            _loggingService?.LogWarning(
                "[TaskScheduler] Scheduled update task removal requires administrator rights.");
            return;
        }

        try
        {
            using var taskService = new TaskService();
            if (taskService.GetTask(TaskName) is null)
            {
                return;
            }

            taskService.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
            _loggingService?.LogInfo("[TaskScheduler] Unregistered Windows scheduled update task.");
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"[TaskScheduler] Failed to unregister task: {ex.Message}");
        }
    }

    public bool IsTaskRegistered()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var taskService = new TaskService();
            return taskService.GetTask(TaskName) is not null;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"[TaskScheduler] Could not query task registration: {ex.Message}");
            return false;
        }
    }

    public DateTime? GetNextRunTime()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var taskService = new TaskService();
            var task = taskService.GetTask(TaskName);
            if (task is null)
            {
                return null;
            }

            var nextRun = task.NextRunTime;
            return nextRun == DateTime.MinValue ? null : nextRun;
        }
        catch (Exception ex)
        {
            _loggingService?.LogWarning($"[TaskScheduler] Could not query next run time: {ex.Message}");
            return null;
        }
    }

    private static Trigger BuildTrigger(AppSettings settings)
    {
        var startBoundary = CreateStartBoundary(settings.ScheduledUpdateHour);

        return settings.ScheduledUpdateFrequency switch
        {
            AppSettings.ScheduledFrequencyDaily => new DailyTrigger
            {
                StartBoundary = startBoundary,
                DaysInterval = 1
            },
            AppSettings.ScheduledFrequencyMonthly => new MonthlyTrigger
            {
                StartBoundary = startBoundary,
                DaysOfMonth = [1],
                MonthsOfYear = MonthsOfTheYear.AllMonths
            },
            _ => new WeeklyTrigger
            {
                StartBoundary = startBoundary,
                DaysOfWeek = MapDayOfWeek(settings.ScheduledUpdateDay),
                WeeksInterval = 1
            }
        };
    }

    private static DateTime CreateStartBoundary(int scheduledHour)
    {
        var hour = Math.Clamp(scheduledHour, 0, 23);
        var today = DateTime.Today.AddHours(hour);
        return today > DateTime.Now ? today : today.AddDays(1);
    }

    private static DaysOfTheWeek MapDayOfWeek(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => DaysOfTheWeek.Monday,
            DayOfWeek.Tuesday => DaysOfTheWeek.Tuesday,
            DayOfWeek.Wednesday => DaysOfTheWeek.Wednesday,
            DayOfWeek.Thursday => DaysOfTheWeek.Thursday,
            DayOfWeek.Friday => DaysOfTheWeek.Friday,
            DayOfWeek.Saturday => DaysOfTheWeek.Saturday,
            _ => DaysOfTheWeek.Sunday
        };
    }

    private static string? ResolveNovaExecutablePath()
    {
        var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            !string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var candidate = Path.Combine(AppContext.BaseDirectory, "NovaSetup.exe");
        return File.Exists(candidate) ? candidate : processPath;
    }
}
