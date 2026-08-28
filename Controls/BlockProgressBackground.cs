using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

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
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellGap), 2.5);

    public static readonly StyledProperty<double> CellCornerRadiusProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellCornerRadius), 1.5);

    public static readonly StyledProperty<bool> IsDarkModeProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsDarkMode), true);

    private double _animatedProgress = 0.0;
    private double _shimmerPhase = 0.0;
    private readonly DispatcherTimer _animTimer;

    static BlockProgressBackground()
    {
        AffectsRender<BlockProgressBackground>(
            BoundsProperty,
            ProgressProperty,
            IsActiveProperty,
            IsCompletedProperty,
            CellSizeProperty,
            CellGapProperty,
            CellCornerRadiusProperty,
            IsDarkModeProperty);
    }

    public BlockProgressBackground()
    {
        _animTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _animTimer.Tick += OnAnimationTick;
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

    public bool IsDarkMode
    {
        get => GetValue(IsDarkModeProperty);
        set => SetValue(IsDarkModeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ProgressProperty || change.Property == IsActiveProperty)
        {
            EnsureAnimationRunning();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureAnimationRunning();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animTimer.Stop();
    }

    private void EnsureAnimationRunning()
    {
        if (!_animTimer.IsEnabled)
        {
            _animTimer.Start();
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        bool needsRedraw = false;

        // Liquid smooth progress interpolation
        double target = Math.Clamp(Progress, 0.0, 100.0);
        double diff = target - _animatedProgress;

        if (Math.Abs(diff) > 0.05)
        {
            _animatedProgress += diff * 0.22;
            needsRedraw = true;
        }
        else if (_animatedProgress != target)
        {
            _animatedProgress = target;
            needsRedraw = true;
        }

        // Liquid shimmer wave phase
        if (IsActive)
        {
            _shimmerPhase = (_shimmerPhase + 0.06) % (Math.PI * 2.0);
            needsRedraw = true;
        }

        if (needsRedraw)
        {
            InvalidateVisual();
        }
        else if (!IsActive && Math.Abs(_animatedProgress - target) <= 0.05)
        {
            _animTimer.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1) return;

        double cell = CellSize > 0 ? CellSize : 8.0;
        double gap = CellGap >= 0 ? CellGap : 2.5;
        double radius = CellCornerRadius >= 0 ? CellCornerRadius : 1.5;

        // Dynamically compute columns and rows based on current bounds
        int cols = (int)Math.Max(1, Math.Floor((bounds.Width + gap) / (cell + gap)));
        int rows = (int)Math.Max(1, Math.Floor((bounds.Height + gap) / (cell + gap)));
        int totalCells = cols * rows;

        double actualGridWidth = (cols * cell) + ((cols - 1) * gap);
        double actualGridHeight = (rows * cell) + ((rows - 1) * gap);

        double startX = Math.Max(0, (bounds.Width - actualGridWidth) / 2.0);
        double startY = Math.Max(0, (bounds.Height - actualGridHeight) / 2.0);

        double pct = Math.Clamp(_animatedProgress, 0.0, 100.0);
        double exactFilled = (pct / 100.0) * totalCells;
        int filledCount = (int)Math.Floor(exactFilled);
        double fractionalCell = exactFilled - filledCount;

        // Subtle, refined glassmorphic palette
        // Empty cells: ultra-subtle 7-10% border sheen
        var emptyPen = new Pen(new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), 0.85);

        // Specular highlight brush for glass sheen effect
        var glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)), 0.7);

        int cellIndex = 0;
        for (int r = 0; r < rows; r++)
        {
            double y = startY + r * (cell + gap);
            for (int c = 0; c < cols; c++)
            {
                double x = startX + c * (cell + gap);
                var rect = new Rect(x, y, cell, cell);
                var rrect = new RoundedRect(rect, radius);

                // Horizontal gradient position across the full card width
                double horizontalRatio = (x + cell * 0.5) / bounds.Width;
                var baseColor = GetLiquidGradientColor(horizontalRatio);

                if (cellIndex < filledCount)
                {
                    // Fully filled glass cell with horizontal liquid gradient
                    double pulseAlpha = 1.0;
                    if (IsActive)
                    {
                        // Subtle traveling liquid shimmer wave
                        double wave = Math.Sin(_shimmerPhase + (c * 0.25) - (r * 0.15));
                        pulseAlpha = 0.85 + (wave * 0.25);
                    }

                    byte fillAlpha = (byte)Math.Clamp(34 * pulseAlpha, 18, 55);
                    byte borderAlpha = (byte)Math.Clamp(70 * pulseAlpha, 35, 100);

                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.85);

                    context.DrawRectangle(fillBrush, borderPen, rrect);

                    // Top glass specular sheen line
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cell - radius, y + 0.8));
                }
                else if (cellIndex == filledCount && fractionalCell > 0.05)
                {
                    // Smoothly transitioning boundary cell
                    byte fillAlpha = (byte)Math.Clamp(34 * fractionalCell, 6, 45);
                    byte borderAlpha = (byte)Math.Clamp(65 * fractionalCell, 15, 80);

                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.85);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                }
                else
                {
                    // Unfilled subtle frosted glass cell (border only)
                    context.DrawRectangle(null, emptyPen, rrect);
                }

                cellIndex++;
            }
        }
    }

    /// <summary>
    /// Evaluates sweeping horizontal liquid gradient: Cyan -> Sky Blue -> Vivid Royal -> Indigo/Purple
    /// </summary>
    private static Color GetLiquidGradientColor(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        // Gradient Stops:
        // Stop 0.0: Electric Cyan (#06B6D4 -> RGB: 6, 182, 212)
        // Stop 0.5: Vivid Azure Blue (#3B82F6 -> RGB: 59, 130, 246)
        // Stop 1.0: Indigo Purple (#6366F1 -> RGB: 99, 102, 241)

        if (t <= 0.5)
        {
            double k = t / 0.5;
            byte r = (byte)(6 + (59 - 6) * k);
            byte g = (byte)(182 + (130 - 182) * k);
            byte b = (byte)(212 + (246 - 212) * k);
            return Color.FromRgb(r, g, b);
        }
        else
        {
            double k = (t - 0.5) / 0.5;
            byte r = (byte)(59 + (99 - 59) * k);
            byte g = (byte)(130 + (102 - 130) * k);
            byte b = (byte)(246 + (241 - 246) * k);
            return Color.FromRgb(r, g, b);
        }
    }
}

