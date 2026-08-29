using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DownloaderApp.Models;
using DownloaderApp.Services;

namespace DownloaderApp.ViewModels;

public partial class AddDownloadDialogViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileService _fileService;
    private readonly IDownloadService _downloadService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _probeCts;

    public event Action<List<(string Url, string FileName, string SaveDir, int Threads, bool AutoExtract, DateTime? ScheduledTime)>, bool>? OnConfirmed;
    public event Action? OnCanceled;

    [ObservableProperty]
    private string _urlInput = string.Empty;

    [ObservableProperty]
    private string _saveDirectory = string.Empty;

    [ObservableProperty]
    private string _customFileName = string.Empty;

    [ObservableProperty]
    private bool _startImmediately = true;

    [ObservableProperty]
    private int _selectedThreadCount = 8;

    [ObservableProperty]
    private bool _autoExtractZip = false;

    [ObservableProperty]
    private bool _isScheduled = false;

    [ObservableProperty]
    private string _scheduledTimeString = DateTime.Now.AddHours(1).ToString("HH:mm");

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private int _detectedUrlsCount = 0;

    [ObservableProperty]
    private bool _isFetchingMetadata;

    [ObservableProperty]
    private FileMetadata? _currentMetadata;

    [ObservableProperty]
    private bool _hasDetectedInfo;

    public int[] AvailableThreadCounts { get; } = new[] { 1, 2, 4, 8, 16 };

    public AddDownloadDialogViewModel(
        IClipboardService clipboardService,
        IFileService fileService,
        IDownloadService downloadService,
        ISettingsService? settingsService = null)
    {
        _clipboardService = clipboardService;
        _fileService = fileService;
        _downloadService = downloadService;
        _settingsService = settingsService ?? new SettingsService();

        var settings = _settingsService.LoadSettings();
        SaveDirectory = settings.DefaultDownloadDirectory;
        SelectedThreadCount = settings.DefaultThreadsPerDownload;
        AutoExtractZip = settings.IsAutoExtractZipEnabled;
    }

    partial void OnUrlInputChanged(string value)
    {
        HasError = false;
        StatusMessage = string.Empty;
        HasDetectedInfo = false;
        CurrentMetadata = null;

        var urls = ExtractValidUrls(value);
        DetectedUrlsCount = urls.Count;

        if (urls.Count == 1)
        {
            FetchMetadataForUrl(urls[0]);
        }
        else if (urls.Count > 1)
        {
            CurrentMetadata = new FileMetadata
            {
                FileName = $"Batch: {urls.Count} files",
                Domain = "Multiple sources"
            };
            HasDetectedInfo = true;
            CustomFileName = string.Empty;
        }
        else
        {
            CustomFileName = string.Empty;
        }
    }

    public void FetchMetadataForUrl(string url)
    {
        _probeCts?.Cancel();
        _probeCts = new CancellationTokenSource();
        var ct = _probeCts.Token;

        IsFetchingMetadata = true;

        Task.Run(async () =>
        {
            try
            {
                var meta = await _downloadService.ProbeMetadataAsync(url, ct);

                if (!ct.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        IsFetchingMetadata = false;
                        CustomFileName = meta.FileName;
                        CurrentMetadata = meta;
                        HasDetectedInfo = true;

                        ApplySmartFolderRouting(meta.FileName);
                    });
                }
            }
            catch
            {
                if (!ct.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        IsFetchingMetadata = false;
                        var name = _downloadService.ExtractFileNameFromUrl(url);
                        CustomFileName = name;
                        ApplySmartFolderRouting(name);
                    });
                }
            }
        }, ct);
    }

    private void ApplySmartFolderRouting(string fileName)
    {
        var settings = _settingsService.LoadSettings();
        if (!settings.IsSmartFolderRoutingEnabled) return;

        var category = DownloadItem.DetermineCategory(fileName);
        var subDir = category switch
        {
            DownloadCategory.Compressed => "Archives",
            DownloadCategory.Programs => "Programs",
            DownloadCategory.Media => "Media",
            DownloadCategory.Documents => "Documents",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(subDir))
        {
            var baseDir = settings.DefaultDownloadDirectory;
            SaveDirectory = Path.Combine(baseDir, subDir);
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.StorageProvider is { } storage)
            {
                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Destination Download Folder",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var selectedPath = folders[0].TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        SaveDirectory = selectedPath;
                        Logger.Info($"[USER ACTION] Selected download directory: {selectedPath}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Folder picker failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        var clipboardUrls = await _clipboardService.ExtractUrlsFromClipboardAsync();
        if (clipboardUrls.Count > 0)
        {
            UrlInput = string.Join(Environment.NewLine, clipboardUrls);
        }
        else
        {
            var raw = await _clipboardService.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                UrlInput = raw.Trim();
            }
            else
            {
                StatusMessage = "No URL found in clipboard";
                HasError = true;
            }
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        var urls = ExtractValidUrls(UrlInput);
        if (urls.Count == 0)
        {
            HasError = true;
            StatusMessage = "Please enter at least one valid HTTP/HTTPS download link.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SaveDirectory))
        {
            SaveDirectory = _fileService.GetDefaultDownloadDirectory();
        }

        try
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Invalid download folder: {ex.Message}";
            return;
        }

        DateTime? scheduledTime = null;
        if (IsScheduled && TimeSpan.TryParse(ScheduledTimeString, out var time))
        {
            var now = DateTime.Now;
            var target = now.Date + time;
            if (target < now) target = target.AddDays(1);
            scheduledTime = target;
        }

        var results = new List<(string Url, string FileName, string SaveDir, int Threads, bool AutoExtract, DateTime? ScheduledTime)>();

        for (int i = 0; i < urls.Count; i++)
        {
            var url = urls[i];
            string fileName;
            if (urls.Count == 1 && !string.IsNullOrWhiteSpace(CustomFileName))
            {
                fileName = CustomFileName.Trim();
            }
            else
            {
                fileName = _downloadService.ExtractFileNameFromUrl(url);
            }

            results.Add((url, fileName, SaveDirectory, SelectedThreadCount, AutoExtractZip, scheduledTime));
        }

        OnConfirmed?.Invoke(results, StartImmediately && !IsScheduled);
    }

    [RelayCommand]
    private void Cancel()
    {
        _probeCts?.Cancel();
        OnCanceled?.Invoke();
    }

    public static List<string> ExtractValidUrls(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();

        return input.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .Distinct()
            .ToList();
    }
}
