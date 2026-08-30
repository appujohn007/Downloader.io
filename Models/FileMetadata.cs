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

    // Telemetry fields
    public string HttpProtocol { get; set; } = "HTTP/1.1";
    public string Server { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public string LastModified { get; set; } = string.Empty;
    public string AcceptRanges { get; set; } = string.Empty;
    public string ContentEncoding { get; set; } = string.Empty;
    public string CdnProvider { get; set; } = "Direct Origin";
    public string CdnRayId { get; set; } = string.Empty;
    public double TtfbMs { get; set; }
    public string ResponseHeadersText { get; set; } = string.Empty;
    public string RequestHeadersText { get; set; } = string.Empty;

    public string FormattedSize => FileSize > 0 ? DownloadItem.FormatBytes(FileSize) : "Unknown Size";
    public string ResumableText => IsResumable ? "Resumable" : "Non-Resumable";
    public string DisplayType => !string.IsNullOrEmpty(ContentType) ? ContentType : (Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant() + " File");
}
