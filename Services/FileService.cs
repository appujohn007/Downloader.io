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
            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"\"{filePath}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", $"\"{filePath}\"");
            }
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
            var targetFile = File.Exists(filePath) ? filePath : (File.Exists($"{filePath}.downloaderio") ? $"{filePath}.downloaderio" : null);
            var dir = !string.IsNullOrEmpty(targetFile) ? Path.GetDirectoryName(targetFile) : (Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath));

            if (OperatingSystem.IsWindows())
            {
                if (!string.IsNullOrEmpty(targetFile))
                {
                    Process.Start("explorer.exe", $"/select,\"{targetFile}\"");
                }
                else if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", $"\"{dir}\"");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (!string.IsNullOrEmpty(targetFile))
                {
                    Process.Start("open", $"-R \"{targetFile}\"");
                }
                else if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("open", $"\"{dir}\"");
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start("xdg-open", $"\"{dir}\"");
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
