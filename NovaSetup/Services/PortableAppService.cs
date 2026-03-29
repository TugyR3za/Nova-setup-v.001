using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NovaSetup.Models;

namespace NovaSetup.Services;

public class PortableAppService
{
    private readonly LoggingService? _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    public PortableAppService(LoggingService? loggingService = null)
    {
        _logger = loggingService;
    }

    public async Task<bool> InstallPortableAsync(AppItem app, string destinationFolder)
    {
        string? tempFile = null;
        try
        {
            var archiveUrl = app.WindowsInstall?.PortableArchiveUrl;
            if (string.IsNullOrEmpty(archiveUrl))
            {
                _logger?.Log($"[PortableApp] No archive URL for {app.Name} - skipping.");
                return false;
            }

            var archiveType = app.WindowsInstall?.PortableArchiveType ?? "zip";
            if (archiveType == "7z")
            {
                _logger?.Log($"[PortableApp] 7z extraction not yet supported - skipping {app.Name}.");
                return false;
            }

            Directory.CreateDirectory(destinationFolder);
            var appFolder = Path.Combine(destinationFolder, app.Id);
            tempFile = Path.Combine(Path.GetTempPath(), $"nova_portable_{app.Id}.{archiveType}");

            _logger?.Log($"[PortableApp] Downloading {app.Name} from {archiveUrl}...");
            if (!Uri.TryCreate(archiveUrl, UriKind.Absolute, out var archiveUri))
            {
                _logger?.Log($"[PortableApp] Invalid archive URL for {app.Name}.");
                return false;
            }

            var totalBytes = await TryGetContentLengthAsync(archiveUri);
            await UpdateDownloadProgressAsync(app, 0, totalBytes);

            using var response = await _httpClient.GetAsync(archiveUri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.Log($"[PortableApp] Download failed for {app.Name}: HTTP {(int)response.StatusCode}.");
                return false;
            }

            totalBytes = totalBytes > 0 ? totalBytes : (response.Content.Headers.ContentLength ?? 0);
            if (totalBytes > 0)
            {
                await UpdateDownloadProgressAsync(app, 0, totalBytes);
            }

            await using (var responseStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long downloadedBytes = 0;
                int bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;
                    await UpdateDownloadProgressAsync(app, downloadedBytes, totalBytes);
                }
            }

            _logger?.Log($"[PortableApp] Extracting {app.Name} to {appFolder}...");
            if (Directory.Exists(appFolder))
            {
                Directory.Delete(appFolder, recursive: true);
            }

            ZipFile.ExtractToDirectory(tempFile, appFolder);
            app.PortableInstallPath = appFolder;

            _logger?.Log($"[PortableApp] Installed {app.Name} to {appFolder}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Log($"[PortableApp] Failed to install {app.Name}: {ex.Message}");
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempFile) && File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Cleanup failure is non-fatal.
                }
            }

            await ResetDownloadProgressAsync(app);
        }
    }

    public string? FindPortableExe(AppItem app, string installFolder)
    {
        try
        {
            var exeName = app.WindowsInstall?.PortableExecutable;
            if (string.IsNullOrEmpty(exeName))
            {
                return null;
            }

            var appFolder = Path.Combine(installFolder, app.Id);
            if (!Directory.Exists(appFolder))
            {
                return null;
            }

            foreach (var file in Directory.EnumerateFiles(appFolder, exeName, SearchOption.AllDirectories))
            {
                return file;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.Log($"[PortableApp] Error finding exe for {app.Name}: {ex.Message}");
            return null;
        }
    }

    private static async Task<long> TryGetContentLengthAsync(Uri uri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            return response.Content.Headers.ContentLength ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task UpdateDownloadProgressAsync(AppItem app, long downloadedBytes, long totalBytes)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            app.DownloadedBytes = downloadedBytes;
            app.TotalBytes = totalBytes;
            app.DownloadProgressPercent = totalBytes > 0
                ? Math.Clamp(downloadedBytes / (double)totalBytes * 100d, 0d, 100d)
                : 0d;
            app.DownloadProgressText = totalBytes > 0
                ? $"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}"
                : $"{FormatBytes(downloadedBytes)} downloaded";
        });
    }

    private static async Task ResetDownloadProgressAsync(AppItem app)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            app.DownloadedBytes = 0;
            app.TotalBytes = 0;
            app.DownloadProgressText = string.Empty;
            app.DownloadProgressPercent = 0;
        });
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024L * 1024L)
        {
            return $"{bytes / 1024d:F1} KB";
        }

        if (bytes < 1024L * 1024L * 1024L)
        {
            return $"{bytes / (1024d * 1024d):F1} MB";
        }

        return $"{bytes / (1024d * 1024d * 1024d):F2} GB";
    }
}
