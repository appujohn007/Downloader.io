using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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

    static DownloadService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 Downloader.io/1.0");
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
                    var invalid = Path.GetInvalidFileNameChars();
                    foreach (var c in invalid)
                    {
                        name = name.Replace(c, '_');
                    }
                    return name;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to extract filename from '{url}': {ex.Message}");
        }

        return "download.bin";
    }

    public async Task<long> ProbeFileSizeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            Logger.Debug($"Probing file size via HTTP HEAD: {url}");
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var res = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (res.IsSuccessStatusCode && res.Content.Headers.ContentLength.HasValue)
            {
                var length = res.Content.Headers.ContentLength.Value;
                Logger.Info($"Probe result for {url}: {DownloadItem.FormatBytes(length)}");
                return length;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Probe failed for {url}: {ex.Message}");
        }
        return -1;
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
        long bytesSinceLastSpeedCalc = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastUiUpdate = Stopwatch.StartNew();
        var lastConsoleLog = Stopwatch.StartNew();

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);

            item.DownloadedBytes += bytesRead;
            bytesSinceLastSpeedCalc += bytesRead;

            if (item.TotalBytes > 0)
            {
                item.ProgressPercentage = Math.Clamp(((double)item.DownloadedBytes / item.TotalBytes) * 100.0, 0, 100);
            }

            // Calculate speed every 400ms
            if (stopwatch.ElapsedMilliseconds >= 400)
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    item.SpeedBytesPerSec = bytesSinceLastSpeedCalc / elapsedSeconds;
                }
                bytesSinceLastSpeedCalc = 0;
                stopwatch.Restart();
            }

            // Dispatch UI update every 200ms
            if (lastUiUpdate.ElapsedMilliseconds >= 200)
            {
                UpdateUi(item);
                lastUiUpdate.Restart();
            }

            // Periodic terminal log every 3 seconds for clean terminal telemetry
            if (lastConsoleLog.ElapsedMilliseconds >= 3000)
            {
                Logger.Download($"[PROGRESS] '{item.FileName}': {item.ProgressPercentage:0.0}% | {DownloadItem.FormatBytes(item.DownloadedBytes)} / {DownloadItem.FormatBytes(item.TotalBytes)} | {DownloadItem.FormatBytes((long)item.SpeedBytesPerSec)}/s");
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
            item.TotalBytes = item.DownloadedBytes;
        }

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
