using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace NovaSetup.Services;

public static class ElevationService
{
    public static bool ElevationWasDenied { get; private set; }

    public static bool IsRunningAsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool RelaunchAsAdministrator(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = BuildArgumentString(args),
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            ElevationWasDenied = false;
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            ElevationWasDenied = true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildArgumentString(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            args.Select(arg => $"\"{arg.Replace("\"", "\\\"", StringComparison.Ordinal)}\""));
    }
}
