using System;

namespace DownloaderApp.Services;

public static class Logger
{
    private static readonly object Lock = new();

    public static void Info(string message)
    {
        Log("INFO", message, ConsoleColor.Cyan);
    }

    public static void Debug(string message)
    {
        Log("DEBUG", message, ConsoleColor.DarkGray);
    }

    public static void Success(string message)
    {
        Log("SUCCESS", message, ConsoleColor.Green);
    }

    public static void Warn(string message)
    {
        Log("WARN", message, ConsoleColor.Yellow);
    }

    public static void Error(string message, Exception? ex = null)
    {
        Log("ERROR", ex != null ? $"{message} | Exception: {ex.Message}\n{ex.StackTrace}" : message, ConsoleColor.Red);
    }

    public static void Download(string message)
    {
        Log("DOWNLOAD", message, ConsoleColor.Magenta);
    }

    private static void Log(string level, string message, ConsoleColor color)
    {
        lock (Lock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");

            Console.ForegroundColor = color;
            Console.Write($"[{level,-8}] ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);

            Console.ForegroundColor = originalColor;
        }
    }
}
