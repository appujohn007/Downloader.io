<div align="center">

# ⚡ Downloader.io

**The next-generation, high-speed, open-source desktop download manager.**  
*Engineered with .NET 8, Avalonia UI 11, and hardware-accelerated Skia rendering.*

[![Release](https://img.shields.io/badge/release-v2.0.0-blue.svg?style=flat-square)](https://github.com/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/UI-Avalonia%2011-854CFF.svg?style=flat-square&logo=avalonia)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg?style=flat-square)](https://github.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](https://github.com/)

[**Features**](#-features) • [**Quick Start**](#-quick-start) • [**Comparison**](#-comparison) • [**Shortcuts**](#-keyboard-shortcuts) • [**Architecture**](#-architecture) • [**Building**](#-building-from-source)

</div>

---

## 📖 Overview

**Downloader.io** is a modern, lightweight, and blazingly fast download manager designed to replace clunky legacy tools with a streamlined UI and cutting-edge networking stack. 

Under the hood, it features an asynchronous multi-stream byte-range slicing engine powered by .NET 8 non-blocking `RandomAccess` disk I/O, smooth Skia-driven fluid visualizers, forensic magic-byte file inspection, and an adaptive token-bucket speed limiter.

---

## ✨ Features

### 🚀 Hyper-Speed Multi-Stream Engine
* **Concurrent Byte-Range Slicing**: Slices large files into **1 to 16 parallel range threads**, maximizing HTTP throughput and saturating multi-gigabit connections.
* **Non-Blocking Zero-Contention I/O**: Direct concurrent chunk writing via `RandomAccess.WriteAsync` directly into target pre-allocated containers without thread locking.
* **Atomic Sidecar Checkpointing**: Interrupted downloads save `.meta` state checkpoints in AppData, enabling reliable instant resumption across network interruptions and app restarts.
* **Smooth Token-Bucket Rate Limiter**: Jitter-free bandwidth throttling with microsecond token replenishment (*Unlimited, 10 MB/s, 5 MB/s, 2 MB/s, 500 KB/s*).

### 🎨 Next-Gen Skia UI & Visual Aesthetics
* **Dynamic Speed Waveform**: Real-time 60 FPS Catmull-Rom cubic Bezier rolling speed chart with luminous aurora gradients and traveling fluid harmonics.
* **Skia Matrix Visualizer**: Skia-rendered reactive matrix tile grid displaying live thread write-head pulses, dynamic golden-ratio harmonic color palettes, and scenario watermarks.
* **Floating Mini Drop Zone**: Minimalist, always-on-top draggable companion widget for dragging links and quick-dropping URLs directly from your browser.
* **Adaptive Dark/Light LCD Themes**: Hand-crafted themes with fluid UI transitions and Windows Mica / Acrylic transparency blur hints.

### 🔬 Deep Forensics & Security
* **Binary Magic-Byte Inspection**: Automatically identifies true underlying file formats (PE Executables, ELF, ISO, Zip, OpenXML, MP4, FLAC, PDF, etc.) from raw binary headers, detecting file spoofing.
* **Cryptographic Multi-Hash Verifier**: Fast 1MB-buffered parallel stream hashing across **SHA-256, SHA-1, MD5, SHA-512, and CRC-32**, with automatic verification against remote HTTP ETags.
* **Zip Slip Path Traversal Immunity**: Hardened automatic decompression engine preventing malicious archives from escaping target directories.
* **Anti-Bot Browser Emulation**: Configurable User-Agent profiles, Chromium Sec-CH and Sec-Fetch headers, standard Accept MIME chains, and dynamic Referer injection.

### 🧠 Smart Automation
* **Smart Category Folder Routing**: Automatically sorts incoming downloads into organized directories (`Archives`, `Media`, `Programs`, `Documents`) based on verified file categories.
* **In-App `Ctrl+V` Quick Ingestion**: Press `Ctrl+V` anywhere in the app to instantly parse clipboard URLs, probe remote headers, and launch download tasks.
* **Post-Download Power Actions**: Automate system actions upon queue completion (**Shutdown**, **Sleep**, **Hibernate**) with cross-platform native execution.
* **Task Scheduling**: Schedule bandwidth-intensive transfers to start during off-peak hours.

---

## 📊 Comparison

| Feature | Downloader.io ⚡ | Legacy Download Managers | Typical Browser |
| :--- | :---: | :---: | :---: |
| **Multi-Thread Range Acceleration** | ✅ **Up to 16 Streams** | ✅ Yes | ❌ Single Stream |
| **Disk I/O Contention Architecture** | ✅ **`RandomAccess` Zero-Merge** | ⚠️ Merges Temp Files | ⚠️ Single Stream |
| **Modern GPU-Accelerated UI** | ✅ **Avalonia 11 + Skia** | ❌ 90s/2000s Win32 UI | ⚠️ Basic List |
| **Forensic Magic-Byte Detection** | ✅ **17+ Binary Formats** | ❌ Extension Only | ❌ Extension Only |
| **Multi-Algorithm Checksums** | ✅ **SHA256/1/512, MD5, CRC32** | ⚠️ MD5 / SHA1 Only | ❌ None |
| **Bandwidth Rate Limiter** | ✅ **Token-Bucket (No Jitter)** | ⚠️ Delay-Based | ❌ None |
| **Ad-Free, Telemetry-Free & Open Source** | ✅ **100% Free & Open** | ❌ Proprietary / Ads / Nag | ⚠️ Closed / Tracked |
| **Floating Drop Zone Companion** | ✅ **Yes** | ⚠️ Some | ❌ None |

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action | Scope |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>V</kbd> | Paste URL(s) from clipboard and open Add Task dialog | Global (In-App) |
| <kbd>Ctrl</kbd> + <kbd>N</kbd> / Click `Add` | Open manual Add Download dialog | Main Window |
| <kbd>Esc</kbd> | Dismiss modals, drawers, or floating drop zone | Any active modal |
| Click Speed Chart | Open Live Real-time Network Analytics & Telemetry modal | Header |

---

## 🚀 Quick Start

### Running with One-Click Script (Windows)

Simply execute [`run.bat`](file:///d:/Git/Downloader.io/run.bat) from the project folder:

```cmd
run.bat
```

### Running with .NET CLI (Cross-Platform)

Make sure [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is installed:

```bash
# Clone the repository
git clone https://github.com/your-username/Downloader.io.git
cd Downloader.io

# Restore dependencies & run
dotnet run
```

---

## 🛠️ Building from Source

### Standard Release Build

```bash
dotnet build -c Release
```

### Publish Self-Contained Native Binary

#### Windows x64 (Single-File Exe):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

#### Linux x64:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

#### macOS (Apple Silicon / ARM64):
```bash
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

The output standalone binary will be located in `bin/Release/net8.0/<rid>/publish/`.

---

## 🏗️ Architecture

Downloader.io is designed with strict **MVVM** separation and modular service abstractions:

```
Downloader.io/
├── Controls/                      # GPU-accelerated custom Skia renderers
│   ├── BlockProgressBackground.cs # Matrix visualizer & harmonic palette generator
│   └── SpeedWaveformControl.cs    # Catmull-Rom cubic Bezier speed waveform
├── Models/                        # Domain models & telemetry structures
│   ├── AppSettings.cs             # User preferences & power action states
│   ├── DownloadItem.cs            # Master download entity with telemetry metrics
│   └── DownloadSegment.cs         # Parallel stream chunk entity & range boundaries
├── Services/                      # Pure backend engine services
│   ├── AudioNotificationService.cs# Cross-platform sound cues
│   ├── ClipboardService.cs        # In-app clipboard extractor
│   ├── DownloadPersistenceService # Atomic JSON state sidecar persistence
│   ├── DownloadService.cs         # Token-Bucket multi-threaded acceleration engine
│   ├── FileInspectionService.cs   # Forensic magic-byte parser & multi-hash engine
│   ├── FileService.cs             # Cross-platform file manager integration
│   ├── PowerService.cs            # Windows/Linux/macOS power automation
│   └── SettingsService.cs         # App configuration management
├── ViewModels/                    # Reactive ViewModels (CommunityToolkit.Mvvm)
│   ├── AddDownloadDialogViewModel # URL parsing, metadata probing & validation
│   ├── MainViewModel.cs           # Main state coordinator & filter engine
│   └── ViewModelBase.cs           # Base observable object
└── Views/                         # Avalonia XAML Views
    ├── MainWindow.axaml           # Modern desktop interface & inspection drawer
    └── MiniDropWindow.axaml       # Always-on-top draggable floating drop companion
```

---

## 🔒 Privacy & Security Policy

* **Zero Telemetry / Zero Tracking**: Downloader.io never collects, logs, or transmits personal data, browsing history, or download links to third-party servers.
* **Direct Origin Communication**: Network connections are established solely between your machine and the remote source host you provide.
* **Zip Slip Hardened**: Decompression routines strictly validate entry canonical paths to protect your filesystem against directory traversal attacks.

---

## 🤝 Contributing

Contributions, feature suggestions, and pull requests are warmly welcomed!

1. Fork the Project.
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
3. Commit your Changes (`git commit -m 'feat: Add some AmazingFeature'`).
4. Push to the Branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

---

## 📄 License

Distributed under the **MIT License**. See `LICENSE` for more information.

<div align="center">
  <sub>Built with ❤️ using Avalonia UI and .NET 8.</sub>
</div>
