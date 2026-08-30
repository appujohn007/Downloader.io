using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DownloaderApp.ViewModels;

namespace DownloaderApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Allow dragging window from top 38px titlebar
        var point = e.GetCurrentPoint(this);
        if (point.Position.Y <= 38 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // In-app Ctrl+V hotkey to paste and add download task
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V)
        {
            if (FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            if (DataContext is MainViewModel vm && !vm.IsAddModalOpen)
            {
                e.Handled = true;
                await vm.PasteFromClipboardCommand.ExecuteAsync(null);
            }
        }
    }
}
