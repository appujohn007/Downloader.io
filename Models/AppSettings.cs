using System;
using System.IO;

namespace DownloaderApp.Models;

public class AppSettings
{
    public bool IsDarkMode { get; set; } = true;
    public string DefaultDownloadDirectory { get; set; } = string.Empty;
    public int MaxConcurrentDownloads { get; set; } = 3;

    public AppSettings()
    {
        DefaultDownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}
