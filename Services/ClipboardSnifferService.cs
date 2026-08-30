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
    public event Action<string>? DownloadableUrlDetected
    {
        add { }
        remove { }
    }
    public bool IsRunning => false;

    public ClipboardSnifferService(IClipboardService? clipboardService = null, ISettingsService? settingsService = null)
    {
    }

    public void Start()
    {
        // Background polling disabled - In-app Ctrl+V / Paste / Drop is used instead.
    }

    public void Stop()
    {
    }
}
