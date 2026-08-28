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

    public static readonly StyledProperty<string?> PaletteSeedProperty =
        AvaloniaProperty.Register<BlockProgressBackground, string?>(nameof(PaletteSeed));

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
    private double _shimmerPhase = 0.0;
    private double _windPhase = 0.0;
    private readonly DispatcherTimer _animTimer;

    // 6 distinct, vibrant gradient palettes
    private static readonly (Color Stop0, Color Stop1, Color Stop2)[] Palettes = new[]
    {
        // 0: Cyber Cyan -> Azure -> Indigo
        (Color.FromRgb(6, 182, 212), Color.FromRgb(59, 130, 246), Color.FromRgb(99, 102, 241)),
        // 1: Neon Emerald -> Mint -> Cyan
        (Color.FromRgb(16, 185, 129), Color.FromRgb(20, 184, 166), Color.FromRgb(6, 182, 212)),
        // 2: Sunset Amber -> Tangerine -> Rose
        (Color.FromRgb(245, 158, 11), Color.FromRgb(249, 115, 22), Color.FromRgb(244, 63, 94)),
        // 3: Electric Violet -> Magenta -> Hot Pink
        (Color.FromRgb(139, 92, 246), Color.FromRgb(217, 70, 239), Color.FromRgb(236, 72, 153)),
        // 4: Hyper Lime -> Green -> Teal
        (Color.FromRgb(132, 204, 22), Color.FromRgb(16, 185, 129), Color.FromRgb(6, 182, 212)),
        // 5: Sapphire Blue -> Sky -> Violet
        (Color.FromRgb(37, 99, 235), Color.FromRgb(56, 189, 248), Color.FromRgb(139, 92, 246)),
    };

    static BlockProgressBackground()
    {
        AffectsRender<BlockProgressBackground>(
            BoundsProperty,
            ProgressProperty,
            IsActiveProperty,
            IsConnectingProperty,
            IsCompletedProperty,
            PaletteSeedProperty,
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

    public string? PaletteSeed
    {
        get => GetValue(PaletteSeedProperty);
        set => SetValue(PaletteSeedProperty, value);
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

        // Smooth progress interpolation without resetting
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

        // Active download traveling shimmer wave
        if (IsActive && !IsConnecting)
        {
            _shimmerPhase = (_shimmerPhase + 0.06) % (Math.PI * 2.0);
            needsRedraw = true;
        }

        // Connecting mode passing wind wave
        if (IsConnecting)
        {
            _windPhase = (_windPhase + 0.08) % (Math.PI * 2.0);
            needsRedraw = true;
        }

        if (needsRedraw)
        {
            InvalidateVisual();
        }
        else if (!IsActive && !IsConnecting && Math.Abs(_animatedProgress - target) <= 0.05)
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

        // Pick palette based on PaletteSeed hash
        int paletteIndex = 0;
        if (!string.IsNullOrEmpty(PaletteSeed))
        {
            paletteIndex = Math.Abs(PaletteSeed.GetHashCode()) % Palettes.Length;
        }
        var currentPalette = Palettes[paletteIndex];

        // Clear, crisp unfilled cell border (well-visible glass grid matrix)
        var emptyPen = new Pen(new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), 0.85);

        // Specular highlight brush for glass sheen effect
        var glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 0.7);

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
                var baseColor = GetPaletteGradientColor(currentPalette, horizontalRatio);

                if (IsConnecting)
                {
                    // Luminous wind wave passing across unfilled cells during connecting mode
                    double waveDist = Math.Sin(_windPhase - (c * 0.22) + (r * 0.08));
                    if (waveDist > 0)
                    {
                        byte windAlpha = (byte)Math.Clamp(waveDist * 40, 0, 40);
                        byte windBorderAlpha = (byte)Math.Clamp(38 + (waveDist * 80), 38, 120);

                        var windFillBrush = new SolidColorBrush(Color.FromArgb(windAlpha, baseColor.R, baseColor.G, baseColor.B));
                        var windBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(windBorderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                        context.DrawRectangle(windFillBrush, windBorderPen, rrect);
                    }
                    else
                    {
                        context.DrawRectangle(null, emptyPen, rrect);
                    }
                }
                else if (cellIndex < filledCount)
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
                    byte borderAlpha = (byte)Math.Clamp(75 * pulseAlpha, 40, 110);

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
                    byte borderAlpha = (byte)Math.Clamp(70 * fractionalCell, 20, 85);

                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.85);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                }
                else
                {
                    // Unfilled visible frosted glass cell (border only)
                    context.DrawRectangle(null, emptyPen, rrect);
                }

                cellIndex++;
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

