using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DownloaderApp.Models;
using DownloaderApp.Services;

namespace DownloaderApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IDownloadService _downloadService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileService _fileService;

    public ObservableCollection<DownloadItem> AllDownloads { get; } = new();
    public ObservableCollection<DownloadItem> DisplayedDownloads { get; } = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private DownloadItem? _selectedItem;

    [ObservableProperty]
    private bool _isAddModalOpen;

    [ObservableProperty]
    private AddDownloadDialogViewModel? _addDialogVm;

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private string _themeModeTitle = "LCD Dark";

    [ObservableProperty]
    private int _totalActiveCount = 0;

    [ObservableProperty]
    private int _totalCompletedCount = 0;

    [ObservableProperty]
    private string _totalDownloadSpeedFormatted = "0 B/s";

    [ObservableProperty]
    private string _quickNotification = string.Empty;

    [ObservableProperty]
    private bool _hasNotification;

    private readonly DispatcherTimer _statsTimer;

    public MainViewModel(
        IDownloadService downloadService,
        IClipboardService clipboardService,
        IFileService fileService)
    {
        _downloadService = downloadService;
        _clipboardService = clipboardService;
        _fileService = fileService;

        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _statsTimer.Tick += (s, e) => RefreshStats();
        _statsTimer.Start();

        InitializeDefaultDownloads();
        ApplyFilter();
    }

    public MainViewModel() : this(new DownloadService(), new ClipboardService(), new FileService())
    {
    }

    private void InitializeDefaultDownloads()
    {
        // Start with clean empty download queue
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    public void ApplyFilter()
    {
        var query = SearchQuery?.Trim().ToLowerInvariant();

        var filtered = AllDownloads.AsEnumerable();

        // Status Filter
        if (!string.IsNullOrEmpty(SelectedFilter) && SelectedFilter != "All")
        {
            filtered = SelectedFilter switch
            {
                "Downloading" => filtered.Where(x => x.Status == DownloadStatus.Downloading || x.Status == DownloadStatus.Connecting),
                "Completed" => filtered.Where(x => x.Status == DownloadStatus.Completed),
                "Paused" => filtered.Where(x => x.Status == DownloadStatus.Paused),
                "Failed" => filtered.Where(x => x.Status == DownloadStatus.Failed),
                _ => filtered
            };
        }

        // Category Filter
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
        {
            filtered = SelectedCategory switch
            {
                "Compressed" => filtered.Where(x => x.Category == DownloadCategory.Compressed),
                "Programs" => filtered.Where(x => x.Category == DownloadCategory.Programs),
                "Media" => filtered.Where(x => x.Category == DownloadCategory.Media),
                "Documents" => filtered.Where(x => x.Category == DownloadCategory.Documents),
                "Other" => filtered.Where(x => x.Category == DownloadCategory.Other),
                _ => filtered
            };
        }

        // Search text
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(x =>
                x.FileName.ToLowerInvariant().Contains(query) ||
                x.Url.ToLowerInvariant().Contains(query));
        }

        var resultList = filtered.OrderByDescending(x => x.CreatedAt).ToList();

        DisplayedDownloads.Clear();
        foreach (var item in resultList)
        {
            DisplayedDownloads.Add(item);
        }
    }

    private void RefreshStats()
    {
        int active = 0;
        int completed = 0;
        double totalSpeed = 0;

        foreach (var item in AllDownloads)
        {
            if (item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.Connecting)
            {
                active++;
                totalSpeed += item.SpeedBytesPerSec;
            }
            else if (item.Status == DownloadStatus.Completed)
            {
                completed++;
            }
        }

        TotalActiveCount = active;
        TotalCompletedCount = completed;
        TotalDownloadSpeedFormatted = totalSpeed > 0 ? $"{DownloadItem.FormatBytes((long)totalSpeed)}/s" : "0 B/s";
    }

    [RelayCommand]
    private void OpenAddModal()
    {
        AddDialogVm = new AddDownloadDialogViewModel(_clipboardService, _fileService, _downloadService);
        AddDialogVm.OnConfirmed += OnAddDownloadConfirmed;
        AddDialogVm.OnCanceled += () => IsAddModalOpen = false;
        IsAddModalOpen = true;
    }

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        var urls = await _clipboardService.ExtractUrlsFromClipboardAsync();
        if (urls.Count > 0)
        {
            AddDialogVm = new AddDownloadDialogViewModel(_clipboardService, _fileService, _downloadService)
            {
                UrlInput = string.Join(Environment.NewLine, urls)
            };
            AddDialogVm.OnConfirmed += OnAddDownloadConfirmed;
            AddDialogVm.OnCanceled += () => IsAddModalOpen = false;
            IsAddModalOpen = true;
            ShowNotification($"Detected {urls.Count} download URL{(urls.Count > 1 ? "s" : "")} from clipboard");
        }
        else
        {
            OpenAddModal();
            ShowNotification("No direct URL found on clipboard; enter download link.");
        }
    }

    [RelayCommand]
    private async Task QuickPasteAndDownloadAsync()
    {
        var urls = await _clipboardService.ExtractUrlsFromClipboardAsync();
        if (urls.Count > 0)
        {
            var defaultDir = _fileService.GetDefaultDownloadDirectory();
            foreach (var url in urls)
            {
                var fileName = _downloadService.ExtractFileNameFromUrl(url);
                var item = new DownloadItem
                {
                    Url = url,
                    FileName = fileName,
                    SaveDirectory = defaultDir,
                    Status = DownloadStatus.Queued,
                    CreatedAt = DateTime.Now
                };
                AllDownloads.Insert(0, item);
                _ = _downloadService.StartDownloadAsync(item);
            }
            ApplyFilter();
            ShowNotification($"Started downloading {urls.Count} file{(urls.Count > 1 ? "s" : "")}");
        }
        else
        {
            OpenAddModal();
            ShowNotification("Clipboard does not contain a valid URL. Paste manually.");
        }
    }

    private void OnAddDownloadConfirmed(System.Collections.Generic.List<(string Url, string FileName, string SaveDir)> items, bool startNow)
    {
        IsAddModalOpen = false;

        foreach (var (url, fileName, saveDir) in items)
        {
            var item = new DownloadItem
            {
                Url = url,
                FileName = fileName,
                SaveDirectory = saveDir,
                Status = startNow ? DownloadStatus.Connecting : DownloadStatus.Queued,
                CreatedAt = DateTime.Now
            };

            AllDownloads.Insert(0, item);

            if (startNow)
            {
                _ = _downloadService.StartDownloadAsync(item);
            }
        }

        ApplyFilter();
        ShowNotification($"Added {items.Count} item{(items.Count > 1 ? "s" : "")} to download queue");
    }

    [RelayCommand]
    private void StartDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.ResumeDownload(item);
        ApplyFilter();
    }

    [RelayCommand]
    private void PauseDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.PauseDownload(item);
        ApplyFilter();
    }

    [RelayCommand]
    private void CancelDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.CancelDownload(item);
        ApplyFilter();
    }

    [RelayCommand]
    private void DeleteDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.CancelDownload(item);
        AllDownloads.Remove(item);
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenFile(DownloadItem? item)
    {
        if (item == null) return;
        _fileService.OpenFile(item.FullPath);
    }

    [RelayCommand]
    private void OpenFolder(DownloadItem? item)
    {
        if (item == null) return;
        _fileService.OpenFolder(item.FullPath);
    }

    [RelayCommand]
    private async Task CopyUrlAsync(DownloadItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.Url)) return;
        await _clipboardService.SetTextAsync(item.Url);
        ShowNotification("Download link copied to clipboard");
    }

    [RelayCommand]
    private void PauseAll()
    {
        foreach (var item in AllDownloads)
        {
            if (item.IsActive)
            {
                _downloadService.PauseDownload(item);
            }
        }
        ApplyFilter();
        ShowNotification("All active downloads paused");
    }

    [RelayCommand]
    private void ResumeAll()
    {
        foreach (var item in AllDownloads)
        {
            if (item.CanResume)
            {
                _downloadService.ResumeDownload(item);
            }
        }
        ApplyFilter();
        ShowNotification("All paused downloads resumed");
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        var completedList = AllDownloads.Where(x => x.Status == DownloadStatus.Completed).ToList();
        foreach (var item in completedList)
        {
            AllDownloads.Remove(item);
        }
        ApplyFilter();
        ShowNotification($"Cleared {completedList.Count} completed download{(completedList.Count > 1 ? "s" : "")}");
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        ThemeModeTitle = IsDarkMode ? "LCD Dark" : "Light";

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        ShowNotification($"Switched to {ThemeModeTitle} Mode");
    }

    [RelayCommand]
    private void SelectFilter(string filter)
    {
        SelectedFilter = filter;
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
    }

    private void ShowNotification(string message)
    {
        QuickNotification = message;
        HasNotification = true;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, e) =>
        {
            HasNotification = false;
            timer.Stop();
        };
        timer.Start();
    }
}
