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
    public int UserAgentPresetIndex { get; set; } = 0;
    public string CustomUserAgent { get; set; } = string.Empty;

    public string GetEffectiveUserAgent()
    {
        return UserAgentPresetIndex switch
        {
            0 => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
            1 => "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0",
            2 => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 Edg/128.0.0.0",
            3 => "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15",
            4 => "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.6613.88 Mobile Safari/537.36",
            5 => "Downloader.io/2.0 (Windows NT 10.0; Win64; x64) NativeEngine/2.4",
            6 => !string.IsNullOrWhiteSpace(CustomUserAgent) ? CustomUserAgent.Trim() : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
            _ => "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
        };
    }

    public AppSettings()
    {
        DefaultDownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}
