using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace DownloaderApp.Services;

public interface IClipboardSnifferService
{
    event Action<string>? DownloadableUrlDetected;
    void Start();
    void Stop();
    bool IsRunning { get; }
}

public class ClipboardSnifferService : IClipboardSnifferService
{
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private string _lastSeenClipboard = string.Empty;
    private bool _isFirstTick = true;

    private static readonly string[] DownloadableExtensions = new[]
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".iso", ".img",
        ".exe", ".msi", ".bat", ".apk", ".dmg", ".pkg", ".deb", ".rpm",
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".mp3", ".flac", ".wav",
        ".pdf", ".epub", ".bin", ".whl", ".jar"
    };

    public event Action<string>? DownloadableUrlDetected;
    public bool IsRunning => _timer.IsEnabled;

    public ClipboardSnifferService(IClipboardService clipboardService, ISettingsService settingsService)
    {
        _clipboardService = clipboardService;
        _settingsService = settingsService;

        _settingsService.SettingsChanged += OnSettingsChanged;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _timer.Tick += async (s, e) => await CheckClipboardAsync();
    }

    private void OnSettingsChanged(Models.AppSettings settings)
    {
        if (settings.IsClipboardSnifferEnabled && !_timer.IsEnabled)
        {
            Start();
        }
        else if (!settings.IsClipboardSnifferEnabled && _timer.IsEnabled)
        {
            Stop();
        }
    }

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _isFirstTick = true;
            _timer.Start();
            Logger.Debug("[SNIFFER] Clipboard sniffer active (startup auto-popup suppressed).");
        }
    }

    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            Logger.Debug("[SNIFFER] Clipboard sniffer paused.");
        }
    }

    private async Task CheckClipboardAsync()
    {
        if (!_settingsService.CurrentSettings.IsClipboardSnifferEnabled) return;

        try
        {
            var text = await _clipboardService.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text)) return;

            // On the very first tick after app startup/window creation, seed existing clipboard and NEVER pop up
            if (_isFirstTick)
            {
                _isFirstTick = false;
                _lastSeenClipboard = text;
                return;
            }

            if (text == _lastSeenClipboard) return;

            _lastSeenClipboard = text;

            var trimmed = text.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && 
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var path = uri.LocalPath;
                var ext = Path.GetExtension(path).ToLowerInvariant();

                bool isDownloadable = DownloadableExtensions.Contains(ext) ||
                                      uri.Query.Contains("download", StringComparison.OrdinalIgnoreCase) ||
                                      path.Contains("/download/", StringComparison.OrdinalIgnoreCase) ||
                                      path.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase);

                if (isDownloadable)
                {
                    Logger.Info($"[SNIFFER] Detected new downloadable URL copied to clipboard: {trimmed}");
                    DownloadableUrlDetected?.Invoke(trimmed);
                }
            }
        }
        catch
        {
            // Ignore clipboard access exceptions
        }
    }
}
