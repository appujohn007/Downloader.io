using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DownloaderApp.ViewModels;

namespace DownloaderApp.Views;

public partial class MiniDropWindow : Window
{
    public MiniDropWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void RestoreMain_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ShowMainWindow();
        }
    }

    private void CloseMini_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Text) || e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (e.Data.Contains(DataFormats.Text))
            {
                var text = e.Data.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await vm.ProcessIncomingUrlAsync(text);
                }
            }
        }
    }
}

