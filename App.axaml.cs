using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DownloaderApp.ViewModels;
using DownloaderApp.Views;

namespace DownloaderApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };

            desktop.MainWindow.Closing += (s, e) =>
            {
                mainVm.SaveDownloadsState();
            };

            desktop.Exit += (s, e) =>
            {
                mainVm.SaveDownloadsState();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
