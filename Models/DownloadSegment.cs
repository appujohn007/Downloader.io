using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DownloaderApp.Models;

public partial class DownloadSegment : ObservableObject
{
    [ObservableProperty]
    private int _segmentId;

    [ObservableProperty]
    private long _startByte;

    [ObservableProperty]
    private long _endByte;

    [ObservableProperty]
    private long _downloadedBytes;

    [ObservableProperty]
    private double _speedBytesPerSec;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isCompleted;

    [JsonIgnore]
    public long TotalBytes => (EndByte >= StartByte) ? (EndByte - StartByte + 1) : -1;

    [JsonIgnore]
    public long CurrentOffset => StartByte + DownloadedBytes;

    [JsonIgnore]
    public double ProgressPercentage
    {
        get
        {
            if (TotalBytes <= 0) return 0.0;
            var pct = (double)DownloadedBytes / TotalBytes * 100.0;
            return Math.Clamp(pct, 0.0, 100.0);
        }
    }

    [JsonIgnore]
    public string FormattedStartPoint => DownloadItem.FormatBytes(StartByte);

    [JsonIgnore]
    public string FormattedEndPoint => DownloadItem.FormatBytes(EndByte);

    [JsonIgnore]
    public string FormattedCurrentOffset => DownloadItem.FormatBytes(CurrentOffset);

    [JsonIgnore]
    public string FormattedDownloaded => DownloadItem.FormatBytes(DownloadedBytes);

    [JsonIgnore]
    public string FormattedSegmentSize => DownloadItem.FormatBytes(TotalBytes);

    [JsonIgnore]
    public string FormattedRange => $"{FormattedStartPoint} → {FormattedEndPoint}";

    [JsonIgnore]
    public string FormattedProgress => TotalBytes > 0 
        ? $"{FormattedDownloaded} / {FormattedSegmentSize} ({ProgressPercentage:0.#}%)"
        : $"{FormattedDownloaded}";

    [JsonIgnore]
    public string FormattedSpeed => SpeedBytesPerSec > 0 ? $"{DownloadItem.FormatBytes((long)SpeedBytesPerSec)}/s" : "-";

    [JsonIgnore]
    public string StatusText => IsCompleted ? "Finished" : (IsActive ? "Downloading" : "Connecting");

    public void UpdateMetrics(long downloaded, double speed)
    {
        DownloadedBytes = downloaded;
        SpeedBytesPerSec = speed;
        OnPropertyChanged(nameof(CurrentOffset));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(FormattedProgress));
        OnPropertyChanged(nameof(FormattedSpeed));
        OnPropertyChanged(nameof(FormattedCurrentOffset));
        OnPropertyChanged(nameof(FormattedDownloaded));
        OnPropertyChanged(nameof(StatusText));
    }
}
