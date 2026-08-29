using System;
using System.IO;

namespace DownloaderApp.Models;

public enum PostDownloadAction
{
    None,
    Shutdown,
    Sleep,
    Hibernate
}

public class AppSettings
{
    public bool IsDarkMode { get; set; } = true;
    public string DefaultDownloadDirectory { get; set; } = string.Empty;
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int DefaultThreadsPerDownload { get; set; } = 8;
    public long GlobalSpeedLimitBytesPerSec { get; set; } = 0; // 0 = Unlimited
    public bool IsSmartFolderRoutingEnabled { get; set; } = true;
    public bool IsAutoExtractZipEnabled { get; set; } = false;
    public bool IsClipboardSnifferEnabled { get; set; } = true;
    public bool IsSoundEnabled { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = false;
    public PostDownloadAction PostDownloadAction { get; set; } = PostDownloadAction.None;

    public AppSettings()
    {
        DefaultDownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}
