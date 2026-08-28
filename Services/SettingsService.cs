using System;
using System.IO;
using System.Text.Json;
using DownloaderApp.Models;

namespace DownloaderApp.Services;

public interface ISettingsService
{
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
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

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    Logger.Info($"[SETTINGS] Loaded settings from {SettingsFilePath} (Theme: {(settings.IsDarkMode ? "Dark" : "Light")})");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load settings from {SettingsFilePath}: {ex.Message}");
        }

        var defaults = new AppSettings();
        SaveSettings(defaults);
        return defaults;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            Logger.Debug($"[SETTINGS] Saved settings to {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to save settings to {SettingsFilePath}: {ex.Message}");
        }
    }
}
