using System;

namespace DownloaderApp.Models;

public class FileMetadata
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; } = -1;
    public string ContentType { get; set; } = string.Empty;
    public bool IsResumable { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public string FormattedSize => FileSize > 0 ? DownloadItem.FormatBytes(FileSize) : "Unknown size";
}
