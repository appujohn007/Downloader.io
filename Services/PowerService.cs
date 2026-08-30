using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DownloaderApp.Models;

namespace DownloaderApp.Services;

public interface IPowerService
{
    void ExecuteAction(PostDownloadAction action);
}

public class PowerService : IPowerService
{
    [DllImport("Powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public void ExecuteAction(PostDownloadAction action)
    {
        if (action == PostDownloadAction.None) return;

        Logger.Info($"[POWER] Executing post-download power action: {action}");

        try
        {
            switch (action)
            {
                case PostDownloadAction.Shutdown:
                    if (OperatingSystem.IsWindows())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "shutdown.exe",
                            Arguments = "/s /t 60 /c \"Downloader.io: All downloads completed. Shutting down in 60s.\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "systemctl",
                            Arguments = "poweroff",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "osascript",
                            Arguments = "-e 'tell app \"System Events\" to shut down'",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    break;

                case PostDownloadAction.Sleep:
                    if (OperatingSystem.IsWindows())
                    {
                        SetSuspendState(false, true, false);
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "systemctl",
                            Arguments = "suspend",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "pmset",
                            Arguments = "sleepnow",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    break;

                case PostDownloadAction.Hibernate:
                    if (OperatingSystem.IsWindows())
                    {
                        SetSuspendState(true, true, false);
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "systemctl",
                            Arguments = "hibernate",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to execute power action '{action}': {ex.Message}");
        }
    }
}

