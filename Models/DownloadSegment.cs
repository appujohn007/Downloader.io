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

    public double ProgressPercentage
    {
        get
        {
            if (TotalBytes <= 0) return 0.0;
            var pct = (double)DownloadedBytes / TotalBytes * 100.0;
            return Math.Clamp(pct, 0.0, 100.0);
        }
    }

    public string FormattedRange => $"{DownloadItem.FormatBytes(StartByte)} - {DownloadItem.FormatBytes(EndByte)}";

    public string FormattedProgress => TotalBytes > 0 
        ? $"{DownloadItem.FormatBytes(DownloadedBytes)} / {DownloadItem.FormatBytes(TotalBytes)} ({ProgressPercentage:0.#}%)"
        : $"{DownloadItem.FormatBytes(DownloadedBytes)}";

    public string FormattedSpeed => SpeedBytesPerSec > 0 ? $"{DownloadItem.FormatBytes((long)SpeedBytesPerSec)}/s" : "-";

    public void UpdateMetrics(long downloaded, double speed)
    {
        DownloadedBytes = downloaded;
        SpeedBytesPerSec = speed;
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(FormattedProgress));
        OnPropertyChanged(nameof(FormattedSpeed));
    }
}

