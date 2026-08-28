using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DownloaderApp.Controls;

public class BlockProgressBackground : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(Progress), 0.0);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsActive), false);

    public static readonly StyledProperty<bool> IsCompletedProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsCompleted), false);

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellSize), 8.0);

    public static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellGap), 3.0);

    public static readonly StyledProperty<double> CellCornerRadiusProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellCornerRadius), 1.5);

    public static readonly StyledProperty<IBrush?> EmptyBorderBrushProperty =
        AvaloniaProperty.Register<BlockProgressBackground, IBrush?>(nameof(EmptyBorderBrush));

    public static readonly StyledProperty<IBrush?> FilledBrushProperty =
        AvaloniaProperty.Register<BlockProgressBackground, IBrush?>(nameof(FilledBrush));

    public static readonly StyledProperty<IBrush?> FilledBorderBrushProperty =
        AvaloniaProperty.Register<BlockProgressBackground, IBrush?>(nameof(FilledBorderBrush));

    public static readonly StyledProperty<IBrush?> ActiveHeadBrushProperty =
        AvaloniaProperty.Register<BlockProgressBackground, IBrush?>(nameof(ActiveHeadBrush));

    static BlockProgressBackground()
    {
        AffectsRender<BlockProgressBackground>(
            ProgressProperty,
            IsActiveProperty,
            IsCompletedProperty,
            CellSizeProperty,
            CellGapProperty,
            CellCornerRadiusProperty,
            EmptyBorderBrushProperty,
            FilledBrushProperty,
            FilledBorderBrushProperty,
            ActiveHeadBrushProperty);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsCompleted
    {
        get => GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public double CellGap
    {
        get => GetValue(CellGapProperty);
        set => SetValue(CellGapProperty, value);
    }

    public double CellCornerRadius
    {
        get => GetValue(CellCornerRadiusProperty);
        set => SetValue(CellCornerRadiusProperty, value);
    }

    public IBrush? EmptyBorderBrush
    {
        get => GetValue(EmptyBorderBrushProperty);
        set => SetValue(EmptyBorderBrushProperty, value);
    }

    public IBrush? FilledBrush
    {
        get => GetValue(FilledBrushProperty);
        set => SetValue(FilledBrushProperty, value);
    }

    public IBrush? FilledBorderBrush
    {
        get => GetValue(FilledBorderBrushProperty);
        set => SetValue(FilledBorderBrushProperty, value);
    }

    public IBrush? ActiveHeadBrush
    {
        get => GetValue(ActiveHeadBrushProperty);
        set => SetValue(ActiveHeadBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        double cell = CellSize > 0 ? CellSize : 8.0;
        double gap = CellGap >= 0 ? CellGap : 3.0;
        double radius = CellCornerRadius >= 0 ? CellCornerRadius : 1.5;

        int cols = (int)Math.Max(1, Math.Floor((bounds.Width + gap) / (cell + gap)));
        int rows = (int)Math.Max(1, Math.Floor((bounds.Height + gap) / (cell + gap)));
        int totalCells = cols * rows;

        double actualGridWidth = (cols * cell) + ((cols - 1) * gap);
        double actualGridHeight = (rows * cell) + ((rows - 1) * gap);

        // Center grid inside available bounds
        double startX = Math.Max(0, (bounds.Width - actualGridWidth) / 2.0);
        double startY = Math.Max(0, (bounds.Height - actualGridHeight) / 2.0);

        double pct = Math.Clamp(Progress, 0.0, 100.0);
        int filledCount = (int)Math.Round((pct / 100.0) * totalCells);

        var emptyPen = new Pen(EmptyBorderBrush ?? new SolidColorBrush(Color.FromArgb(30, 140, 150, 170)), 0.9);
        var filledPen = new Pen(FilledBorderBrush ?? new SolidColorBrush(Color.FromArgb(60, 59, 130, 246)), 0.9);
        var fillBrush = FilledBrush ?? new SolidColorBrush(Color.FromArgb(38, 59, 130, 246));
        var headBrush = ActiveHeadBrush ?? new SolidColorBrush(Color.FromArgb(85, 56, 189, 248));

        int cellIndex = 0;
        for (int r = 0; r < rows; r++)
        {
            double y = startY + r * (cell + gap);
            for (int c = 0; c < cols; c++)
            {
                double x = startX + c * (cell + gap);
                var rect = new Rect(x, y, cell, cell);
                var rrect = new RoundedRect(rect, radius);

                if (cellIndex < filledCount)
                {
                    // If active downloading, highlight the leading edge cell
                    if (IsActive && cellIndex == filledCount - 1)
                    {
                        context.DrawRectangle(headBrush, filledPen, rrect);
                    }
                    else
                    {
                        context.DrawRectangle(fillBrush, filledPen, rrect);
                    }
                }
                else
                {
                    // Empty cell (border only)
                    context.DrawRectangle(null, emptyPen, rrect);
                }

                cellIndex++;
            }
        }
    }
}
