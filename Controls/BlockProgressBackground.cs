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

    public static readonly StyledProperty<bool> IsConnectingProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsConnecting), false);

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsIndeterminate), false);

    public static readonly StyledProperty<bool> IsCompletedProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsCompleted), false);

    public static readonly StyledProperty<int> PaletteIndexProperty =
        AvaloniaProperty.Register<BlockProgressBackground, int>(nameof(PaletteIndex), 0);

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellSize), 8.0);

    public static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellGap), 2.5);

    public static readonly StyledProperty<double> CellCornerRadiusProperty =
        AvaloniaProperty.Register<BlockProgressBackground, double>(nameof(CellCornerRadius), 1.5);

    public static readonly StyledProperty<bool> IsDarkModeProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsDarkMode), true);

    private double _animatedProgress = 0.0;
    private bool _isInitialized = false;
    private double _animPhase = 0.0;
    private readonly DispatcherTimer _animTimer;

    // 10 distinct, vibrant gradient palettes
    private static readonly (Color Stop0, Color Stop1, Color Stop2)[] Palettes = new[]
    {
        // 0: Cyber Cyan -> Vivid Blue -> Purple
        (Color.FromRgb(0, 210, 255), Color.FromRgb(59, 130, 246), Color.FromRgb(147, 51, 234)),
        // 1: Neon Emerald -> Teal -> Electric Mint
        (Color.FromRgb(16, 185, 129), Color.FromRgb(20, 184, 166), Color.FromRgb(52, 211, 153)),
        // 2: Sunset Amber -> Fire Orange -> Crimson Rose
        (Color.FromRgb(245, 158, 11), Color.FromRgb(249, 115, 22), Color.FromRgb(244, 63, 94)),
        // 3: Ultraviolet -> Magenta -> Neon Pink
        (Color.FromRgb(139, 92, 246), Color.FromRgb(217, 70, 239), Color.FromRgb(244, 114, 182)),
        // 4: Electric Lime -> Mint -> Seafoam
        (Color.FromRgb(132, 204, 22), Color.FromRgb(16, 185, 129), Color.FromRgb(6, 182, 212)),
        // 5: Sapphire Blue -> Azure Sky -> Iris
        (Color.FromRgb(37, 99, 235), Color.FromRgb(56, 189, 248), Color.FromRgb(129, 140, 248)),
        // 6: Crimson Flame -> Coral -> Golden Sun
        (Color.FromRgb(239, 68, 68), Color.FromRgb(251, 146, 60), Color.FromRgb(250, 204, 21)),
        // 7: Aurora Aqua -> Turquoise -> Deep Ocean
        (Color.FromRgb(45, 212, 191), Color.FromRgb(14, 165, 233), Color.FromRgb(99, 102, 241)),
        // 8: Cyber Fuchsia -> Purple -> Midnight Blue
        (Color.FromRgb(236, 72, 153), Color.FromRgb(168, 85, 247), Color.FromRgb(59, 130, 246)),
        // 9: Peach Blossom -> Rose -> Violet
        (Color.FromRgb(251, 113, 133), Color.FromRgb(244, 63, 94), Color.FromRgb(192, 132, 252)),
    };

    static BlockProgressBackground()
    {
        AffectsRender<BlockProgressBackground>(
            BoundsProperty,
            ProgressProperty,
            IsActiveProperty,
            IsConnectingProperty,
            IsIndeterminateProperty,
            IsCompletedProperty,
            PaletteIndexProperty,
            CellSizeProperty,
            CellGapProperty,
            CellCornerRadiusProperty,
            IsDarkModeProperty);
    }

    public BlockProgressBackground()
    {
        _animTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
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

    public bool IsConnecting
    {
        get => GetValue(IsConnectingProperty);
        set => SetValue(IsConnectingProperty, value);
    }

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public bool IsCompleted
    {
        get => GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public int PaletteIndex
    {
        get => GetValue(PaletteIndexProperty);
        set => SetValue(PaletteIndexProperty, value);
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

        if (change.Property == ProgressProperty)
        {
            if (!_isInitialized)
            {
                _animatedProgress = Math.Clamp(Progress, 0.0, 100.0);
                _isInitialized = true;
            }
            EnsureAnimationRunning();
        }
        else if (change.Property == IsActiveProperty || change.Property == IsConnectingProperty || change.Property == IsIndeterminateProperty)
        {
            EnsureAnimationRunning();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_isInitialized)
        {
            _animatedProgress = Math.Clamp(Progress, 0.0, 100.0);
            _isInitialized = true;
        }
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

        // Smooth continuous 60fps liquid damping interpolation
        double target = Math.Clamp(Progress, 0.0, 100.0);
        double diff = target - _animatedProgress;

        if (Math.Abs(diff) > 0.01)
        {
            double step = diff * 0.14;
            if (Math.Abs(step) < 0.004) step = Math.Sign(diff) * 0.004;
            _animatedProgress += step;
            needsRedraw = true;
        }
        else if (_animatedProgress != target)
        {
            _animatedProgress = target;
            needsRedraw = true;
        }

        // Connecting wave or indeterminate live stream animation phase
        if (IsConnecting || IsIndeterminate)
        {
            _animPhase = (_animPhase + 0.07) % (Math.PI * 2.0);
            needsRedraw = true;
        }

        if (needsRedraw)
        {
            InvalidateVisual();
        }
        else if (!IsConnecting && !IsIndeterminate && Math.Abs(_animatedProgress - target) <= 0.01)
        {
            _animTimer.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1) return;

        double gap = CellGap >= 0 ? CellGap : 2.5;
        double nominalSize = CellSize > 0 ? CellSize : 8.0;
        double nominalStep = nominalSize + gap;
        double radius = CellCornerRadius >= 0 ? CellCornerRadius : 1.5;

        // Dynamically compute exact integer columns and rows to seamlessly span 100% of bounds
        int cols = Math.Max(1, (int)Math.Round((bounds.Width + gap) / nominalStep));
        int rows = Math.Max(1, (int)Math.Round((bounds.Height + gap) / nominalStep));

        double stepX = bounds.Width / cols;
        double stepY = bounds.Height / rows;

        double cellW = Math.Max(2.0, stepX - gap);
        double cellH = Math.Max(2.0, stepY - gap);

        int totalCells = cols * rows;

        // Fill cell-by-cell sequentially to the right row by row based on percentage
        double pct = Math.Clamp(_animatedProgress, 0.0, 100.0);
        double exactFilled = (pct / 100.0) * totalCells;
        int filledCount = (int)Math.Floor(exactFilled);
        double fractionalCell = exactFilled - filledCount;

        // Select palette cleanly from PaletteIndex
        int palIdx = Math.Abs(PaletteIndex) % Palettes.Length;
        var currentPalette = Palettes[palIdx];

        // Theme-aware rendering: transparent, non-distracting unfilled matrix
        bool isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;

        IBrush emptyFillBrush;
        Pen emptyBorderPen;
        Pen glassHighlightPen;
        byte fillBaseAlpha;
        byte borderBaseAlpha;
        byte baseUnfilledBorderAlpha;
        byte baseUnfilledFillAlpha;

        if (isLight)
        {
            // Light Mode: subtle, transparent outline
            baseUnfilledFillAlpha = 4;
            baseUnfilledBorderAlpha = 16;
            emptyFillBrush = new SolidColorBrush(Color.FromArgb(baseUnfilledFillAlpha, 15, 23, 42));
            emptyBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(baseUnfilledBorderAlpha, 15, 23, 42)), 0.85);
            glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 0.7);
            fillBaseAlpha = 24;
            borderBaseAlpha = 60;
        }
        else
        {
            // Dark Mode: soft, ultra-translucent cyber-glass matrix
            baseUnfilledFillAlpha = 4;
            baseUnfilledBorderAlpha = 18;
            emptyFillBrush = new SolidColorBrush(Color.FromArgb(baseUnfilledFillAlpha, 255, 255, 255));
            emptyBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(baseUnfilledBorderAlpha, 255, 255, 255)), 0.85);
            glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), 0.7);
            fillBaseAlpha = 28;
            borderBaseAlpha = 68;
        }

        int cellIndex = 0;
        for (int r = 0; r < rows; r++)
        {
            double y = (r * stepY) + (gap * 0.5);
            for (int c = 0; c < cols; c++)
            {
                double x = (c * stepX) + (gap * 0.5);
                var rect = new Rect(x, y, cellW, cellH);
                var rrect = new RoundedRect(rect, radius);

                double horizontalRatio = (x + cellW * 0.5) / bounds.Width;
                var baseColor = GetPaletteGradientColor(currentPalette, horizontalRatio);

                if (IsIndeterminate)
                {
                    // Live stream indeterminate wave animation in assigned palette colors
                    double wave = Math.Sin(_animPhase - (c * 0.16));
                    double waveFactor = Math.Clamp(0.5 + (wave * 0.5), 0.0, 1.0);

                    byte waveFillAlpha = (byte)(baseUnfilledFillAlpha + (waveFactor * 22));
                    byte waveBorderAlpha = (byte)(baseUnfilledBorderAlpha + (waveFactor * 52));

                    var waveFill = new SolidColorBrush(Color.FromArgb(waveFillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(waveBorderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(waveFill, wavePen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                }
                else if (cellIndex < filledCount)
                {
                    // Fully filled cell with liquid gradient
                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillBaseAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderBaseAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                }
                else if (cellIndex == filledCount && fractionalCell > 0.01)
                {
                    // Silky liquid leading cell transition
                    byte fillAlpha = (byte)Math.Clamp(fillBaseAlpha * fractionalCell, baseUnfilledFillAlpha, fillBaseAlpha);
                    byte borderAlpha = (byte)Math.Clamp(baseUnfilledBorderAlpha + ((borderBaseAlpha - baseUnfilledBorderAlpha) * fractionalCell), baseUnfilledBorderAlpha, borderBaseAlpha);

                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + (cellW * fractionalCell) - radius, y + 0.8));
                }
                else
                {
                    // Unfilled cell
                    if (IsConnecting)
                    {
                        // Faster, ultra-light and transparent gentle wave across unfilled cells during connecting mode
                        double wave = Math.Sin(_animPhase - (c * 0.14));
                        double waveFactor = Math.Clamp(0.5 + (wave * 0.5), 0.0, 1.0);

                        byte waveFillAlpha = (byte)(baseUnfilledFillAlpha + (waveFactor * 7));
                        byte waveBorderAlpha = (byte)(baseUnfilledBorderAlpha + (waveFactor * 22));

                        var waveFill = new SolidColorBrush(Color.FromArgb(waveFillAlpha, baseColor.R, baseColor.G, baseColor.B));
                        var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(waveBorderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                        context.DrawRectangle(waveFill, wavePen, rrect);
                    }
                    else
                    {
                        // Clean, soft translucent unfilled cell
                        context.DrawRectangle(emptyFillBrush, emptyBorderPen, rrect);
                        context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                    }
                }

                cellIndex++;
            }
        }
    }

    /// <summary>
    /// Evaluates smooth horizontal 3-stop liquid gradient with cosine ease blending
    /// </summary>
    private static Color GetPaletteGradientColor((Color Stop0, Color Stop1, Color Stop2) pal, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        if (t <= 0.5)
        {
            double rawK = t / 0.5;
            double k = (1.0 - Math.Cos(rawK * Math.PI)) / 2.0;
            byte r = (byte)(pal.Stop0.R + (pal.Stop1.R - pal.Stop0.R) * k);
            byte g = (byte)(pal.Stop0.G + (pal.Stop1.G - pal.Stop0.G) * k);
            byte b = (byte)(pal.Stop0.B + (pal.Stop1.B - pal.Stop0.B) * k);
            return Color.FromRgb(r, g, b);
        }
        else
        {
            double rawK = (t - 0.5) / 0.5;
            double k = (1.0 - Math.Cos(rawK * Math.PI)) / 2.0;
            byte r = (byte)(pal.Stop1.R + (pal.Stop2.R - pal.Stop1.R) * k);
            byte g = (byte)(pal.Stop1.G + (pal.Stop2.G - pal.Stop1.G) * k);
            byte b = (byte)(pal.Stop1.B + (pal.Stop2.B - pal.Stop1.B) * k);
            return Color.FromRgb(r, g, b);
        }
    }
}

