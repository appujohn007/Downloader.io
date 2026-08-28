using System;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DownloaderApp.Models;

public partial class DownloadItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _saveDirectory = string.Empty;

    [ObservableProperty]
    private long _totalBytes = -1;

    [ObservableProperty]
    private long _downloadedBytes = 0;

    [ObservableProperty]
    private double _progressPercentage = 0.0;

    [ObservableProperty]
    private double _speedBytesPerSec = 0.0;

    [ObservableProperty]
    private DownloadStatus _status = DownloadStatus.Queued;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime? _completedAt;

    public string FullPath => Path.Combine(SaveDirectory, FileName);

    public CancellationTokenSource? Cts { get; set; }

    public DownloadCategory Category => DetermineCategory(FileName);

    public string FormattedSize
    {
        get
        {
            if (TotalBytes <= 0)
                return FormatBytes(DownloadedBytes);
            return $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";
        }
    }

    public string FormattedSpeed
    {
        get
        {
            if (Status != DownloadStatus.Downloading || SpeedBytesPerSec <= 0)
                return string.Empty;
            return $"{FormatBytes((long)SpeedBytesPerSec)}/s";
        }
    }

    public string FormattedEta
    {
        get
        {
            if (Status != DownloadStatus.Downloading || SpeedBytesPerSec <= 0 || TotalBytes <= 0)
                return string.Empty;

            var remainingBytes = TotalBytes - DownloadedBytes;
            if (remainingBytes <= 0) return "Finishing...";

            var secondsRemaining = (double)remainingBytes / SpeedBytesPerSec;
            var timeSpan = TimeSpan.FromSeconds(secondsRemaining);

            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m left";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s left";
            return $"{timeSpan.Seconds}s left";
        }
    }

    public string StatusDisplayText => Status switch
    {
        DownloadStatus.Queued => "Queued",
        DownloadStatus.Connecting => "Connecting...",
        DownloadStatus.Downloading => "Downloading",
        DownloadStatus.Paused => "Paused",
        DownloadStatus.Completed => "Completed",
        DownloadStatus.Failed => "Failed",
        DownloadStatus.Canceled => "Canceled",
        _ => Status.ToString()
    };

    public bool IsActive => Status == DownloadStatus.Downloading || Status == DownloadStatus.Connecting;
    public bool CanPause => Status == DownloadStatus.Downloading || Status == DownloadStatus.Connecting;
    public bool CanResume => Status == DownloadStatus.Paused || Status == DownloadStatus.Failed;
    public bool IsCompleted => Status == DownloadStatus.Completed;

    public void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(FormattedSize));
        OnPropertyChanged(nameof(FormattedSpeed));
        OnPropertyChanged(nameof(FormattedEta));
        OnPropertyChanged(nameof(StatusDisplayText));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(IsCompleted));
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static DownloadCategory DetermineCategory(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".iso" => DownloadCategory.Compressed,
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" or ".apk" or ".dmg" or ".pkg" => DownloadCategory.Programs,
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".mp3" or ".flac" or ".wav" or ".aac" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => DownloadCategory.Media,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".md" or ".csv" or ".json" or ".xml" => DownloadCategory.Documents,
            _ => DownloadCategory.Other
        };
    }
}
