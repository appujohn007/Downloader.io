using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace DownloaderApp.Services;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
    Task<List<string>> ExtractUrlsFromClipboardAsync();
}

public class ClipboardService : IClipboardService
{
    private static readonly Regex UrlRegex = new(
        @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<string?> GetTextAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            return await clipboard.GetTextAsync();
        }
        return null;
    }

    public async Task SetTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public async Task<List<string>> ExtractUrlsFromClipboardAsync()
    {
        var list = new List<string>();
        var text = await GetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return list;

        var matches = UrlRegex.Matches(text);
        foreach (Match match in matches)
        {
            if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                if (!list.Contains(match.Value))
                {
                    list.Add(match.Value);
                }
            }
        }

        return list;
    }
}
