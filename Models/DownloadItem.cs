using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DownloaderApp.Models;

public enum FailureScenario
{
    None,
    CloudflareChallenge,
    AuthRequired,
    NotFound,
    RateLimited,
    Timeout,
    DnsUnreachable,
    StorageError,
    Generic
}

public partial class DownloadItem : ObservableObject
{
    [ObservableProperty]
    private int _paletteIndex = Random.Shared.Next();

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

    [ObservableProperty]
    private int _maxSegments = 8;

    [ObservableProperty]
    private long _speedCapBytesPerSec = 0; // 0 = unlimited

    [ObservableProperty]
    private string? _checksumMd5;

    [ObservableProperty]
    private string? _checksumSha256;

    [ObservableProperty]
    private bool _isCalculatingHash;

    [ObservableProperty]
    private string? _expectedChecksum;

    [ObservableProperty]
    private bool? _isChecksumMatched;

    [ObservableProperty]
    private bool _autoExtractZip;

    [ObservableProperty]
    private DateTime? _scheduledStartTime;

    [ObservableProperty]
    private bool _isScheduled;

    [ObservableProperty]
    private string _serverHeadersSummary = string.Empty;

    [ObservableProperty]
    private int _retryAttempts = 0;

    public ObservableCollection<DownloadSegment> Segments { get; set; } = new();

    [JsonIgnore]
    public string FullPath => Path.Combine(SaveDirectory, FileName);

    [JsonIgnore]
    public string PartialPath => $"{FullPath}.downloaderio";

