using System;
using System.Threading.Tasks;
using Avalonia;
using DownloaderApp.Services;

namespace DownloaderApp;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Global Unhandled Exception Handling for perfect terminal debugging
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Error("CRITICAL UNHANDLED EXCEPTION in AppDomain", ex);
            }
            else
            {
                Logger.Error($"CRITICAL UNHANDLED ERROR: {e.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Logger.Error("Unobserved Task Exception in background thread", e.Exception);
            e.SetObserved();
        };

        Logger.Info("==================================================");
        Logger.Info("       Downloader.io - Debug Console Session      ");
        Logger.Info("==================================================");
        Logger.Info($".NET Runtime: {Environment.Version} on {Environment.OSVersion}");
        Logger.Info("Starting Avalonia desktop UI...");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Logger.Error("Application startup crash", ex);
            throw;
        }
        finally
        {
            Logger.Info("Application session closed.");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
