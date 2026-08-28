using System;
using System.IO;

namespace DownloaderApp.Models;

public class FileMetadata
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; } = -1;
    public string ContentType { get; set; } = string.Empty;
    public bool IsResumable { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;

    public string FormattedSize => FileSize > 0 ? DownloadItem.FormatBytes(FileSize) : "Unknown Size";
    public string ResumableText => IsResumable ? "Resumable" : "Non-Resumable";
    public string DisplayType => !string.IsNullOrEmpty(ContentType) ? ContentType : (Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant() + " File");
}
