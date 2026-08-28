namespace DownloaderApp.Models;

public enum DownloadStatus
{
    Queued,
    Connecting,
    Downloading,
    Paused,
    Completed,
    Failed,
    Canceled
}

public enum DownloadCategory
{
    All,
    Documents,
    Compressed,
    Media,
    Programs,
    Other
}
