# Downloader.io ⚡

A modern, high-performance, and stylish Windows download manager built with **Avalonia UI 11** and **C# (.NET 8)**.

---

## ✨ Advanced Features

### 🚀 High-Performance Acceleration Engine
- **Multi-Threaded Segmented Acceleration**: Automatically slices large files into **4 to 16 concurrent HTTP byte-range threads** using high-speed non-blocking `RandomAccess` disk streams for maximum throughput.
- **Bandwidth Throttling / Speed Limiter**: Global and per-task speed limits (*Unlimited, 10 MB/s, 5 MB/s, 2 MB/s, 500 KB/s*).
- **Auto-Recovery & Exponential Backoff**: Automatically recovers and retries transient network interruptions and rate limits without failing download jobs.

### 🎨 Next-Gen UI/UX & Visual Aesthetics
- **Real-Time Speed Waveform Chart**: Live rolling 60-second speed sparkline in the header with gradient fill and peak speed readout.
- **Download Inspector & Details Drawer**:
  - **Live Range Threads Visualizer**: Displays active progress, byte range, and speed for each concurrent thread.
  - **Cryptographic Hash Verifier**: Calculate MD5 and SHA-256 hashes on completed downloads with one-click clipboard matching.
  - **File & Server Inspector**: View remote server headers, MIME types, response status, and disk paths.
- **Floating Mini Drop Zone**: Minimalist, always-on-top draggable desktop widget with live speed pill and drag-and-drop link ingestion.
- **Reactive Matrix Block Control**: Skia-based background matrix rendering 10 distinct dynamic color palettes behind active download cards.

### 🧠 Smart Automation & File Management
- **Smart Folder Routing**: Automatically routes downloads into subfolders (`Downloads/Archives`, `Downloads/Videos`, `Downloads/Programs`, `Downloads/Documents`) based on file extension.
- **Background Clipboard Sniffer**: Detects copied download links (`.zip`, `.exe`, `.iso`, `.mp4`, etc.) and presents instant quick-add prompts.
- **Auto-Extract ZIP Archives**: Automatically unpacks downloaded `.zip` archives upon completion.
- **Scheduled Tasks**: Schedule downloads to start at a specified time.
- **Post-Download Power Actions**: Automatically shut down, sleep, or hibernate your PC when all active downloads complete.
- **Subtle Audio Feedback**: Cybernetic sound alerts on download completion and errors.

---

## 🚀 How to Run in Development Mode

Simply **double-click** [`run.bat`](file:///d:/Git/Downloader.io/run.bat) in the project root:

```cmd
run.bat
```

Or run via PowerShell / Terminal:
```bash
dotnet run
```

---

## 📁 Project Architecture

```
Downloader.io/
├── Assets/                        # Icons and application resources
├── Controls/                      # Custom Skia UI controls
│   ├── BlockProgressBackground.cs # Matrix visualizer control
│   └── SpeedWaveformControl.cs    # Real-time rolling speed sparkline
├── Models/                        # Data structures and domain models
│   ├── AppSettings.cs             # Configuration options & power actions
│   ├── DownloadEnums.cs           # Status and category enumerations
│   ├── DownloadItem.cs            # Download item with segments & metrics
│   └── DownloadSegment.cs         # Thread chunk progress & range tracking
├── Services/                      # Application backend services
│   ├── AudioNotificationService.cs# System and synthetic audio cues
│   ├── ClipboardService.cs        # Windows clipboard integration
│   ├── ClipboardSnifferService.cs # Background clipboard watcher daemon
│   ├── DownloadService.cs         # Multi-threaded range engine & extractor
│   ├── FileService.cs             # Explorer integration & file execution
│   ├── PowerService.cs            # Windows power management automation
│   └── SettingsService.cs         # JSON configuration persistence
├── ViewModels/                    # MVVM view models (CommunityToolkit.Mvvm)
│   ├── AddDownloadDialogViewModel.cs # Add download modal logic
│   ├── MainViewModel.cs           # Core application view model
│   └── ViewModelBase.cs           # Base view model
├── Views/                         # Avalonia XAML views
│   ├── MainWindow.axaml           # Primary application window
│   ├── MainWindow.axaml.cs        # Window interactions & drag
│   ├── MiniDropWindow.axaml       # Draggable floating drop widget
│   └── MiniDropWindow.axaml.cs    # Mini widget drop handling
├── App.axaml                      # Theme resources, colors & styles
├── App.axaml.cs                   # Application entry point
├── Downloader.csproj              # .NET 8 / Avalonia 11 project file
└── run.bat                        # One-click dev executor script
```
