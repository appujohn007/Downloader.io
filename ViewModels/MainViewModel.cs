using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DownloaderApp.Models;
using DownloaderApp.Services;
using DownloaderApp.Views;

namespace DownloaderApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IDownloadService _downloadService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileService _fileService;
    private readonly ISettingsService _settingsService;
    private readonly IAudioNotificationService _audioService;
    private readonly IPowerService _powerService;
    private readonly IClipboardSnifferService _snifferService;
    private readonly AppSettings _settings;

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
    private double _currentAggregateSpeed = 0.0;

    [ObservableProperty]
    private string _totalDownloadSpeedFormatted = "0 B/s";

    [ObservableProperty]
    private string _quickNotification = string.Empty;

    [ObservableProperty]
    private bool _hasNotification;

    // Speed Limiter
    [ObservableProperty]
    private int _selectedSpeedCapIndex = 0; // 0: Unlimited, 1: 10MB/s, 2: 5MB/s, 3: 2MB/s, 4: 500KB/s

    // Inspector Drawer
    [ObservableProperty]
    private bool _isInspectorOpen;

    [ObservableProperty]
    private DownloadItem? _inspectedItem;

    [ObservableProperty]
    private int _inspectorTab = 0; // 0: Segments, 1: Checksum, 2: File Info

    [ObservableProperty]
    private string _checksumAlgorithm = "SHA256";

    [ObservableProperty]
    private string _calculatedChecksum = string.Empty;

    [ObservableProperty]
    private string _expectedChecksumInput = string.Empty;

    [ObservableProperty]
    private bool? _checksumMatchResult;

    [ObservableProperty]
    private bool _isCalculatingChecksum;

    public bool IsTab0 => InspectorTab == 0;
    public bool IsTab1 => InspectorTab == 1;
    public bool IsTab2 => InspectorTab == 2;

    public string ChecksumMatchStatusText => ChecksumMatchResult.HasValue
        ? (ChecksumMatchResult.Value ? "✓ Match Verified (Checksums Identical)" : "✗ Mismatch Detected (Hashes Do Not Match)")
        : string.Empty;

    // Settings Drawer
    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isSmartFolderRoutingEnabled;

    [ObservableProperty]
    private bool _isAutoExtractZipEnabled;

    [ObservableProperty]
    private bool _isClipboardSnifferEnabled;

    [ObservableProperty]
    private bool _isSoundEnabled;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private string _defaultDownloadDirectory = string.Empty;

    [ObservableProperty]
    private int _defaultThreadsPerDownload = 8;

    [ObservableProperty]
    private int _selectedPostDownloadActionIndex = 0;

    public PostDownloadAction SelectedPostDownloadAction
    {
        get => (PostDownloadAction)SelectedPostDownloadActionIndex;
        set => SelectedPostDownloadActionIndex = (int)value;
    }

    // Floating Mini Widget
    private MiniDropWindow? _miniWindow;
    [ObservableProperty]
    private bool _isMiniWidgetVisible;

    private readonly DispatcherTimer _statsTimer;
    private readonly DispatcherTimer _scheduleTimer;
    private readonly DispatcherTimer _notificationTimer;
    private readonly IDownloadPersistenceService _persistenceService;
    private DateTime _lastSavedTime = DateTime.MinValue;
    private bool _hasExecutedPowerAction = false;

    public MainViewModel(
        IDownloadService downloadService,
        IClipboardService clipboardService,
        IFileService fileService,
        ISettingsService settingsService,
        IAudioNotificationService? audioService = null,
        IPowerService? powerService = null,
        IClipboardSnifferService? snifferService = null,
        IDownloadPersistenceService? persistenceService = null)
    {
        _downloadService = downloadService;
        _clipboardService = clipboardService;
        _fileService = fileService;
        _settingsService = settingsService;
        _audioService = audioService ?? new AudioNotificationService(_settingsService);
        _powerService = powerService ?? new PowerService();
        _snifferService = snifferService ?? new ClipboardSnifferService(_clipboardService, _settingsService);
        _persistenceService = persistenceService ?? new DownloadPersistenceService();

        _settings = _settingsService.LoadSettings();
        IsDarkMode = _settings.IsDarkMode;
        ThemeModeTitle = IsDarkMode ? "LCD Dark" : "Light";

        // Load Settings to ViewModel
        IsSmartFolderRoutingEnabled = _settings.IsSmartFolderRoutingEnabled;
        IsAutoExtractZipEnabled = _settings.IsAutoExtractZipEnabled;
        IsClipboardSnifferEnabled = _settings.IsClipboardSnifferEnabled;
        IsSoundEnabled = _settings.IsSoundEnabled;
        MinimizeToTray = _settings.MinimizeToTray;
        CloseToTray = _settings.CloseToTray;
        DefaultDownloadDirectory = _settings.DefaultDownloadDirectory;
        DefaultThreadsPerDownload = _settings.DefaultThreadsPerDownload;
        SelectedPostDownloadAction = _settings.PostDownloadAction;

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        // Sniffer Event
        _snifferService.DownloadableUrlDetected += OnSnifferUrlDetected;
        if (_settings.IsClipboardSnifferEnabled)
        {
            _snifferService.Start();
        }

        // Stats Refresh Timer
        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _statsTimer.Tick += (s, e) => RefreshStats();
        _statsTimer.Start();

        // Scheduler Check Timer
        _scheduleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _scheduleTimer.Tick += (s, e) => CheckScheduledDownloads();
        _scheduleTimer.Start();

        // Notification dismiss timer
        _notificationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _notificationTimer.Tick += (s, e) =>
        {
            HasNotification = false;
            _notificationTimer.Stop();
        };

        // Load and restore persistent downloads
        var savedDownloads = _persistenceService.LoadDownloads();
        foreach (var item in savedDownloads)
        {
            AllDownloads.Add(item);
        }

        ApplyFilter();
    }

    public MainViewModel() : this(
        new DownloadService(),
        new ClipboardService(),
        new FileService(),
        new SettingsService(),
        null,
        null,
        null,
        new DownloadPersistenceService())
    {
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
    partial void OnInspectorTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTab0));
        OnPropertyChanged(nameof(IsTab1));
        OnPropertyChanged(nameof(IsTab2));
    }

    partial void OnSelectedSpeedCapIndexChanged(int value)
    {
        long limit = value switch
        {
            1 => 10 * 1024 * 1024,
            2 => 5 * 1024 * 1024,
            3 => 2 * 1024 * 1024,
            4 => 500 * 1024,
            _ => 0
        };

        _settings.GlobalSpeedLimitBytesPerSec = limit;
        _settingsService.SaveSettings(_settings);

        var limitStr = limit > 0 ? $"{DownloadItem.FormatBytes(limit)}/s" : "Unlimited";
        ShowNotification($"Speed limit set to: {limitStr}");
    }

    public void ApplyFilter()
    {
        var query = SearchQuery?.Trim().ToLowerInvariant();
        var filtered = AllDownloads.AsEnumerable();

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
        CurrentAggregateSpeed = totalSpeed;
        TotalDownloadSpeedFormatted = totalSpeed > 0 ? $"{DownloadItem.FormatBytes((long)totalSpeed)}/s" : "0 B/s";

        // Periodic debounced auto-save during active downloads
        if (active > 0 && (DateTime.Now - _lastSavedTime).TotalSeconds >= 4)
        {
            SaveDownloadsState();
            _lastSavedTime = DateTime.Now;
        }

        // Check power management automation
        if (active == 0 && AllDownloads.Count > 0 && SelectedPostDownloadAction != PostDownloadAction.None && !_hasExecutedPowerAction)
        {
            bool allFinished = AllDownloads.All(x => x.Status == DownloadStatus.Completed || x.Status == DownloadStatus.Failed || x.Status == DownloadStatus.Canceled);
            if (allFinished)
            {
                SaveDownloadsState();
                _hasExecutedPowerAction = true;
                _powerService.ExecuteAction(SelectedPostDownloadAction);
            }
        }
        else if (active > 0)
        {
            _hasExecutedPowerAction = false;
        }
    }

    private void CheckScheduledDownloads()
    {
        var now = DateTime.Now;
        var dueItems = AllDownloads.Where(x => x.IsScheduled && x.ScheduledStartTime.HasValue && x.ScheduledStartTime.Value <= now && x.Status == DownloadStatus.Queued).ToList();

        foreach (var item in dueItems)
        {
            item.IsScheduled = false;
            _ = _downloadService.StartDownloadAsync(item);
            ShowNotification($"Scheduled download '{item.FileName}' started.");
        }
    }

    private void OnSnifferUrlDetected(string url)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsAddModalOpen) return;

            AddDialogVm = new AddDownloadDialogViewModel(_clipboardService, _fileService, _downloadService, _settingsService)
            {
                UrlInput = url
            };
            AddDialogVm.OnConfirmed += OnAddDownloadConfirmed;
            AddDialogVm.OnCanceled += () => IsAddModalOpen = false;
            IsAddModalOpen = true;

            ShowNotification($"Detected download link from clipboard!");
        });
    }

    public async Task ProcessIncomingUrlAsync(string url)
    {
        await Task.Yield();
        var urls = AddDownloadDialogViewModel.ExtractValidUrls(url);
        if (urls.Count > 0)
        {
            var defaultDir = _fileService.GetDefaultDownloadDirectory();
            foreach (var u in urls)
            {
                var fileName = _downloadService.ExtractFileNameFromUrl(u);
                var item = new DownloadItem
                {
                    Url = u,
                    FileName = fileName,
                    SaveDirectory = defaultDir,
                    MaxSegments = DefaultThreadsPerDownload,
                    Status = DownloadStatus.Queued,
                    CreatedAt = DateTime.Now
                };
                AllDownloads.Insert(0, item);
                _ = _downloadService.StartDownloadAsync(item);
            }
            ApplyFilter();
            SaveDownloadsState();
            ShowNotification($"Added & started {urls.Count} download(s)");
        }
    }

    [RelayCommand]
    private void OpenAddModal()
    {
        AddDialogVm = new AddDownloadDialogViewModel(_clipboardService, _fileService, _downloadService, _settingsService);
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
            AddDialogVm = new AddDownloadDialogViewModel(_clipboardService, _fileService, _downloadService, _settingsService)
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

    private void OnAddDownloadConfirmed(
        List<(string Url, string FileName, string SaveDir, int Threads, bool AutoExtract, DateTime? ScheduledTime)> items,
        bool startNow)
    {
        IsAddModalOpen = false;

        foreach (var (url, fileName, saveDir, threads, autoExtract, scheduledTime) in items)
        {
            var item = new DownloadItem
            {
                Url = url,
                FileName = fileName,
                SaveDirectory = saveDir,
                MaxSegments = threads,
                AutoExtractZip = autoExtract,
                ScheduledStartTime = scheduledTime,
                IsScheduled = scheduledTime.HasValue,
                Status = (startNow && !scheduledTime.HasValue) ? DownloadStatus.Connecting : DownloadStatus.Queued,
                CreatedAt = DateTime.Now
            };

            AllDownloads.Insert(0, item);

            if (startNow && !scheduledTime.HasValue)
            {
                _ = _downloadService.StartDownloadAsync(item);
            }
        }

        ApplyFilter();
        SaveDownloadsState();
        ShowNotification($"Added {items.Count} item{(items.Count > 1 ? "s" : "")} to download queue");
    }

    // Inspector Drawer Commands
    [RelayCommand]
    private void OpenInspector(DownloadItem? item)
    {
        if (item == null) return;
        InspectedItem = item;
        CalculatedChecksum = item.ChecksumSha256 ?? item.ChecksumMd5 ?? string.Empty;
        ExpectedChecksumInput = string.Empty;
        ChecksumMatchResult = null;
        InspectorTab = 0;
        IsInspectorOpen = true;
    }

    [RelayCommand]
    private void CloseInspector()
    {
        IsInspectorOpen = false;
    }

    [RelayCommand]
    private void SelectInspectorTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out var tabIndex))
        {
            InspectorTab = tabIndex;
        }
    }

    [RelayCommand]
    private async Task ComputeChecksumAsync()
    {
        if (InspectedItem == null || !File.Exists(InspectedItem.FullPath))
        {
            ShowNotification("File does not exist on disk yet.");
            return;
        }

        IsCalculatingChecksum = true;
        try
        {
            var hash = await _downloadService.ComputeHashAsync(InspectedItem.FullPath, ChecksumAlgorithm);
            CalculatedChecksum = hash;

            if (ChecksumAlgorithm == "MD5")
            {
                InspectedItem.ChecksumMd5 = hash;
            }
            else
            {
                InspectedItem.ChecksumSha256 = hash;
            }

            VerifyChecksumMatch();
            ShowNotification($"{ChecksumAlgorithm} hash generated.");
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to compute hash: {ex.Message}");
        }
        finally
        {
            IsCalculatingChecksum = false;
        }
    }

    [RelayCommand]
    private void VerifyChecksumMatch()
    {
        if (string.IsNullOrWhiteSpace(CalculatedChecksum) || string.IsNullOrWhiteSpace(ExpectedChecksumInput))
        {
            ChecksumMatchResult = null;
            OnPropertyChanged(nameof(ChecksumMatchStatusText));
            return;
        }

        bool match = string.Equals(CalculatedChecksum.Trim(), ExpectedChecksumInput.Trim(), StringComparison.OrdinalIgnoreCase);
        ChecksumMatchResult = match;
        OnPropertyChanged(nameof(ChecksumMatchStatusText));
    }

    [RelayCommand]
    private async Task PasteExpectedChecksumAsync()
    {
        var text = await _clipboardService.GetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
        {
            ExpectedChecksumInput = text.Trim();
            VerifyChecksumMatch();
        }
    }

    // Floating Mini Widget
    [RelayCommand]
    private void ToggleMiniWidget()
    {
        if (_miniWindow == null)
        {
            _miniWindow = new MiniDropWindow
            {
                DataContext = this
            };
        }

        if (IsMiniWidgetVisible)
        {
            _miniWindow.Hide();
            IsMiniWidgetVisible = false;
        }
        else
        {
            _miniWindow.Show();
            IsMiniWidgetVisible = true;
        }
    }

    public void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            desktop.MainWindow.WindowState = WindowState.Normal;
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
        }
    }

    // Settings Drawer Commands
    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    [RelayCommand]
    private async Task BrowseDefaultDirectoryAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.StorageProvider is { } storage)
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Default Download Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var path = folders[0].TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    DefaultDownloadDirectory = path;
                }
            }
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.IsSmartFolderRoutingEnabled = IsSmartFolderRoutingEnabled;
        _settings.IsAutoExtractZipEnabled = IsAutoExtractZipEnabled;
        _settings.IsClipboardSnifferEnabled = IsClipboardSnifferEnabled;
        _settings.IsSoundEnabled = IsSoundEnabled;
        _settings.MinimizeToTray = MinimizeToTray;
        _settings.CloseToTray = CloseToTray;
        _settings.DefaultDownloadDirectory = DefaultDownloadDirectory;
        _settings.DefaultThreadsPerDownload = DefaultThreadsPerDownload;
        _settings.PostDownloadAction = SelectedPostDownloadAction;

        _settingsService.SaveSettings(_settings);

        if (IsClipboardSnifferEnabled && !_snifferService.IsRunning)
        {
            _snifferService.Start();
        }
        else if (!IsClipboardSnifferEnabled && _snifferService.IsRunning)
        {
            _snifferService.Stop();
        }

        IsSettingsOpen = false;
        ShowNotification("Settings updated successfully");
    }

    [RelayCommand]
    private void StartDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.ResumeDownload(item);
        ApplyFilter();
        SaveDownloadsState();
    }

    [RelayCommand]
    private void PauseDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.PauseDownload(item);
        ApplyFilter();
        SaveDownloadsState();
    }

    [RelayCommand]
    private void CancelDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.CancelDownload(item);
        ApplyFilter();
        SaveDownloadsState();
    }

    [RelayCommand]
    private void DeleteDownload(DownloadItem? item)
    {
        if (item == null) return;
        _downloadService.CancelDownload(item);
        AllDownloads.Remove(item);
        if (InspectedItem == item)
        {
            InspectedItem = null;
            IsInspectorOpen = false;
        }
        ApplyFilter();
        SaveDownloadsState();
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
        SaveDownloadsState();
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
        SaveDownloadsState();
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
        SaveDownloadsState();
        ShowNotification($"Cleared {completedList.Count} completed download{(completedList.Count > 1 ? "s" : "")}");
    }

    public void SaveDownloadsState()
    {
        _persistenceService.SaveDownloads(AllDownloads);
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

        _settings.IsDarkMode = IsDarkMode;
        _settingsService.SaveSettings(_settings);

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

        _notificationTimer.Stop();
        _notificationTimer.Start();
    }
}
