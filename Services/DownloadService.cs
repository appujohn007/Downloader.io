using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using DownloaderApp.Models;
using Microsoft.Win32.SafeHandles;

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
    Task<string> ComputeHashAsync(string filePath, string algorithm, CancellationToken ct = default);
    Task<bool> ExtractArchiveAsync(string archivePath, string destinationDirectory, CancellationToken ct = default);
}

public class DownloadService : IDownloadService
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioNotificationService _audioNotificationService;

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
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 Downloader.io/2.0");
    }

    public DownloadService(ISettingsService? settingsService = null, IAudioNotificationService? audioNotificationService = null)
    {
        _settingsService = settingsService ?? new SettingsService();
        _audioNotificationService = audioNotificationService ?? new AudioNotificationService(_settingsService);
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

            // 2. Fallback to GET with Range: bytes=0-0
            if (response == null || !response.IsSuccessStatusCode)
            {
                response?.Dispose();
                using var getReq = new HttpRequestMessage(HttpMethod.Get, url);
                getReq.Headers.Range = new RangeHeaderValue(0, 0);
                response = await HttpClient.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, ct);
            }

            using (response)
            {
                if (response.Content.Headers.ContentType?.MediaType != null)
                {
                    meta.ContentType = response.Content.Headers.ContentType.MediaType;
                }

                meta.IsResumable = response.StatusCode == System.Net.HttpStatusCode.PartialContent ||
                                  response.Headers.AcceptRanges.Contains("bytes");

                if (response.Content.Headers.ContentRange?.Length.HasValue == true)
                {
                    meta.FileSize = response.Content.Headers.ContentRange.Length.Value;
                }
                else if (response.Content.Headers.ContentLength.HasValue)
                {
                    meta.FileSize = response.Content.Headers.ContentLength.Value;
                }

                string? detectedName = null;

                if (response.Content.Headers.ContentDisposition != null)
                {
                    detectedName = response.Content.Headers.ContentDisposition.FileNameStar ??
                                   response.Content.Headers.ContentDisposition.FileName;
                }

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

                if (string.IsNullOrWhiteSpace(detectedName))
                {
                    var effectiveUri = response.RequestMessage?.RequestUri ?? uri;
                    detectedName = ExtractFileNameFromUrl(effectiveUri.ToString());
                }

                if (!string.IsNullOrWhiteSpace(detectedName))
                {
                    detectedName = CleanFileName(detectedName);

                    if (!Path.HasExtension(detectedName) && !string.IsNullOrEmpty(meta.ContentType))
                    {
                        if (MimeExtensions.TryGetValue(meta.ContentType, out var ext))
                        {
                            detectedName += ext;
                        }
                    }

                    meta.FileName = detectedName;
                }

                meta.ErrorMessage = string.Empty;
                return meta;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to probe metadata for '{url}': {ex.Message}");
            meta.ErrorMessage = ex.Message;
            meta.FileName = ExtractFileNameFromUrl(url);
            return meta;
        }
    }

    public string ExtractFileNameFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "download.bin";

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
                Directory.CreateDirectory(item.SaveDirectory);
            }

            var targetFilePath = item.FullPath;
            var partialFilePath = item.PartialPath;

            // Probe metadata first to verify size and range support
            var meta = await ProbeMetadataAsync(item.Url, ct);
            if (meta.FileSize > 0)
            {
                item.TotalBytes = meta.FileSize;
            }
            if (!string.IsNullOrWhiteSpace(meta.ContentType))
            {
                item.ServerHeadersSummary = $"Type: {meta.ContentType} | Host: {meta.Domain} | Resumable: {(meta.IsResumable ? "Yes" : "No")}";
            }

            int configuredThreads = item.MaxSegments > 0 ? item.MaxSegments : _settingsService.CurrentSettings.DefaultThreadsPerDownload;
            int threads = Math.Clamp(configuredThreads, 1, 16);

            if (meta.IsResumable && item.TotalBytes > 1024 * 1024 && threads > 1)
            {
                // Multi-threaded Segmented Mode with Full Resumption Support
                Logger.Download($"[ACCELERATION] Launching {threads}-threaded accelerated download for '{item.FileName}' ({DownloadItem.FormatBytes(item.TotalBytes)})");
                await ProcessSegmentedDownloadAsync(item, partialFilePath, targetFilePath, threads, ct);
            }
            else
            {
                // Single-Stream Mode
                Logger.Download($"[SINGLE-STREAM] Starting stream download for '{item.FileName}'");
                await ProcessSingleStreamDownloadAsync(item, partialFilePath, targetFilePath, ct);
            }

            // Verify final target file exists on disk
            if (!File.Exists(targetFilePath))
            {
                throw new FileNotFoundException($"Downloaded file '{targetFilePath}' was not found after stream completion.");
            }

            var finalSize = new FileInfo(targetFilePath).Length;
            item.DownloadedBytes = finalSize;
            if (item.TotalBytes <= 0) item.TotalBytes = finalSize;

            item.Status = DownloadStatus.Completed;
            item.CompletedAt = DateTime.Now;
            item.SpeedBytesPerSec = 0;
            item.ProgressPercentage = 100;
            UpdateUi(item);

            _audioNotificationService.PlayDownloadCompleted();

            // Auto-extract ZIP if requested
            var settings = _settingsService.CurrentSettings;
            if ((item.AutoExtractZip || settings.IsAutoExtractZipEnabled) && Path.GetExtension(targetFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var extractFolder = Path.Combine(item.SaveDirectory, Path.GetFileNameWithoutExtension(item.FileName));
                _ = ExtractArchiveAsync(targetFilePath, extractFolder, CancellationToken.None);
            }

            Logger.Success($"[COMPLETED] Successfully downloaded '{item.FileName}' ({DownloadItem.FormatBytes(finalSize)}) to '{targetFilePath}'");
        }
        catch (OperationCanceledException)
        {
            if (item.Status != DownloadStatus.Paused && item.Status != DownloadStatus.Canceled)
            {
                item.Status = DownloadStatus.Paused;
            }
            item.SpeedBytesPerSec = 0;
            SaveSegmentsMeta(item, item.Segments.ToList());
            UpdateUi(item);
            Logger.Warn($"[PAUSED/CANCELLED] Download '{item.FileName}' interrupted by user.");
        }
        catch (Exception ex)
        {
            item.Status = DownloadStatus.Failed;
            item.ErrorMessage = ex.Message;
            item.SpeedBytesPerSec = 0;
            SaveSegmentsMeta(item, item.Segments.ToList());
            UpdateUi(item);
            _audioNotificationService.PlayDownloadFailed();
            Logger.Error($"[FAILED] Download '{item.FileName}' encountered an error: {ex.Message}", ex);
        }
    }

    private async Task ProcessSegmentedDownloadAsync(
        DownloadItem item,
        string partialFilePath,
        string finalFilePath,
        int threadCount,
        CancellationToken ct)
    {
        long totalLength = item.TotalBytes;

        // 1. Synchronously pre-allocate partial file if new or size mismatch
        if (!File.Exists(partialFilePath) || new FileInfo(partialFilePath).Length != totalLength)
        {
            using (var initStream = new FileStream(partialFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                initStream.SetLength(totalLength);
            }
        }

        // 2. Load or initialize thread segments with full state resumption
        List<DownloadSegment> segmentList;

        if (item.Segments.Count == threadCount && item.Segments.Sum(s => s.TotalBytes) == totalLength)
        {
            // Resume from in-memory collection
            segmentList = item.Segments.ToList();
            Logger.Download($"[RESUME] Continuing {threadCount}-threaded download from in-memory state ({DownloadItem.FormatBytes(segmentList.Sum(s => s.DownloadedBytes))}/{DownloadItem.FormatBytes(totalLength)})");
        }
        else if (File.Exists(item.SegmentsMetaPath))
        {
            try
            {
                var json = File.ReadAllText(item.SegmentsMetaPath);
                var saved = JsonSerializer.Deserialize<List<DownloadSegment>>(json);
                if (saved != null && saved.Count == threadCount && saved.Sum(s => s.TotalBytes) == totalLength)
                {
                    segmentList = saved;
                    Logger.Download($"[RESUME] Continuing {threadCount}-threaded download from saved metadata ({DownloadItem.FormatBytes(segmentList.Sum(s => s.DownloadedBytes))}/{DownloadItem.FormatBytes(totalLength)})");
                }
                else
                {
                    segmentList = CreateNewSegments(totalLength, threadCount);
                }
            }
            catch
            {
                segmentList = CreateNewSegments(totalLength, threadCount);
            }
        }
        else
        {
            segmentList = CreateNewSegments(totalLength, threadCount);
        }

        // Update UI collection
        Dispatcher.UIThread.Post(() =>
        {
            item.Segments.Clear();
            foreach (var seg in segmentList)
            {
                item.Segments.Add(seg);
            }
        });

        long initialDownloaded = segmentList.Sum(s => s.DownloadedBytes);
        long aggregateBytesDownloaded = initialDownloaded;
        item.DownloadedBytes = initialDownloaded;
        item.ProgressPercentage = totalLength > 0 ? Math.Clamp(((double)initialDownloaded / totalLength) * 100.0, 0, 100) : 0;
        item.Status = DownloadStatus.Downloading;
        UpdateUi(item);

        long bytesSinceLastSample = 0;
        double smoothedSpeed = 0.0;
        var speedTimer = Stopwatch.StartNew();
        var uiProgressTimer = Stopwatch.StartNew();
        var uiSpeedDisplayTimer = Stopwatch.StartNew();
        var metaSaveTimer = Stopwatch.StartNew();

        var tasks = new List<Task>();

        // 3. Open file stream for non-blocking concurrent writes
        using (var fileStream = new FileStream(partialFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 81920, useAsync: true))
        {
            var fileHandle = fileStream.SafeFileHandle;

            foreach (var seg in segmentList)
            {
                // If segment already finished, skip launching worker
                if (seg.DownloadedBytes >= seg.TotalBytes && seg.TotalBytes > 0)
                {
                    seg.IsCompleted = true;
                    seg.IsActive = false;
                    continue;
                }

                tasks.Add(Task.Run(async () =>
                {
                    int retries = 0;
                    bool success = false;

                    while (!success && retries < 4)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            long currentOffset = seg.StartByte + seg.DownloadedBytes;
                            if (currentOffset > seg.EndByte)
                            {
                                seg.IsCompleted = true;
                                seg.IsActive = false;
                                break;
                            }

                            seg.IsActive = true;
                            seg.IsCompleted = false;

                            using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
                            request.Headers.Range = new RangeHeaderValue(currentOffset, seg.EndByte);

                            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                            // Server must return 206 Partial Content
                            if (response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                            {
                                throw new HttpRequestException($"Server returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) instead of 206 Partial Content for byte range {currentOffset}-{seg.EndByte}");
                            }

                            using var stream = await response.Content.ReadAsStreamAsync(ct);
                            var buffer = new byte[65536];
                            int read;
                            var segSpeedTimer = Stopwatch.StartNew();
                            long segBytesSinceSample = 0;

                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                            {
                                await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, read), currentOffset, ct);
                                currentOffset += read;
                                seg.DownloadedBytes += read;
                                segBytesSinceSample += read;

                                Interlocked.Add(ref aggregateBytesDownloaded, read);
                                Interlocked.Add(ref bytesSinceLastSample, read);

                                // Speed limiter throttling
                                long limit = item.SpeedCapBytesPerSec > 0 ? item.SpeedCapBytesPerSec : _settingsService.CurrentSettings.GlobalSpeedLimitBytesPerSec;
                                if (limit > 0)
                                {
                                    long allowedChunk = limit / threadCount;
                                    if (allowedChunk > 0 && segBytesSinceSample >= allowedChunk)
                                    {
                                        await Task.Delay(15, ct);
                                    }
                                }

                                if (segSpeedTimer.ElapsedMilliseconds >= 300)
                                {
                                    double sec = segSpeedTimer.Elapsed.TotalSeconds;
                                    if (sec > 0)
                                    {
                                        seg.SpeedBytesPerSec = segBytesSinceSample / sec;
                                    }
                                    segBytesSinceSample = 0;
                                    segSpeedTimer.Restart();
                                }
                            }

                            seg.IsCompleted = true;
                            seg.IsActive = false;
                            seg.SpeedBytesPerSec = 0;
                            success = true;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            retries++;
                            item.RetryAttempts = retries;
                            Logger.Warn($"[RETRY] Segment {seg.SegmentId} failed: {ex.Message}. Attempt {retries}/4...");
                            await Task.Delay(1000 * retries, ct);
                        }
                    }

                    if (!success)
                    {
                        throw new IOException($"Segment {seg.SegmentId} failed after {retries} retries.");
                    }
                }, ct));
            }

            // Monitor task for fluid UI updates & periodic metadata persistence
            var monitorTask = Task.Run(async () =>
            {
                while (!Task.WhenAll(tasks).IsCompleted)
                {
                    if (ct.IsCancellationRequested) break;

                    if (speedTimer.ElapsedMilliseconds >= 250)
                    {
                        double sec = speedTimer.Elapsed.TotalSeconds;
                        long bytesSampled = Interlocked.Exchange(ref bytesSinceLastSample, 0);
                        if (sec > 0)
                        {
                            double instantSpeed = bytesSampled / sec;
                            smoothedSpeed = smoothedSpeed <= 0 ? instantSpeed : (0.25 * instantSpeed) + (0.75 * smoothedSpeed);
                        }
                        speedTimer.Restart();
                    }

                    if (uiProgressTimer.ElapsedMilliseconds >= 40)
                    {
                        long totalDownloaded = aggregateBytesDownloaded;
                        double progressPct = totalLength > 0 ? Math.Clamp(((double)totalDownloaded / totalLength) * 100.0, 0, 100) : 0;
                        bool updateSpeed = uiSpeedDisplayTimer.ElapsedMilliseconds >= 600;
                        if (updateSpeed) uiSpeedDisplayTimer.Restart();

                        Dispatcher.UIThread.Post(() =>
                        {
                            item.UpdateProgressMetrics(totalDownloaded, totalLength, progressPct, smoothedSpeed, updateSpeed);
                        }, DispatcherPriority.Normal);

                        uiProgressTimer.Restart();
                    }

                    // Periodic metadata persistence every 2.5 seconds
                    if (metaSaveTimer.ElapsedMilliseconds >= 2500)
                    {
                        SaveSegmentsMeta(item, segmentList);
                        metaSaveTimer.Restart();
                    }

                    await Task.Delay(35, ct);
                }
            }, ct);

            try
            {
                await Task.WhenAll(tasks);
                await monitorTask;
                await fileStream.FlushAsync(ct);
            }
            finally
            {
                SaveSegmentsMeta(item, segmentList);
            }
        } // fileStream is now fully flushed and disposed

        // 4. Validate integrity of all segments
        long sumDownloaded = segmentList.Sum(s => s.DownloadedBytes);
        if (sumDownloaded < totalLength)
        {
            throw new IOException($"Segmented download incomplete: expected {totalLength} bytes, received {sumDownloaded} bytes.");
        }

        // 5. Delete metadata sidecar on successful completion
        if (File.Exists(item.SegmentsMetaPath))
        {
            try { File.Delete(item.SegmentsMetaPath); } catch {}
        }

        // 6. Promote partial file to final target
        if (File.Exists(finalFilePath))
        {
            File.Delete(finalFilePath);
        }
        File.Move(partialFilePath, finalFilePath);
    }

    private static List<DownloadSegment> CreateNewSegments(long totalLength, int threadCount)
    {
        long segmentSize = totalLength / threadCount;
        var list = new List<DownloadSegment>();
        for (int i = 0; i < threadCount; i++)
        {
            long start = i * segmentSize;
            long end = (i == threadCount - 1) ? (totalLength - 1) : ((i + 1) * segmentSize - 1);
            list.Add(new DownloadSegment
            {
                SegmentId = i + 1,
                StartByte = start,
                EndByte = end,
                DownloadedBytes = 0,
                IsActive = true,
                IsCompleted = false
            });
        }
        return list;
    }

    private static void SaveSegmentsMeta(DownloadItem item, List<DownloadSegment> segments)
    {
        // Segments are now automatically persisted cleanly in %AppData%/Downloader.io/downloads.json
        // Clean up any legacy .meta files in download directory
        try
        {
            if (File.Exists(item.SegmentsMetaPath))
            {
                File.Delete(item.SegmentsMetaPath);
            }
        }
        catch {}
    }

    private async Task ProcessSingleStreamDownloadAsync(
        DownloadItem item,
        string partialFilePath,
        string finalFilePath,
        CancellationToken ct)
    {
        long existingBytes = 0;
        if (File.Exists(partialFilePath))
        {
            existingBytes = new FileInfo(partialFilePath).Length;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        bool isRangeAccepted = response.StatusCode == System.Net.HttpStatusCode.PartialContent;

        if (!response.IsSuccessStatusCode && !isRangeAccepted)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                existingBytes = 0;
                if (File.Exists(partialFilePath)) File.Delete(partialFilePath);
                using var freshRequest = new HttpRequestMessage(HttpMethod.Get, item.Url);
                using var freshResponse = await HttpClient.SendAsync(freshRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                freshResponse.EnsureSuccessStatusCode();
                await ReadSingleStreamAsync(item, freshResponse, partialFilePath, finalFilePath, 0, ct);
                return;
            }
            throw new HttpRequestException($"Server returned HTTP {(int)response.StatusCode}");
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
            existingBytes = 0;
        }

        await ReadSingleStreamAsync(item, response, partialFilePath, finalFilePath, existingBytes, ct, totalLength);
    }

    private async Task ReadSingleStreamAsync(
        DownloadItem item,
        HttpResponseMessage response,
        string partialFilePath,
        string finalFilePath,
        long initialBytes,
        CancellationToken ct,
        long totalLength = -1)
    {
        long currentDownloadedBytes = initialBytes;
        long currentTotalBytes = totalLength > 0 ? totalLength : item.TotalBytes;

        item.Status = DownloadStatus.Downloading;
        item.DownloadedBytes = initialBytes;
        if (totalLength > 0) item.TotalBytes = totalLength;

        UpdateUi(item);

        using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
        {
            using (var fileStream = new FileStream(
                partialFilePath,
                initialBytes > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                int bytesRead;
                long bytesSinceLastSample = 0;
                double smoothedSpeed = 0.0;

                var speedSampleTimer = Stopwatch.StartNew();
                var uiProgressTimer = Stopwatch.StartNew();
                var uiSpeedDisplayTimer = Stopwatch.StartNew();

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);

                    currentDownloadedBytes += bytesRead;
                    bytesSinceLastSample += bytesRead;

                    if (speedSampleTimer.ElapsedMilliseconds >= 250)
                    {
                        var elapsedSec = speedSampleTimer.Elapsed.TotalSeconds;
                        if (elapsedSec > 0)
                        {
                            var instantSpeed = bytesSinceLastSample / elapsedSec;
                            smoothedSpeed = smoothedSpeed <= 0 ? instantSpeed : (0.25 * instantSpeed) + (0.75 * smoothedSpeed);
                        }
                        bytesSinceLastSample = 0;
                        speedSampleTimer.Restart();
                    }

                    if (uiProgressTimer.ElapsedMilliseconds >= 35)
                    {
                        double progressPct = currentTotalBytes > 0 ? Math.Clamp(((double)currentDownloadedBytes / currentTotalBytes) * 100.0, 0, 100) : 0;
                        bool updateSpeedDisplay = uiSpeedDisplayTimer.ElapsedMilliseconds >= 700;
                        if (updateSpeedDisplay) uiSpeedDisplayTimer.Restart();

                        long snapDownloaded = currentDownloadedBytes;
                        long snapTotal = currentTotalBytes;
                        double snapSpeed = smoothedSpeed;

                        Dispatcher.UIThread.Post(() =>
                        {
                            item.UpdateProgressMetrics(snapDownloaded, snapTotal, progressPct, snapSpeed, updateSpeedDisplay);
                        }, DispatcherPriority.Normal);

                        uiProgressTimer.Restart();
                    }
                }

                await fileStream.FlushAsync(ct);
            } // fileStream is now fully flushed and disposed
        }

        if (File.Exists(finalFilePath))
        {
            File.Delete(finalFilePath);
        }
        File.Move(partialFilePath, finalFilePath);
    }

    public async Task<string> ComputeHashAsync(string filePath, string algorithm, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return string.Empty;

        return await Task.Run(() =>
        {
            using var stream = File.OpenRead(filePath);
            using HashAlgorithm hasher = algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.Create(),
                "SHA256" => SHA256.Create(),
                "SHA1" => SHA1.Create(),
                _ => SHA256.Create()
            };

            var hashBytes = hasher.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }, ct);
    }

    public async Task<bool> ExtractArchiveAsync(string archivePath, string destinationDirectory, CancellationToken ct = default)
    {
        if (!File.Exists(archivePath)) return false;

        try
        {
            Logger.Info($"[AUTO-EXTRACT] Extracting '{archivePath}' to '{destinationDirectory}'");
            Directory.CreateDirectory(destinationDirectory);
            await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, destinationDirectory, overwriteFiles: true), ct);
            Logger.Success($"[AUTO-EXTRACT] Successfully extracted archive to '{destinationDirectory}'");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[AUTO-EXTRACT] Failed to extract '{archivePath}': {ex.Message}");
            return false;
        }
    }

    public void PauseDownload(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.Connecting)
        {
            Logger.Info($"[USER ACTION] Pausing download for '{item.FileName}'");
            item.Status = DownloadStatus.Paused;
            item.SpeedBytesPerSec = 0;
            item.Cts?.Cancel();
            SaveSegmentsMeta(item, item.Segments.ToList());
            UpdateUi(item);
        }
    }

    public void ResumeDownload(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Paused || item.Status == DownloadStatus.Failed || (item.Status == DownloadStatus.Queued && item.IsScheduled))
        {
            Logger.Info($"[USER ACTION] Resuming download for '{item.FileName}'");
            item.IsScheduled = false;
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
