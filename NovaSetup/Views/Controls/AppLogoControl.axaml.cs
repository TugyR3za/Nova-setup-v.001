using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NovaSetup.Views.Controls;

public partial class AppLogoControl : UserControl
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly SemaphoreSlim DownloadThrottle = new(4, 4);
    private static readonly ConcurrentDictionary<string, Task<string?>> SvgPathCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex PathRegex = new(
        "<path[^>]*d=[\"'](?<data>.*?)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private int _loadVersion;
    private CancellationTokenSource? _loadCts;

    public static readonly StyledProperty<string?> LogoUrlProperty =
        AvaloniaProperty.Register<AppLogoControl, string?>(nameof(LogoUrl));

    public static readonly StyledProperty<string> FallbackGlyphProperty =
        AvaloniaProperty.Register<AppLogoControl, string>(nameof(FallbackGlyph), "?");

    public static readonly StyledProperty<double> LogoSizeProperty =
        AvaloniaProperty.Register<AppLogoControl, double>(nameof(LogoSize), 32d);

    public AppLogoControl()
    {
        InitializeComponent();
        ApplySize();
        ApplyFallbackGlyph();
        ApplyLogoPath(null);
    }

    public string? LogoUrl
    {
        get => GetValue(LogoUrlProperty);
        set => SetValue(LogoUrlProperty, value);
    }

    public string FallbackGlyph
    {
        get => GetValue(FallbackGlyphProperty);
        set => SetValue(FallbackGlyphProperty, value);
    }

    public double LogoSize
    {
        get => GetValue(LogoSizeProperty);
        set => SetValue(LogoSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LogoSizeProperty)
        {
            ApplySize();
            return;
        }

        if (change.Property == FallbackGlyphProperty)
        {
            ApplyFallbackGlyph();
            return;
        }

        if (change.Property == LogoUrlProperty)
        {
            _ = LoadLogoAsync();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelPendingLoad();
        base.OnDetachedFromVisualTree(e);
    }

    private async Task LoadLogoAsync()
    {
        var currentVersion = Interlocked.Increment(ref _loadVersion);
        ResetPendingLoadCancellation();
        var url = LogoUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            ApplyLogoPath(null);
            return;
        }

        string? pathData;
        try
        {
            pathData = await GetCachedSvgPathAsync(url, _loadCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            pathData = null;
        }

        if (currentVersion != _loadVersion)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => ApplyLogoPath(pathData));
    }

    private void ResetPendingLoadCancellation()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _loadCts, next);
        if (previous is null)
        {
            return;
        }

        try
        {
            previous.Cancel();
        }
        catch
        {
            // Best-effort cancellation only.
        }
        finally
        {
            previous.Dispose();
        }
    }

    private void CancelPendingLoad()
    {
        var current = Interlocked.Exchange(ref _loadCts, null);
        if (current is null)
        {
            return;
        }

        try
        {
            current.Cancel();
        }
        catch
        {
            // Best-effort cancellation only.
        }
        finally
        {
            current.Dispose();
        }
    }

    private void ApplySize()
    {
        var size = Math.Max(16d, LogoSize);
        LogoContainer.Width = size;
        LogoContainer.Height = size;
        LogoContainer.CornerRadius = new CornerRadius(Math.Round(size / 3.2));
        LogoViewbox.Width = size * 0.68;
        LogoViewbox.Height = size * 0.68;
        FallbackGlyphText.FontSize = Math.Max(12d, size * 0.42);
    }

    private void ApplyFallbackGlyph()
    {
        FallbackGlyphText.Text = string.IsNullOrWhiteSpace(FallbackGlyph) ? "?" : FallbackGlyph;
    }

    private void ApplyLogoPath(string? pathData)
    {
        if (!string.IsNullOrWhiteSpace(pathData))
        {
            try
            {
                LogoPath.Data = Geometry.Parse(pathData);
                LogoViewbox.IsVisible = true;
                FallbackGlyphText.IsVisible = false;
                return;
            }
            catch
            {
                // Fall back to the monogram placeholder.
            }
        }

        LogoPath.Data = null;
        LogoViewbox.IsVisible = false;
        FallbackGlyphText.IsVisible = true;
    }

    private static async Task<string?> DownloadSvgPathAsync(string url)
    {
        await DownloadThrottle.WaitAsync().ConfigureAwait(false);
        try
        {
            using var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var svg = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(svg))
            {
                return null;
            }

            var pathMatch = PathRegex.Match(svg);
            if (!pathMatch.Success)
            {
                return null;
            }

            return System.Net.WebUtility.HtmlDecode(pathMatch.Groups["data"].Value);
        }
        catch
        {
            return null;
        }
        finally
        {
            DownloadThrottle.Release();
        }
    }

    private static async Task<string?> GetCachedSvgPathAsync(string url, CancellationToken cancellationToken)
    {
        var sharedTask = SvgPathCache.GetOrAdd(url, DownloadSvgPathAsync);
        return await sharedTask.WaitAsync(cancellationToken);
    }
}
