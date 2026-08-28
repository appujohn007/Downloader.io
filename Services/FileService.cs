using System;
using System.Diagnostics;
using System.IO;

namespace DownloaderApp.Services;

public interface IFileService
{
    void OpenFile(string filePath);
    void OpenFolder(string filePath);
    string GetDefaultDownloadDirectory();
}

public class FileService : IFileService
{
    public void OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open file {filePath}: {ex.Message}");
        }
    }

    public void OpenFolder(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else
            {
                var dir = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", $"\"{dir}\"");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open folder for {filePath}: {ex.Message}");
        }
    }

    public string GetDefaultDownloadDirectory()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads))
        {
            Directory.CreateDirectory(downloads);
        }
        return downloads;
    }
}
