using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DownloaderApp.Models;

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

    public static readonly StyledProperty<bool> IsFailedProperty =
        AvaloniaProperty.Register<BlockProgressBackground, bool>(nameof(IsFailed), false);

    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<BlockProgressBackground, string>(nameof(ErrorMessage), string.Empty);

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

    public static readonly StyledProperty<IEnumerable?> SegmentsProperty =
        AvaloniaProperty.Register<BlockProgressBackground, IEnumerable?>(nameof(Segments), null);

    private double _animatedProgress = 0.0;
    private bool _isInitialized = false;
    private double _animPhase = 0.0;
    private readonly DispatcherTimer _animTimer;

    static BlockProgressBackground()
    {
        AffectsRender<BlockProgressBackground>(
            BoundsProperty,
            ProgressProperty,
            IsActiveProperty,
            IsConnectingProperty,
            IsIndeterminateProperty,
            IsCompletedProperty,
            IsFailedProperty,
            ErrorMessageProperty,
            PaletteIndexProperty,
            CellSizeProperty,
            CellGapProperty,
            CellCornerRadiusProperty,
            IsDarkModeProperty,
            SegmentsProperty);
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

    public bool IsFailed
    {
        get => GetValue(IsFailedProperty);
        set => SetValue(IsFailedProperty, value);
    }

    public string ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
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

    public IEnumerable? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
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
            EnsureTimerRunning();
        }
        else if (change.Property == IsActiveProperty ||
                 change.Property == IsConnectingProperty ||
                 change.Property == IsIndeterminateProperty ||
                 change.Property == IsFailedProperty ||
                 change.Property == SegmentsProperty)
        {
            EnsureTimerRunning();
        }
    }

    private void EnsureTimerRunning()
    {
        if (IsActive || IsConnecting || IsIndeterminate || IsFailed || Math.Abs(_animatedProgress - Progress) > 0.05)
        {
            if (!_animTimer.IsEnabled)
            {
                _animTimer.Start();
            }
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        bool needsRedraw = false;
        double target = Math.Clamp(Progress, 0.0, 100.0);

        if (Math.Abs(_animatedProgress - target) > 0.01)
        {
            double diff = target - _animatedProgress;
            double step = diff * 0.12;
            if (Math.Abs(step) < 0.05)
            {
                step = Math.Sign(diff) * Math.Min(Math.Abs(diff), 0.05);
            }
            _animatedProgress += step;
            needsRedraw = true;
        }
        else
        {
            _animatedProgress = target;
        }

        if (IsActive || IsConnecting || IsIndeterminate || IsFailed)
        {
            _animPhase += 0.06;
            if (_animPhase > Math.PI * 2000.0) _animPhase -= Math.PI * 2000.0;
            needsRedraw = true;
        }

        if (needsRedraw)
        {
            InvalidateVisual();
        }
        else if (!IsConnecting && !IsIndeterminate && !IsActive && !IsFailed && Math.Abs(_animatedProgress - target) <= 0.01)
        {
            _animTimer.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1) return;

        bool isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;

        // When download failed, render stylish abstract cyber error artwork
        if (IsFailed)
        {
            RenderFailedArtwork(context, bounds, isLight);
            return;
        }

        double gap = CellGap >= 0 ? CellGap : 2.5;
        double nominalSize = CellSize > 0 ? CellSize : 8.0;
        double nominalStep = nominalSize + gap;
        double radius = CellCornerRadius >= 0 ? CellCornerRadius : 1.5;

        int cols = Math.Max(1, (int)Math.Round((bounds.Width + gap) / nominalStep));
        int rows = Math.Max(1, (int)Math.Round((bounds.Height + gap) / nominalStep));

        double stepX = bounds.Width / cols;
        double stepY = bounds.Height / rows;

        double cellW = Math.Max(2.0, stepX - gap);
        double cellH = Math.Max(2.0, stepY - gap);

        int totalCells = cols * rows;

        var currentPalette = GetHarmonicPalette(PaletteIndex);

        IBrush emptyFillBrush;
        Pen emptyBorderPen;
        Pen glassHighlightPen;
        byte fillBaseAlpha;
        byte borderBaseAlpha;
        byte baseUnfilledBorderAlpha;
        byte baseUnfilledFillAlpha;

        if (isLight)
        {
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
            baseUnfilledFillAlpha = 4;
            baseUnfilledBorderAlpha = 18;
            emptyFillBrush = new SolidColorBrush(Color.FromArgb(baseUnfilledFillAlpha, 255, 255, 255));
            emptyBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(baseUnfilledBorderAlpha, 255, 255, 255)), 0.85);
            glassHighlightPen = new Pen(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), 0.7);
            fillBaseAlpha = 28;
            borderBaseAlpha = 68;
        }

        // Check if we have active thread segments
        List<DownloadSegment>? segmentList = null;
        if (Segments != null)
        {
            segmentList = Segments.OfType<DownloadSegment>().ToList();
            if (segmentList.Count < 2) segmentList = null;
        }

        long totalFileBytes = 0;
        if (segmentList != null)
        {
            totalFileBytes = segmentList.Max(s => s.EndByte) + 1;
            if (totalFileBytes <= 0) segmentList = null;
        }

        double pct = Math.Clamp(_animatedProgress, 0.0, 100.0);
        double exactFilledSequential = (pct / 100.0) * totalCells;
        int filledCountSequential = (int)Math.Floor(exactFilledSequential);
        double fractionalCellSequential = exactFilledSequential - filledCountSequential;

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
                    double wave = Math.Sin(_animPhase - (c * 0.16));
                    double waveFactor = Math.Clamp(0.5 + (wave * 0.5), 0.0, 1.0);

                    byte waveFillAlpha = (byte)(baseUnfilledFillAlpha + (waveFactor * 22));
                    byte waveBorderAlpha = (byte)(baseUnfilledBorderAlpha + (waveFactor * 52));

                    var waveFill = new SolidColorBrush(Color.FromArgb(waveFillAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(waveBorderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(waveFill, wavePen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                }
                else if (segmentList != null)
                {
                    // ================= THREAD-RELATIVE CHUNK FILLING =================
                    long cellStartByte = (long)((double)cellIndex / totalCells * totalFileBytes);
                    long cellEndByte = (long)((double)(cellIndex + 1) / totalCells * totalFileBytes) - 1;

                    var ownerSeg = segmentList.FirstOrDefault(s => cellStartByte >= s.StartByte && cellStartByte <= s.EndByte);
                    if (ownerSeg == null && segmentList.Count > 0)
                    {
                        ownerSeg = segmentList.Last();
                    }

                    if (ownerSeg != null)
                    {
                        long segCurrentOffset = ownerSeg.CurrentOffset;

                        if (segCurrentOffset >= cellEndByte || ownerSeg.IsCompleted)
                        {
                            // Cell is fully handled by this thread
                            var fillBrush = new SolidColorBrush(Color.FromArgb(fillBaseAlpha, baseColor.R, baseColor.G, baseColor.B));
                            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderBaseAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                            context.DrawRectangle(fillBrush, borderPen, rrect);
                            context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                        }
                        else if (segCurrentOffset >= cellStartByte)
                        {
                            // Cell is the active write head of this thread
                            double cellSpan = Math.Max(1.0, cellEndByte - cellStartByte + 1);
                            double frac = Math.Clamp((double)(segCurrentOffset - cellStartByte) / cellSpan, 0.0, 1.0);

                            // Active head pulse glow with luminous harmonic highlight
                            double pulse = Math.Sin(_animPhase * 2.0);
                            byte pulseBonus = (byte)(Math.Max(0, pulse) * 22);

                            byte fillAlpha = (byte)Math.Clamp(fillBaseAlpha * frac + pulseBonus, baseUnfilledFillAlpha, (byte)255);
                            byte borderAlpha = (byte)Math.Clamp(borderBaseAlpha + pulseBonus * 2, baseUnfilledBorderAlpha, (byte)255);

                            var fillBrush = new SolidColorBrush(Color.FromArgb(fillAlpha, currentPalette.Highlight.R, currentPalette.Highlight.G, currentPalette.Highlight.B));
                            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, currentPalette.Highlight.R, currentPalette.Highlight.G, currentPalette.Highlight.B)), 1.15);

                            context.DrawRectangle(fillBrush, borderPen, rrect);
                            context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                        }
                        else
                        {
                            // Unfilled cell waiting for this thread
                            context.DrawRectangle(emptyFillBrush, emptyBorderPen, rrect);
                            context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                        }
                    }
                    else
                    {
                        context.DrawRectangle(emptyFillBrush, emptyBorderPen, rrect);
                    }
                }
                else if (cellIndex < filledCountSequential)
                {
                    // Sequential mode: Fully filled
                    var fillBrush = new SolidColorBrush(Color.FromArgb(fillBaseAlpha, baseColor.R, baseColor.G, baseColor.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(borderBaseAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                }
                else if (cellIndex == filledCountSequential && fractionalCellSequential > 0.01)
                {
                    // Sequential mode: Partially filled active head
                    byte fracFillAlpha = (byte)Math.Clamp(fillBaseAlpha * fractionalCellSequential, baseUnfilledFillAlpha, (byte)255);
                    byte fracBorderAlpha = (byte)Math.Clamp(borderBaseAlpha * fractionalCellSequential, baseUnfilledBorderAlpha, (byte)255);

                    if (IsActive || IsConnecting)
                    {
                        double pulse = Math.Sin(_animPhase * 2.0);
                        byte pulseBonus = (byte)(Math.Max(0, pulse) * 16);
                        fracFillAlpha = (byte)Math.Clamp(fracFillAlpha + pulseBonus, (byte)0, (byte)255);
                        fracBorderAlpha = (byte)Math.Clamp(fracBorderAlpha + pulseBonus, (byte)0, (byte)255);
                    }

                    var fillBrush = new SolidColorBrush(Color.FromArgb(fracFillAlpha, currentPalette.Highlight.R, currentPalette.Highlight.G, currentPalette.Highlight.B));
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(fracBorderAlpha, currentPalette.Highlight.R, currentPalette.Highlight.G, currentPalette.Highlight.B)), 1.1);

                    context.DrawRectangle(fillBrush, borderPen, rrect);
                    context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                }
                else
                {
                    // Empty cell
                    if (IsConnecting)
                    {
                        double wave = Math.Sin(_animPhase - (c * 0.12));
                        double waveFactor = Math.Clamp(0.5 + (wave * 0.5), 0.0, 1.0);

                        byte waveFillAlpha = (byte)(baseUnfilledFillAlpha + (waveFactor * 10));
                        byte waveBorderAlpha = (byte)(baseUnfilledBorderAlpha + (waveFactor * 22));

                        var waveFill = new SolidColorBrush(Color.FromArgb(waveFillAlpha, baseColor.R, baseColor.G, baseColor.B));
                        var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(waveBorderAlpha, baseColor.R, baseColor.G, baseColor.B)), 0.9);

                        context.DrawRectangle(waveFill, wavePen, rrect);
                    }
                    else
                    {
                        context.DrawRectangle(emptyFillBrush, emptyBorderPen, rrect);
                        context.DrawLine(glassHighlightPen, new Point(x + radius, y + 0.8), new Point(x + cellW - radius, y + 0.8));
                    }
                }

                cellIndex++;
            }
        }
    }

    /// <summary>
    /// Procedurally generates infinite, mathematically harmonious 3-color gradient combos with glowing highlight.
    /// Uses golden ratio hue dispersion and aesthetic color harmony archetypes.
    /// </summary>
    public static (Color Stop0, Color Stop1, Color Stop2, Color Highlight) GetHarmonicPalette(int seed)
    {
        uint uSeed = (uint)seed ^ 0x9E3779B9u;
        double baseHue = (uSeed * 137.50776405) % 360.0;
        int schemeType = (int)(uSeed % 5);

        double hue1, hue2;
        switch (schemeType)
        {
            case 0: // Golden Analogous Drift (Smooth Aurora)
                hue1 = (baseHue + 32.0 + (uSeed % 12)) % 360.0;
                hue2 = (hue1 + 38.0 + ((uSeed >> 4) % 15)) % 360.0;
                break;
            case 1: // Split-Complementary Electric (Neon Glow)
                hue1 = (baseHue + 55.0) % 360.0;
                hue2 = (baseHue + 140.0 + (uSeed % 20)) % 360.0;
                break;
            case 2: // Triadic Luminescence (Balanced Rich Spectrum)
                hue1 = (baseHue + 45.0 + (uSeed % 10)) % 360.0;
                hue2 = (baseHue + 95.0 + (uSeed % 15)) % 360.0;
                break;
            case 3: // High-Contrast Neon Flow (Cyan/Lime/Magenta)
                hue1 = (baseHue + 50.0) % 360.0;
                hue2 = (baseHue + 115.0) % 360.0;
                break;
            default: // Jewel Twilight
                hue1 = (baseHue + 28.0) % 360.0;
                hue2 = (baseHue + 72.0) % 360.0;
                break;
        }

        var stop0 = HslToRgb(baseHue, 0.94, 0.54);
        var stop1 = HslToRgb(hue1, 0.92, 0.50);
        var stop2 = HslToRgb(hue2, 0.95, 0.48);
        var highlight = HslToRgb(baseHue, 0.98, 0.76);

        return (stop0, stop1, stop2, highlight);
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        h = (h % 360.0 + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        l = Math.Clamp(l, 0.0, 1.0);

        double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        double m = l - c / 2.0;

        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255.0),
            (byte)Math.Round((g + m) * 255.0),
            (byte)Math.Round((b + m) * 255.0));
    }

    private static Color GetPaletteGradientColor((Color Stop0, Color Stop1, Color Stop2, Color Highlight) pal, double t)
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

    private void RenderFailedArtwork(DrawingContext context, Rect bounds, bool isLight)
    {
        // 1. Subtle glowing crimson ambient background
        Color bgStart = isLight ? Color.FromArgb(20, 239, 68, 68) : Color.FromArgb(32, 220, 38, 38);
        Color bgEnd = isLight ? Color.FromArgb(8, 245, 158, 11) : Color.FromArgb(14, 15, 23, 42);
        var bgBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(bgStart, 0.0),
                new GradientStop(bgEnd, 1.0)
            }
        };
        context.FillRectangle(bgBrush, bounds, 6);

        // 2. Abstract isometric geometric grid & contour ribbons
        var gridPen = new Pen(new SolidColorBrush(isLight ? Color.FromArgb(18, 239, 68, 68) : Color.FromArgb(24, 248, 113, 113)), 0.75);
        var accentPen = new Pen(new SolidColorBrush(isLight ? Color.FromArgb(35, 245, 158, 11) : Color.FromArgb(45, 245, 158, 11)), 1.0);
        var emberPen = new Pen(new SolidColorBrush(isLight ? Color.FromArgb(45, 239, 68, 68) : Color.FromArgb(60, 239, 68, 68)), 1.25);

        // Draw abstract isometric diagonal grid waves
        double spacing = 18.0;
        for (double x = -bounds.Height; x < bounds.Width + bounds.Height; x += spacing)
        {
            context.DrawLine(gridPen, new Point(x, bounds.Height), new Point(x + bounds.Height * 0.7, 0));
        }

        // Draw flowing abstract sinus contour wave ribbons across the card
        var waveGeometry = new StreamGeometry();
        using (var ctx = waveGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, bounds.Height * 0.7), false);
            for (double wx = 0; wx <= bounds.Width; wx += 20)
            {
                double wy = bounds.Height * 0.7 + Math.Sin((wx / bounds.Width) * Math.PI * 3.0 + 0.5) * (bounds.Height * 0.22);
                ctx.LineTo(new Point(wx, wy));
            }
        }
        context.DrawGeometry(null, accentPen, waveGeometry);

        var waveGeometry2 = new StreamGeometry();
        using (var ctx2 = waveGeometry2.Open())
        {
            ctx2.BeginFigure(new Point(0, bounds.Height * 0.4), false);
            for (double wx = 0; wx <= bounds.Width; wx += 20)
            {
                double wy = bounds.Height * 0.4 + Math.Cos((wx / bounds.Width) * Math.PI * 2.5 + 1.2) * (bounds.Height * 0.28);
                ctx2.LineTo(new Point(wx, wy));
            }
        }
        context.DrawGeometry(null, emberPen, waveGeometry2);

        // 3. Subtle floating geometric ember diamond pips
        int diamondCount = Math.Max(2, (int)(bounds.Width / 80));
        var diamondBrush = new SolidColorBrush(isLight ? Color.FromArgb(40, 239, 68, 68) : Color.FromArgb(50, 239, 68, 68));
        for (int i = 0; i < diamondCount; i++)
        {
            double dx = 30 + i * (bounds.Width / diamondCount) + Math.Sin(i * 1.7) * 15;
            double dy = bounds.Height * 0.35 + Math.Cos(i * 2.1) * (bounds.Height * 0.25);
            
            var diamondGeom = new StreamGeometry();
            using (var dctx = diamondGeom.Open())
            {
                dctx.BeginFigure(new Point(dx, dy - 4), true);
                dctx.LineTo(new Point(dx + 4, dy));
                dctx.LineTo(new Point(dx, dy + 4));
                dctx.LineTo(new Point(dx - 4, dy));
            }
            context.DrawGeometry(diamondBrush, null, diamondGeom);
        }

        // 4. Subtle glowing bottom hazard ribbon bar
        var hazardBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(90, 239, 68, 68), 0.0),
                new GradientStop(Color.FromArgb(70, 245, 158, 11), 0.5),
                new GradientStop(Color.FromArgb(90, 239, 68, 68), 1.0)
            }
        };
        context.FillRectangle(hazardBrush, new Rect(0, bounds.Height - 2, bounds.Width, 2));
    }
}
