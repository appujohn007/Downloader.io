using System;
using System.IO;
using System.Text.Json;
using DownloaderApp.Models;

namespace DownloaderApp.Services;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
    event Action<AppSettings>? SettingsChanged;
}

public class SettingsService : ISettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Downloader.io");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private AppSettings? _cachedSettings;

    public event Action<AppSettings>? SettingsChanged;

    public AppSettings CurrentSettings => _cachedSettings ?? LoadSettings();

    public AppSettings LoadSettings()
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    _cachedSettings = settings;
                    Logger.Info($"[SETTINGS] Initialized settings from {SettingsFilePath} (Theme: {(settings.IsDarkMode ? "Dark" : "Light")})");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load settings from {SettingsFilePath}: {ex.Message}");
        }

        var defaults = new AppSettings();
        _cachedSettings = defaults;
        SaveSettings(defaults);
        return defaults;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            _cachedSettings = settings;

            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            Logger.Debug($"[SETTINGS] Saved settings to {SettingsFilePath}");

            SettingsChanged?.Invoke(settings);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to save settings to {SettingsFilePath}: {ex.Message}");
        }
    }
}
