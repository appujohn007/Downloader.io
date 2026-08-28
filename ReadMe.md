# Downloader.io ⚡

A modern, sleek, and high-performance Windows download manager built with **Avalonia UI** and **C# (.NET 8)**.

---

## ✨ Features

- **Clipboard Auto-Paste**: Instant detection and parsing of download URLs from your Windows clipboard with a single click.
- **Batch Add Links**: Add single or multiple download links simultaneously with custom destination folder and immediate download options.
- **Asynchronous Download Engine**:
  - Resumable downloads via HTTP `Range` request headers.
  - Real-time download speed calculation (KB/s, MB/s) and live aggregate speed meter.
  - Smooth progress metrics, time remaining (ETA), and downloaded size indicators.
  - Pause, Resume, Cancel, and Delete download controls.
- **File & Folder Actions**: One-click opening of completed files or locating items inside Windows File Explorer (`explorer.exe /select`).
- **LCD-Optimized Dark Mode & Light Mode**:
  - Specially calibrated LCD dark theme (`#0E1015` / `#181B24`) designed to prevent washed-out gray haze and backlight glow on LCD panels.
  - Full support for Light Theme with clean porcelain tones.
  - Instant one-click toggle in the titlebar.
- **Filters & Categories**: Filter downloads by status (*All, Downloading, Completed, Paused, Failed*) or by file type (*Compressed, Programs, Media, Documents*).

---

## 🚀 How to Run in Development Mode

You **do not need to compile or reinstall an `.exe` installer** every time you make changes. 

Simply **double-click** [`run.bat`](file:///d:/Git/Downloader.io/run.bat) in the project root:

```cmd
run.bat
```

Or from PowerShell / Command Prompt:
```bash
dotnet run
```

---

## 📁 Project Structure

```
Downloader.io/
├── Assets/                        # Icons and application resources
├── Models/                        # Data structures and domain models
│   ├── DownloadEnums.cs           # Download status & category definitions
│   └── DownloadItem.cs            # Download task item with formatted metrics
├── Services/                      # Application backend services
│   ├── ClipboardService.cs        # Windows clipboard integration & URL extraction
│   ├── DownloadService.cs         # HTTP chunked streaming & Range resume engine
│   └── FileService.cs             # Windows Explorer and file execution
├── ViewModels/                    # MVVM view models (CommunityToolkit.Mvvm)
│   ├── ViewModelBase.cs           # Base view model
│   ├── MainViewModel.cs           # Primary application view model
│   └── AddDownloadDialogViewModel.cs # Modal dialog logic for adding links
├── Views/                         # Avalonia XAML views
│   ├── MainWindow.axaml           # Modern acrylic / LCD dark UI window
│   └── MainWindow.axaml.cs        # Window lifecycle & titlebar dragging
├── App.axaml                      # Global design system, colors & theme palettes
├── App.axaml.cs                   # Application entry point
├── Program.cs                     # Main desktop launcher
├── ViewLocator.cs                 # XAML MVVM view locator
├── app.manifest                   # Windows DPI and OS compatibility manifest
├── Downloader.csproj              # .NET 8 / Avalonia 11 project file
└── run.bat                        # One-click dev executor script
```
