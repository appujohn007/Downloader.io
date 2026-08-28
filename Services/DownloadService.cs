using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using DownloaderApp.Models;

namespace DownloaderApp.Services;

public interface IDownloadService
{
    Task StartDownloadAsync(DownloadItem item);
    void PauseDownload(DownloadItem item);
    void ResumeDownload(DownloadItem item);
    void CancelDownload(DownloadItem item);
    string ExtractFileNameFromUrl(string url);
    Task<long> ProbeFileSizeAsync(string url, CancellationToken ct = default);
    Task<FileMetadata> ProbeMetadataAsync(string url, CancellationToken ct = default);
}

public class DownloadService : IDownloadService
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromHours(24)
    };

    private static readonly Dictionary<string, string> MimeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/zip", ".zip" },
        { "application/x-zip-compressed", ".zip" },
        { "application/x-rar-compressed", ".rar" },
        { "application/x-7z-compressed", ".7z" },
        { "application/x-tar", ".tar" },
        { "application/gzip", ".gz" },
        { "application/pdf", ".pdf" },
        { "application/json", ".json" },
        { "application/octet-stream", ".bin" },
        { "video/mp4", ".mp4" },
        { "video/x-matroska", ".mkv" },
        { "video/webm", ".webm" },
        { "video/quicktime", ".mov" },
        { "audio/mpeg", ".mp3" },
        { "audio/wav", ".wav" },
        { "audio/flac", ".flac" },
        { "audio/aac", ".aac" },
        { "image/png", ".png" },
        { "image/jpeg", ".jpg" },
        { "image/gif", ".gif" },
        { "image/webp", ".webp" },
        { "image/svg+xml", ".svg" },
        { "text/plain", ".txt" },
        { "text/html", ".html" }
    };

    static DownloadService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 Downloader.io/1.0");
    }

    public async Task<FileMetadata> ProbeMetadataAsync(string url, CancellationToken ct = default)
    {
        var meta = new FileMetadata { Url = url };

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            meta.ErrorMessage = "Invalid URL";
            meta.FileName = "download.bin";
            return meta;
        }

        try
        {
            Logger.Debug($"[METADATA] Probing headers for: {url}");

            HttpResponseMessage? response = null;

            // 1. Try HEAD request first
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
                response = await HttpClient.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex)
            {
                Logger.Debug($"HEAD request failed ({ex.Message}), falling back to GET Range request...");
            }

            // 2. If HEAD request returned MethodNotAllowed (405) or failed, try GET with Range: 0-0
            if (response == null || !response.IsSuccessStatusCode)
            {
                response?.Dispose();
                using var getReq = new HttpRequestMessage(HttpMethod.Get, url);
                getReq.Headers.Range = new RangeHeaderValue(0, 0);
                response = await HttpClient.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, ct);
            }

            using (response)
            {
                // Content Type
                if (response.Content.Headers.ContentType?.MediaType != null)
                {
                    meta.ContentType = response.Content.Headers.ContentType.MediaType;
                }

                // Resumable check
                meta.IsResumable = response.StatusCode == System.Net.HttpStatusCode.PartialContent ||
                                  response.Headers.AcceptRanges.Contains("bytes");

                // Content Length
                if (response.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    meta.FileSize = response.Content.Headers.ContentRange.Length.Value;
                }
                else if (response.Content.Headers.ContentLength.HasValue)
                {
                    meta.FileSize = response.Content.Headers.ContentLength.Value;
                }

                // Filename from Content-Disposition header
                string? detectedName = null;

                if (response.Content.Headers.ContentDisposition != null)
                {
                    detectedName = response.Content.Headers.ContentDisposition.FileNameStar ??
                                   response.Content.Headers.ContentDisposition.FileName;
                }

                // If not in parsed ContentDisposition, search raw header
                if (string.IsNullOrWhiteSpace(detectedName) && response.Content.Headers.TryGetValues("Content-Disposition", out var values))
                {
                    var raw = string.Join(";", values);
                    var matchStar = Regex.Match(raw, @"filename\*=UTF-8''([^;]+)", RegexOptions.IgnoreCase);
                    if (matchStar.Success)
                    {
                        detectedName = Uri.UnescapeDataString(matchStar.Groups[1].Value);
                    }
                    else
                    {
                        var match = Regex.Match(raw, @"filename=[""']?([^""';]+)[""']?", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            detectedName = match.Groups[1].Value;
                        }
                    }
                }

                // Fallback to effective final URI after redirects
                if (string.IsNullOrWhiteSpace(detectedName))
                {
                    var finalUri = response.RequestMessage?.RequestUri ?? uri;
                    detectedName = ExtractFileNameFromUrl(finalUri.AbsoluteUri);
                }

                // Clean detected name
                detectedName = CleanFileName(detectedName);

                // If extension is missing, check Content-Type MIME map
                if (!Path.HasExtension(detectedName) && !string.IsNullOrEmpty(meta.ContentType))
                {
                    if (MimeExtensions.TryGetValue(meta.ContentType, out var ext))
                    {
                        detectedName += ext;
                    }
                }

                meta.Domain = uri.Host;
                meta.FileName = string.IsNullOrWhiteSpace(detectedName) ? "download.bin" : detectedName;
                Logger.Info($"[METADATA DETECTED] File: '{meta.FileName}' | Size: {meta.FormattedSize} | Type: {meta.ContentType} | Resumable: {meta.IsResumable}");
            }
        }
        catch (OperationCanceledException)
        {
            meta.FileName = ExtractFileNameFromUrl(url);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Metadata probe encountered error: {ex.Message}");
            meta.FileName = ExtractFileNameFromUrl(url);
            meta.ErrorMessage = ex.Message;
        }

        return meta;
    }

    public string ExtractFileNameFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "download.file";

        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var localPath = uri.LocalPath.TrimEnd('/');
                var name = Path.GetFileName(localPath);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return CleanFileName(Uri.UnescapeDataString(name));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to extract filename from '{url}': {ex.Message}");
        }

        return "download.bin";
    }

    private static string CleanFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "download.bin";
        name = name.Trim(' ', '"', '\'', '\t', '\r', '\n');

        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    public async Task<long> ProbeFileSizeAsync(string url, CancellationToken ct = default)
    {
        var meta = await ProbeMetadataAsync(url, ct);
        return meta.FileSize;
    }

    public async Task StartDownloadAsync(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Downloading)
        {
            Logger.Warn($"Download '{item.FileName}' is already active. Ignoring start request.");
            return;
        }

        item.Cts?.Cancel();
        item.Cts = new CancellationTokenSource();
        var ct = item.Cts.Token;

        item.Status = DownloadStatus.Connecting;
        item.ErrorMessage = string.Empty;
        UpdateUi(item);

        Logger.Download($"[CONNECT] Starting download for '{item.FileName}' from: {item.Url}");

        try
        {
            if (!Directory.Exists(item.SaveDirectory))
            {
                Logger.Info($"Creating destination directory: {item.SaveDirectory}");
                Directory.CreateDirectory(item.SaveDirectory);
            }

            var targetFilePath = item.FullPath;
            long existingBytes = 0;

            if (File.Exists(targetFilePath))
            {
                existingBytes = new FileInfo(targetFilePath).Length;
                Logger.Download($"[RESUME] Found existing partial file '{targetFilePath}' with {DownloadItem.FormatBytes(existingBytes)}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);

            // Support Range request for resuming
            if (existingBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);
                Logger.Debug($"Sent HTTP header: Range: bytes={existingBytes}-");
            }

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            bool isRangeAccepted = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            Logger.Info($"[HTTP RESPONSE] Status: {(int)response.StatusCode} {response.ReasonPhrase} (Partial Content Accepted: {isRangeAccepted})");

            if (!response.IsSuccessStatusCode && !isRangeAccepted)
            {
                // If 416 Range Not Satisfiable, start fresh
                if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    Logger.Warn($"HTTP 416 Range Not Satisfiable. Deleting existing partial file and starting from byte 0.");
                    existingBytes = 0;
                    if (File.Exists(targetFilePath)) File.Delete(targetFilePath);

                    using var freshRequest = new HttpRequestMessage(HttpMethod.Get, item.Url);
                    using var freshResponse = await HttpClient.SendAsync(freshRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                    freshResponse.EnsureSuccessStatusCode();
                    await ProcessDownloadStreamAsync(item, freshResponse, targetFilePath, 0, ct);
                    return;
                }

                throw new HttpRequestException($"Server returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
            }

            long totalLength = -1;
            if (isRangeAccepted && response.Content.Headers.ContentRange?.Length.HasValue == true)
            {
                totalLength = response.Content.Headers.ContentRange.Length.Value;
            }
            else if (response.Content.Headers.ContentLength.HasValue)
            {
                totalLength = isRangeAccepted ? existingBytes + response.Content.Headers.ContentLength.Value : response.Content.Headers.ContentLength.Value;
            }

            if (!isRangeAccepted && existingBytes > 0)
            {
                Logger.Warn("Server does not support partial content ranges. Restarting download from byte 0.");
                existingBytes = 0;
            }

            Logger.Download($"[DOWNLOADING] '{item.FileName}' | Total Target Size: {DownloadItem.FormatBytes(totalLength)} | Destination: {targetFilePath}");
            await ProcessDownloadStreamAsync(item, response, targetFilePath, existingBytes, ct, totalLength);
        }
        catch (OperationCanceledException)
        {
            if (item.Status != DownloadStatus.Paused && item.Status != DownloadStatus.Canceled)
            {
                item.Status = DownloadStatus.Paused;
            }
            item.SpeedBytesPerSec = 0;
            UpdateUi(item);
            Logger.Warn($"[PAUSED/CANCELLED] Download '{item.FileName}' interrupted by user.");
        }
        catch (Exception ex)
        {
            item.Status = DownloadStatus.Failed;
            item.ErrorMessage = ex.Message;
            item.SpeedBytesPerSec = 0;
            UpdateUi(item);
            Logger.Error($"[FAILED] Download '{item.FileName}' encountered an error: {ex.Message}", ex);
        }
    }

    private async Task ProcessDownloadStreamAsync(
        DownloadItem item,
        HttpResponseMessage response,
        string targetFilePath,
        long initialBytes,
        CancellationToken ct,
        long totalLength = -1)
    {
        long currentDownloadedBytes = initialBytes;
        long currentTotalBytes = totalLength > 0 ? totalLength : item.TotalBytes;

        item.Status = DownloadStatus.Downloading;
        item.DownloadedBytes = initialBytes;
        if (totalLength > 0)
        {
            item.TotalBytes = totalLength;
        }

        UpdateUi(item);

        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(
            targetFilePath,
            initialBytes > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        int bytesRead;
        long bytesSinceLastSample = 0;
        double smoothedSpeed = 0.0;

        var speedSampleTimer = Stopwatch.StartNew();
        var uiProgressTimer = Stopwatch.StartNew();
        var uiSpeedDisplayTimer = Stopwatch.StartNew();
        var lastConsoleLog = Stopwatch.StartNew();

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);

            currentDownloadedBytes += bytesRead;
            bytesSinceLastSample += bytesRead;

            // Sample speed every 250ms with Exponential Moving Average (EMA)
            if (speedSampleTimer.ElapsedMilliseconds >= 250)
            {
                var elapsedSec = speedSampleTimer.Elapsed.TotalSeconds;
                if (elapsedSec > 0)
                {
                    var instantSpeed = bytesSinceLastSample / elapsedSec;
                    if (smoothedSpeed <= 0)
                    {
                        smoothedSpeed = instantSpeed;
                    }
                    else
                    {
                        // EMA smoothing factor: 0.25 (stable yet responsive)
                        smoothedSpeed = (0.25 * instantSpeed) + (0.75 * smoothedSpeed);
                    }
                }
                bytesSinceLastSample = 0;
                speedSampleTimer.Restart();
            }

            // Multi-tiered UI update cadence:
            // 1. Progress Bar & Downloaded bytes: update every ~100ms for smooth continuous fluid flow
            // 2. Speed text & ETA: update every ~700ms for human readability without flickering
            if (uiProgressTimer.ElapsedMilliseconds >= 100)
            {
                double progressPct = 0;
                if (currentTotalBytes > 0)
                {
                    progressPct = Math.Clamp(((double)currentDownloadedBytes / currentTotalBytes) * 100.0, 0, 100);
                }

                bool updateSpeedDisplay = uiSpeedDisplayTimer.ElapsedMilliseconds >= 700;
                if (updateSpeedDisplay)
                {
                    uiSpeedDisplayTimer.Restart();
                }

                long snapDownloaded = currentDownloadedBytes;
                long snapTotal = currentTotalBytes;
                double snapSpeed = smoothedSpeed;

                Dispatcher.UIThread.Post(() =>
                {
                    item.UpdateProgressMetrics(snapDownloaded, snapTotal, progressPct, snapSpeed, updateSpeedDisplay);
                }, DispatcherPriority.Normal);

                uiProgressTimer.Restart();
            }

            // Periodic terminal log every 3 seconds
            if (lastConsoleLog.ElapsedMilliseconds >= 3000)
            {
                double progressPct = currentTotalBytes > 0 ? ((double)currentDownloadedBytes / currentTotalBytes) * 100.0 : 0;
                Logger.Download($"[PROGRESS] '{item.FileName}': {progressPct:0.0}% | {DownloadItem.FormatBytes(currentDownloadedBytes)} / {DownloadItem.FormatBytes(currentTotalBytes)} | {DownloadItem.FormatBytes((long)smoothedSpeed)}/s");
                lastConsoleLog.Restart();
            }
        }

        await fileStream.FlushAsync(ct);

        item.Status = DownloadStatus.Completed;
        item.CompletedAt = DateTime.Now;
        item.SpeedBytesPerSec = 0;
        item.ProgressPercentage = 100;
        if (item.TotalBytes <= 0)
        {
            item.TotalBytes = currentDownloadedBytes;
        }
        item.DownloadedBytes = item.TotalBytes;

        UpdateUi(item);
        Logger.Success($"[COMPLETED] Successfully downloaded '{item.FileName}' ({DownloadItem.FormatBytes(item.DownloadedBytes)}) to '{targetFilePath}'");
    }

    public void PauseDownload(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.Connecting)
        {
            Logger.Info($"[USER ACTION] Pausing download for '{item.FileName}'");
            item.Status = DownloadStatus.Paused;
            item.SpeedBytesPerSec = 0;
            item.Cts?.Cancel();
            UpdateUi(item);
        }
    }

    public void ResumeDownload(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Paused || item.Status == DownloadStatus.Failed)
        {
            Logger.Info($"[USER ACTION] Resuming download for '{item.FileName}'");
            _ = StartDownloadAsync(item);
        }
    }

    public void CancelDownload(DownloadItem item)
    {
        Logger.Info($"[USER ACTION] Canceling download for '{item.FileName}'");
        item.Status = DownloadStatus.Canceled;
        item.SpeedBytesPerSec = 0;
        item.Cts?.Cancel();
        UpdateUi(item);
    }

    private static void UpdateUi(DownloadItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            item.NotifyProgressChanged();
        });
    }
}
