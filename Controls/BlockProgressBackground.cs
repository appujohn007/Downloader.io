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

    public bool IsConnecting
    {
        get => GetValue(IsConnectingProperty);
        set => SetValue(IsConnectingProperty, value);
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
        else if (change.Property == IsActiveProperty || change.Property == IsConnectingProperty)
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

        // Smooth continuous progress interpolation without resetting
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

        // Connecting wave animation phase
        if (IsConnecting)
        {
            _animPhase = (_animPhase + 0.06) % (Math.PI * 2.0);
            needsRedraw = true;
        }

        if (needsRedraw)
        {
            InvalidateVisual();
        }
        else if (!IsConnecting && Math.Abs(_animatedProgress - target) <= 0.05)
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

        double actualGridWidth = (cols * cell) + ((cols - 1) * gap);
        double actualGridHeight = (rows * cell) + ((rows - 1) * gap);

        double startX = Math.Max(0, (bounds.Width - actualGridWidth) / 2.0);
        double startY = Math.Max(0, (bounds.Height - actualGridHeight) / 2.0);

        // Column-based progress so ALL rows fill together horizontally (matching the progress bar!)
        double pct = Math.Clamp(_animatedProgress, 0.0, 100.0);
        double exactFilledCols = (pct / 100.0) * cols;
        int fullFilledColCount = (int)Math.Floor(exactFilledCols);
        double fractionalCol = exactFilledCols - fullFilledColCount;

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

        if (isLight)
        {
            // Light Mode: subtle, transparent outline
            emptyFillBrush = new SolidColorBrush(Color.FromArgb(4, 15, 23, 42));
            emptyBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(16, 15, 23, 42)), 0.85);
            glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), 0.7);
            fillBaseAlpha = 26;
            borderBaseAlpha = 65;
        }
        else
        {
            // Dark Mode: soft, ultra-translucent cyber-glass matrix
            emptyFillBrush = new SolidColorBrush(Color.FromArgb(4, 255, 255, 255));
            emptyBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), 0.85);
            glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)), 0.7);
            fillBaseAlpha = 30;
            borderBaseAlpha = 72;
        }

        for (int c = 0; c < cols; c++)
        {
            double x = startX + c * (cell + gap);
            double horizontalRatio = (x + cell * 0.5) / bounds.Width;
            var baseColor = GetPaletteGradientColor(currentPalette, horizontalRatio);

            bool isColFullyFilled = c < fullFilledColCount;
            bool isColFractional = c == fullFilledColCount && fractionalCol > 0.03;

            for (int r = 0; r < rows; r++)
            {
                double y = startY + r * (cell + gap);
                var rect = new Rect(x, y, cell, cell);
                var rrect = new RoundedRect(rect, radius);

                if (isColFullyFilled)
                {
                    // Fully filled column cell across all rows
                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillBaseAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderBaseAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cell - radius, y + 0.8));
                }
                else if (isColFractional)
                {
                    // Smoothly transitioning boundary column
                    byte fillAlpha = (byte)Math.Clamp(fillBaseAlpha * fractionalCol, 6, fillBaseAlpha);
                    byte borderAlpha = (byte)Math.Clamp(borderBaseAlpha * fractionalCol, 18, borderBaseAlpha);

                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                }
                else
                {
                    // Unfilled cell
                    if (IsConnecting)
                    {
                        // Gentle traveling light wave across unfilled cells during connecting mode
                        double wave = Math.Sin(_animPhase - (c * 0.18));
                        double waveFactor = Math.Clamp(0.5 + (wave * 0.5), 0.0, 1.0);

                        byte waveFillAlpha = (byte)Math.Clamp(4 + (waveFactor * 18), 4, 24);
                        byte waveBorderAlpha = (byte)Math.Clamp(18 + (waveFactor * 48), 18, 70);

                        var waveFill = new SolidColorBrush(Color.FromArgb(waveFillAlpha, baseColor.R, baseColor.G, baseColor.B));
                        var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(waveBorderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                        context.DrawRectangle(waveFill, wavePen, rrect);
                    }
                    else
                    {
                        // Clean, soft translucent unfilled cell
                        context.DrawRectangle(emptyFillBrush, emptyBorderPen, rrect);
                        context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cell - radius, y + 0.8));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates smooth horizontal 3-stop liquid gradient using selected card palette
    /// </summary>
    private static Color GetPaletteGradientColor((Color Stop0, Color Stop1, Color Stop2) pal, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        if (t <= 0.5)
        {
            double k = t / 0.5;
            byte r = (byte)(pal.Stop0.R + (pal.Stop1.R - pal.Stop0.R) * k);
            byte g = (byte)(pal.Stop0.G + (pal.Stop1.G - pal.Stop0.G) * k);
            byte b = (byte)(pal.Stop0.B + (pal.Stop1.B - pal.Stop0.B) * k);
            return Color.FromRgb(r, g, b);
        }
        else
        {
            double k = (t - 0.5) / 0.5;
            byte r = (byte)(pal.Stop1.R + (pal.Stop2.R - pal.Stop1.R) * k);
            byte g = (byte)(pal.Stop1.G + (pal.Stop2.G - pal.Stop1.G) * k);
            byte b = (byte)(pal.Stop1.B + (pal.Stop2.B - pal.Stop1.B) * k);
            return Color.FromRgb(r, g, b);
        }
    }
}

