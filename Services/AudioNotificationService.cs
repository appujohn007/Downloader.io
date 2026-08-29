using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DownloaderApp.Services;

public interface IAudioNotificationService
{
    void PlayDownloadCompleted();
    void PlayDownloadFailed();
}

public class AudioNotificationService : IAudioNotificationService
{
    private readonly ISettingsService _settingsService;

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    public AudioNotificationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void PlayDownloadCompleted()
    {
        try
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.IsSoundEnabled) return;

            Task.Run(() =>
            {
                if (OperatingSystem.IsWindows())
                {
                    // MB_ICONASTERISK = 0x00000040L
                    MessageBeep(0x00000040);
                }
            });
        }
        catch
        {
            // Ignore audio playback errors
        }
    }

    public void PlayDownloadFailed()
    {
        try
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.IsSoundEnabled) return;

            Task.Run(() =>
            {
                if (OperatingSystem.IsWindows())
                {
                    // MB_ICONHAND = 0x00000010L
                    MessageBeep(0x00000010);
                }
            });
        }
        catch
        {
            // Ignore audio playback errors
        }
    }
}

