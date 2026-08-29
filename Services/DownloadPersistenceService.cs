using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DownloaderApp.Models;

namespace DownloaderApp.Services;

public interface IDownloadPersistenceService
{
    List<DownloadItem> LoadDownloads();
    void SaveDownloads(IEnumerable<DownloadItem> items);
}

public class DownloadPersistenceService : IDownloadPersistenceService
{
    private static readonly string StorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Downloader.io");

    private static readonly string StorageFilePath = Path.Combine(StorageDirectory, "downloads.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _fileLock = new();

    public List<DownloadItem> LoadDownloads()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(StorageFilePath))
                {
                    return new List<DownloadItem>();
                }

                var json = File.ReadAllText(StorageFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<DownloadItem>();
                }

                var items = JsonSerializer.Deserialize<List<DownloadItem>>(json, JsonOptions);
                if (items == null) return new List<DownloadItem>();

                foreach (var item in items)
                {
                    // If app was terminated while downloading or connecting, restore as Paused
                    if (item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.Connecting)
                    {
                        item.Status = DownloadStatus.Paused;
                    }
                    item.SpeedBytesPerSec = 0;

                    // Ensure segment statuses are clean
                    foreach (var seg in item.Segments)
                    {
                        seg.IsActive = false;
                        seg.SpeedBytesPerSec = 0;
                    }
                }

                Logger.Info($"[PERSISTENCE] Restored {items.Count} download item(s) from {StorageFilePath}");
                return items;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to load downloads from {StorageFilePath}: {ex.Message}");
                return new List<DownloadItem>();
            }
        }
    }

    public void SaveDownloads(IEnumerable<DownloadItem> items)
    {
        lock (_fileLock)
        {
            try
            {
                if (!Directory.Exists(StorageDirectory))
                {
                    Directory.CreateDirectory(StorageDirectory);
                }

                var list = items.ToList();
                var json = JsonSerializer.Serialize(list, JsonOptions);

                var tempPath = StorageFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, StorageFilePath, true);

                Logger.Debug($"[PERSISTENCE] Saved {list.Count} download(s) to {StorageFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to save downloads to {StorageFilePath}: {ex.Message}");
            }
        }
    }
}

