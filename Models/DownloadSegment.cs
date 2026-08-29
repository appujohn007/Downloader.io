using System;
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

    public long TotalBytes => (EndByte >= StartByte) ? (EndByte - StartByte + 1) : -1;
    public long CurrentOffset => StartByte + DownloadedBytes;

    public double ProgressPercentage
    {
        get
        {
            if (TotalBytes <= 0) return 0.0;
            var pct = (double)DownloadedBytes / TotalBytes * 100.0;
            return Math.Clamp(pct, 0.0, 100.0);
        }
    }

    public string FormattedStartPoint => DownloadItem.FormatBytes(StartByte);
    public string FormattedEndPoint => DownloadItem.FormatBytes(EndByte);
    public string FormattedCurrentOffset => DownloadItem.FormatBytes(CurrentOffset);
    public string FormattedDownloaded => DownloadItem.FormatBytes(DownloadedBytes);
    public string FormattedSegmentSize => DownloadItem.FormatBytes(TotalBytes);
    public string FormattedRange => $"{FormattedStartPoint} → {FormattedEndPoint}";

    public string FormattedProgress => TotalBytes > 0 
        ? $"{FormattedDownloaded} / {FormattedSegmentSize} ({ProgressPercentage:0.#}%)"
        : $"{FormattedDownloaded}";

    public string FormattedSpeed => SpeedBytesPerSec > 0 ? $"{DownloadItem.FormatBytes((long)SpeedBytesPerSec)}/s" : "-";

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