    [JsonIgnore]
    public string SegmentsMetaPath
    {
        get
        {
            var metaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Downloader.io",
                "metadata");

            var safeKey = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(FullPath))).ToLowerInvariant();
            return Path.Combine(metaDir, $"{safeKey}_{FileName}.meta");
        }
    }

    [JsonIgnore]
    public CancellationTokenSource? Cts { get; set; }

    [JsonIgnore]
    public DownloadCategory Category => DetermineCategory(FileName);

    [JsonIgnore]
    public string FormattedSize
    {
        get
        {
            if (TotalBytes <= 0)
                return $"{FormatBytes(DownloadedBytes)} / ?";
            return $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";
        }
    }

    [JsonIgnore]
    public string FormattedPercentageText
    {
        get
        {
            if (Status == DownloadStatus.Downloading && TotalBytes <= 0)
                return "Stream";
            if (Status == DownloadStatus.Connecting)
                return "0%";
            return $"{ProgressPercentage:0.#}%";
        }
    }

    [JsonIgnore]
    public string FormattedSpeed
    {
        get
        {
            if (Status != DownloadStatus.Downloading || SpeedBytesPerSec <= 0)
                return string.Empty;
            return $"{FormatBytes((long)SpeedBytesPerSec)}/s";
        }
    }

    [JsonIgnore]
    public string FormattedEta
    {
        get
        {
            if (Status != DownloadStatus.Downloading || SpeedBytesPerSec <= 1024 || TotalBytes <= 0)
                return string.Empty;

            var remainingBytes = TotalBytes - DownloadedBytes;
            if (remainingBytes <= 0) return "Finishing...";

            var secondsRemaining = (double)remainingBytes / SpeedBytesPerSec;
            if (secondsRemaining <= 0) return "Finishing...";
            if (secondsRemaining > 86400 * 7) return "> 7 days left";

            var timeSpan = TimeSpan.FromSeconds(secondsRemaining);

            if (timeSpan.TotalHours >= 24)
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h left";
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m left";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s left";
            return $"{timeSpan.Seconds}s left";
        }
    }

    [JsonIgnore]
    public string StatusDisplayText => Status switch
    {
        DownloadStatus.Queued => IsScheduled ? $"Scheduled for {ScheduledStartTime:HH:mm}" : "Queued",
        DownloadStatus.Connecting => RetryAttempts > 0 ? $"Reconnecting (Try {RetryAttempts})..." : "Connecting...",
        DownloadStatus.Downloading => TotalBytes <= 0 ? "Streaming..." : (Segments.Count > 1 ? $"Accelerated ({Segments.Count} threads)" : "Downloading"),
        DownloadStatus.Paused => "Paused",
        DownloadStatus.Completed => "Completed",
        DownloadStatus.Failed => "Failed",
        DownloadStatus.Canceled => "Canceled",
        _ => Status.ToString()
    };

    [JsonIgnore]
    public bool IsActive => Status == DownloadStatus.Downloading || Status == DownloadStatus.Connecting;

    [JsonIgnore]
    public bool IsConnecting => Status == DownloadStatus.Connecting;

    [JsonIgnore]
    public bool IsIndeterminate => TotalBytes <= 0 && Status == DownloadStatus.Downloading;

    [JsonIgnore]
    public bool CanPause => Status == DownloadStatus.Downloading || Status == DownloadStatus.Connecting;

    [JsonIgnore]
    public bool CanResume => Status == DownloadStatus.Paused || Status == DownloadStatus.Failed || (Status == DownloadStatus.Queued && IsScheduled);

    [JsonIgnore]
    public bool IsCompleted => Status == DownloadStatus.Completed;

    [JsonIgnore]
    public bool IsFailed => Status == DownloadStatus.Failed;

    [JsonIgnore]
    public string ScenarioDiagnosticText
    {
        get
        {
            if (Status != DownloadStatus.Failed) return string.Empty;
            var err = ErrorMessage ?? string.Empty;
            if (err.Contains("403") || err.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase))
                return "Cloudflare Bot Challenge / Access Denied (HTTP 403)";
            if (err.Contains("401") || err.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                return "Authentication Required / Login Protected (HTTP 401)";
            if (err.Contains("404") || err.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
                return "File Not Found on Remote Server (HTTP 404)";
            if (err.Contains("429") || err.Contains("Too Many", StringComparison.OrdinalIgnoreCase))
                return "Server Rate Limited / Slowdown Required (HTTP 429)";
            if (err.Contains("500") || err.Contains("502") || err.Contains("503") || err.Contains("504"))
                return "Remote Server Internal / Gateway Error";
            if (err.Contains("timeout", StringComparison.OrdinalIgnoreCase) || err.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return "Network Connection Timed Out";
            if (err.Contains("name resolution", StringComparison.OrdinalIgnoreCase) || err.Contains("host", StringComparison.OrdinalIgnoreCase))
                return "DNS Resolution / Host Unreachable";
            if (err.Contains("disk", StringComparison.OrdinalIgnoreCase) || err.Contains("space", StringComparison.OrdinalIgnoreCase))
                return "Insufficient Local Storage Space";
            if (err.Contains("denied", StringComparison.OrdinalIgnoreCase) || err.Contains("access", StringComparison.OrdinalIgnoreCase))
                return "Local File Access / Permission Denied";
            return !string.IsNullOrWhiteSpace(err) ? err : "Download Failed (Click Retry to Reconnect)";
        }
    }

    [JsonIgnore]
    public string ScenarioDiagnosticIcon
    {
        get
        {
            if (Status != DownloadStatus.Failed) return string.Empty;
            var err = ErrorMessage ?? string.Empty;
            if (err.Contains("403") || err.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase))
                return "🛡️";
            if (err.Contains("401"))
                return "🔒";
            if (err.Contains("404"))
                return "🔍";
            if (err.Contains("429"))
                return "⏳";
            if (err.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "⏱️";
            if (err.Contains("DNS", StringComparison.OrdinalIgnoreCase) || err.Contains("resolution", StringComparison.OrdinalIgnoreCase))
                return "🌐";
            return "⚠️";
        }
    }

    [JsonIgnore]
    public FailureScenario ScenarioType
    {
        get
        {
            if (Status != DownloadStatus.Failed) return FailureScenario.None;
            var err = ErrorMessage ?? string.Empty;
            if (err.Contains("403") || err.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase))
                return FailureScenario.CloudflareChallenge;
            if (err.Contains("401") || err.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                return FailureScenario.AuthRequired;
            if (err.Contains("404") || err.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
                return FailureScenario.NotFound;
            if (err.Contains("429") || err.Contains("Too Many", StringComparison.OrdinalIgnoreCase))
                return FailureScenario.RateLimited;
            if (err.Contains("timeout", StringComparison.OrdinalIgnoreCase) || err.Contains("timed out", StringComparison.OrdinalIgnoreCase) || err.Contains("504") || err.Contains("408"))
                return FailureScenario.Timeout;
            if (err.Contains("name resolution", StringComparison.OrdinalIgnoreCase) || err.Contains("host", StringComparison.OrdinalIgnoreCase) || err.Contains("dns", StringComparison.OrdinalIgnoreCase))
                return FailureScenario.DnsUnreachable;
            if (err.Contains("disk", StringComparison.OrdinalIgnoreCase) || err.Contains("space", StringComparison.OrdinalIgnoreCase) || err.Contains("denied", StringComparison.OrdinalIgnoreCase))
                return FailureScenario.StorageError;
            return FailureScenario.Generic;
        }
    }

    partial void OnStatusChanged(DownloadStatus value)
    {
        OnPropertyChanged(nameof(StatusDisplayText));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(ScenarioDiagnosticText));
        OnPropertyChanged(nameof(ScenarioDiagnosticIcon));
        OnPropertyChanged(nameof(ScenarioType));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(ScenarioDiagnosticText));
        OnPropertyChanged(nameof(ScenarioDiagnosticIcon));
        OnPropertyChanged(nameof(ScenarioType));
    }

    public void UpdateProgressMetrics(long downloaded, long total, double progressPct, double smoothedSpeed, bool updateSpeedDisplay)
    {
        DownloadedBytes = downloaded;
        if (total > 0) TotalBytes = total;
        ProgressPercentage = progressPct;
        
        OnPropertyChanged(nameof(FormattedSize));
        OnPropertyChanged(nameof(FormattedPercentageText));
        OnPropertyChanged(nameof(IsIndeterminate));

        if (updateSpeedDisplay)
        {
            SpeedBytesPerSec = smoothedSpeed;
            OnPropertyChanged(nameof(FormattedSpeed));
            OnPropertyChanged(nameof(FormattedEta));
            OnPropertyChanged(nameof(StatusDisplayText));
        }
    }

    public void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(FormattedSize));
        OnPropertyChanged(nameof(FormattedPercentageText));
        OnPropertyChanged(nameof(FormattedSpeed));
        OnPropertyChanged(nameof(FormattedEta));
        OnPropertyChanged(nameof(StatusDisplayText));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(IsIndeterminate));
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

    public static DownloadCategory DetermineCategory(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or ".iso" or ".img" or ".dmg" or ".vhd" => DownloadCategory.Compressed,
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" or ".apk" or ".pkg" or ".deb" or ".rpm" or ".whl" or ".jar" or ".sh" => DownloadCategory.Programs,
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".wmv" or ".flv" or ".mp3" or ".flac" or ".wav" or ".aac" or ".m4a" or ".ogg" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".bmp" or ".ico" => DownloadCategory.Media,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".epub" or ".mobi" or ".rtf" or ".bin" or ".dat" or ".log" or ".sql" or ".db" or ".torrent" or ".cfg" or ".ini" => DownloadCategory.Documents,
            _ => DownloadCategory.Other
        };
    }
}
