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

    public static readonly StyledProperty<FailureScenario> ScenarioTypeProperty =
        AvaloniaProperty.Register<BlockProgressBackground, FailureScenario>(nameof(ScenarioType), FailureScenario.None);

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
            ScenarioTypeProperty,
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

    public FailureScenario ScenarioType
    {
        get => GetValue(ScenarioTypeProperty);
        set => SetValue(ScenarioTypeProperty, value);
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
        var scenario = ScenarioType;
        if (scenario == FailureScenario.None)
        {
            var err = ErrorMessage ?? string.Empty;
            if (err.Contains("403") || err.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase))
                scenario = FailureScenario.CloudflareChallenge;
            else if (err.Contains("401") || err.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                scenario = FailureScenario.AuthRequired;
            else if (err.Contains("404") || err.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
                scenario = FailureScenario.NotFound;
            else if (err.Contains("429") || err.Contains("Too Many", StringComparison.OrdinalIgnoreCase))
                scenario = FailureScenario.RateLimited;
            else if (err.Contains("timeout", StringComparison.OrdinalIgnoreCase) || err.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                scenario = FailureScenario.Timeout;
            else if (err.Contains("name resolution", StringComparison.OrdinalIgnoreCase) || err.Contains("host", StringComparison.OrdinalIgnoreCase))
                scenario = FailureScenario.DnsUnreachable;
            else
                scenario = FailureScenario.Generic;
        }

        Color colorA, colorB, colorAccent, colorGrid;

        switch (scenario)
        {
            case FailureScenario.CloudflareChallenge:
                // Cloudflare Signature: Sunset Orange + Solar Amber + Ember Crimson
                colorA = isLight ? Color.FromArgb(28, 244, 129, 32) : Color.FromArgb(44, 244, 129, 32);
                colorB = isLight ? Color.FromArgb(18, 239, 68, 68) : Color.FromArgb(30, 220, 38, 38);
                colorAccent = isLight ? Color.FromArgb(160, 244, 129, 32) : Color.FromArgb(220, 250, 173, 63);
                colorGrid = isLight ? Color.FromArgb(24, 244, 129, 32) : Color.FromArgb(28, 244, 129, 32);
                break;

            case FailureScenario.AuthRequired:
                // Security / Auth: Cyber Gold + Burnished Amber
                colorA = isLight ? Color.FromArgb(26, 245, 158, 11) : Color.FromArgb(40, 245, 158, 11);
                colorB = isLight ? Color.FromArgb(14, 217, 119, 6) : Color.FromArgb(28, 180, 83, 9);
                colorAccent = isLight ? Color.FromArgb(160, 245, 158, 11) : Color.FromArgb(220, 251, 191, 36);
                colorGrid = isLight ? Color.FromArgb(22, 245, 158, 11) : Color.FromArgb(28, 245, 158, 11);
                break;

            case FailureScenario.NotFound:
                // 404 / Missing: Neon Indigo + Deep Violet
                colorA = isLight ? Color.FromArgb(26, 99, 102, 241) : Color.FromArgb(40, 99, 102, 241);
                colorB = isLight ? Color.FromArgb(14, 139, 92, 246) : Color.FromArgb(28, 124, 58, 237);
                colorAccent = isLight ? Color.FromArgb(160, 99, 102, 241) : Color.FromArgb(220, 167, 139, 250);
                colorGrid = isLight ? Color.FromArgb(22, 99, 102, 241) : Color.FromArgb(28, 99, 102, 241);
                break;

            case FailureScenario.RateLimited:
            case FailureScenario.Timeout:
                // Rate Limit / Timeout: Tangerine + Rose Red
                colorA = isLight ? Color.FromArgb(26, 251, 146, 60) : Color.FromArgb(40, 251, 146, 60);
                colorB = isLight ? Color.FromArgb(14, 244, 63, 94) : Color.FromArgb(28, 225, 29, 72);
                colorAccent = isLight ? Color.FromArgb(160, 251, 146, 60) : Color.FromArgb(220, 253, 186, 116);
                colorGrid = isLight ? Color.FromArgb(22, 251, 146, 60) : Color.FromArgb(28, 251, 146, 60);
                break;

            case FailureScenario.DnsUnreachable:
                // DNS / Network: Electric Cyan + Alert Crimson
                colorA = isLight ? Color.FromArgb(26, 6, 182, 212) : Color.FromArgb(40, 6, 182, 212);
                colorB = isLight ? Color.FromArgb(14, 239, 68, 68) : Color.FromArgb(28, 220, 38, 38);
                colorAccent = isLight ? Color.FromArgb(160, 6, 182, 212) : Color.FromArgb(220, 103, 232, 249);
                colorGrid = isLight ? Color.FromArgb(22, 6, 182, 212) : Color.FromArgb(28, 6, 182, 212);
                break;

            default:
                // Generic / IO Error: Alert Crimson + Ruby
                colorA = isLight ? Color.FromArgb(26, 239, 68, 68) : Color.FromArgb(40, 239, 68, 68);
                colorB = isLight ? Color.FromArgb(14, 185, 28, 28) : Color.FromArgb(28, 153, 27, 27);
                colorAccent = isLight ? Color.FromArgb(160, 239, 68, 68) : Color.FromArgb(220, 248, 113, 113);
                colorGrid = isLight ? Color.FromArgb(22, 239, 68, 68) : Color.FromArgb(28, 239, 68, 68);
                break;
        }

        // 1. Smooth atmospheric scenario ambient background
        var bgBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(colorA, 0.0),
                new GradientStop(colorB, 1.0)
            }
        };
        context.FillRectangle(bgBrush, bounds, 6);

        // 2. Abstract isometric diagonal cyber-grid waves
        var gridPen = new Pen(new SolidColorBrush(colorGrid), 0.75);
        double spacing = 18.0;
        for (double x = -bounds.Height; x < bounds.Width + bounds.Height; x += spacing)
        {
            context.DrawLine(gridPen, new Point(x, bounds.Height), new Point(x + bounds.Height * 0.75, 0));
        }

        // 3. Flowing dynamic animated sinusoidal wave ribbons
        double wavePhase = _animPhase * 0.7;
        var waveGeometry = new StreamGeometry();
        using (var ctx = waveGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, bounds.Height * 0.75), false);
            for (double wx = 0; wx <= bounds.Width; wx += 20)
            {
                double wy = bounds.Height * 0.72 + Math.Sin((wx / bounds.Width) * Math.PI * 3.0 + wavePhase) * (bounds.Height * 0.22);
                ctx.LineTo(new Point(wx, wy));
            }
        }
        var wavePen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 45 : 70), colorAccent.R, colorAccent.G, colorAccent.B)), 1.25);
        context.DrawGeometry(null, wavePen, waveGeometry);

        var waveGeometry2 = new StreamGeometry();
        using (var ctx2 = waveGeometry2.Open())
        {
            ctx2.BeginFigure(new Point(0, bounds.Height * 0.38), false);
            for (double wx = 0; wx <= bounds.Width; wx += 20)
            {
                double wy = bounds.Height * 0.38 + Math.Cos((wx / bounds.Width) * Math.PI * 2.5 + wavePhase * 1.3) * (bounds.Height * 0.26);
                ctx2.LineTo(new Point(wx, wy));
            }
        }
        var wavePen2 = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 35 : 55), colorB.R, colorB.G, colorB.B)), 1.0);
        context.DrawGeometry(null, wavePen2, waveGeometry2);

        // 4. Subtle floating geometric diamond particle nodes
        int particleCount = Math.Max(3, (int)(bounds.Width / 90));
        var particleBrush = new SolidColorBrush(Color.FromArgb((byte)(isLight ? 50 : 75), colorAccent.R, colorAccent.G, colorAccent.B));
        for (int i = 0; i < particleCount; i++)
        {
            double px = 30 + i * (bounds.Width / particleCount) + Math.Sin(i * 1.7 + wavePhase) * 12;
            double py = bounds.Height * 0.45 + Math.Cos(i * 2.1 + wavePhase * 0.8) * (bounds.Height * 0.25);
            
            var pGeom = new StreamGeometry();
            using (var pctx = pGeom.Open())
            {
                pctx.BeginFigure(new Point(px, py - 3.5), true);
                pctx.LineTo(new Point(px + 3.5, py));
                pctx.LineTo(new Point(px, py + 3.5));
                pctx.LineTo(new Point(px - 3.5, py));
            }
            context.DrawGeometry(particleBrush, null, pGeom);
        }

        // 5. Draw Scenario-Specific Background Symbol Watermark (Positioned behind the right quadrant)
        Point symbolCenter = new Point(Math.Max(120, bounds.Width - 85), bounds.Height * 0.48);
        double symbolScale = Math.Clamp(bounds.Height / 62.0, 0.75, 1.05);

        switch (scenario)
        {
            case FailureScenario.CloudflareChallenge:
                DrawCloudflareShieldSymbol(context, symbolCenter, symbolScale, _animPhase, isLight);
                break;
            case FailureScenario.AuthRequired:
                DrawAuthLockSymbol(context, symbolCenter, symbolScale, _animPhase, isLight);
                break;
            case FailureScenario.NotFound:
                DrawNotFoundSymbol(context, symbolCenter, symbolScale, _animPhase, isLight);
                break;
            case FailureScenario.RateLimited:
            case FailureScenario.Timeout:
                DrawTimeoutSymbol(context, symbolCenter, symbolScale, _animPhase, isLight);
                break;
            case FailureScenario.DnsUnreachable:
                DrawDnsGlobeSymbol(context, symbolCenter, symbolScale, _animPhase, isLight);
                break;
            default:
                DrawGenericHazardSymbol(context, symbolCenter, symbolScale, _animPhase, isLight);
                break;
        }

        // 6. Subtle glowing bottom hazard ribbon bar
        var hazardBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb((byte)(isLight ? 120 : 180), colorAccent.R, colorAccent.G, colorAccent.B), 0.0),
                new GradientStop(Color.FromArgb((byte)(isLight ? 90 : 140), colorB.R, colorB.G, colorB.B), 0.5),
                new GradientStop(Color.FromArgb((byte)(isLight ? 120 : 180), colorAccent.R, colorAccent.G, colorAccent.B), 1.0)
            }
        };
        context.FillRectangle(hazardBrush, new Rect(0, bounds.Height - 2.5, bounds.Width, 2.5));
    }

    private static void DrawCloudflareShieldSymbol(DrawingContext context, Point center, double scale, double animPhase, bool isLight)
    {
        // 1. Animated expanding defense sonar radar rings
        double ringPulse = (animPhase * 0.35) % 1.0;
        for (int r = 0; r < 2; r++)
        {
            double phaseOffset = (ringPulse + r * 0.5) % 1.0;
            double radius = 16.0 * scale + phaseOffset * 34.0 * scale;
            byte alpha = (byte)(Math.Max(0, (1.0 - phaseOffset) * (isLight ? 60 : 90)));
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 244, 129, 32)), 1.0 * scale);
            context.DrawEllipse(null, ringPen, center, radius, radius * 0.72);
        }

        // 2. Cloudflare Cloud silhouette vector outline
        var cloudGeom = new StreamGeometry();
        using (var ctx = cloudGeom.Open())
        {
            double ox = center.X;
            double oy = center.Y + 3 * scale;

            ctx.BeginFigure(new Point(ox - 30 * scale, oy + 8 * scale), true);
            ctx.ArcTo(new Point(ox - 30 * scale, oy - 10 * scale), new Size(12 * scale, 12 * scale), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(ox - 6 * scale, oy - 22 * scale), new Size(16 * scale, 16 * scale), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(ox + 22 * scale, oy - 14 * scale), new Size(20 * scale, 20 * scale), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(ox + 34 * scale, oy + 8 * scale), new Size(14 * scale, 14 * scale), 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(ox - 30 * scale, oy + 8 * scale));
        }

        byte cloudFillAlpha = (byte)(isLight ? 25 : 35);
        byte cloudStrokeAlpha = (byte)(isLight ? 100 : 160);
        var cloudFill = new SolidColorBrush(Color.FromArgb(cloudFillAlpha, 244, 129, 32));
        var cloudPen = new Pen(new SolidColorBrush(Color.FromArgb(cloudStrokeAlpha, 244, 129, 32)), 1.35 * scale);
        context.DrawGeometry(cloudFill, cloudPen, cloudGeom);

        // 3. Central Defense Shield geometry
        var shieldGeom = new StreamGeometry();
        using (var sctx = shieldGeom.Open())
        {
            double sx = center.X;
            double sy = center.Y - 2 * scale;
            double sw = 14 * scale;
            double sh = 18 * scale;

            sctx.BeginFigure(new Point(sx, sy - sh * 0.5), true);
            sctx.LineTo(new Point(sx + sw, sy - sh * 0.35));
            sctx.LineTo(new Point(sx + sw, sy + sh * 0.1));
            sctx.ArcTo(new Point(sx, sy + sh * 0.75), new Size(sw, sh * 0.7), 0, false, SweepDirection.Clockwise);
            sctx.ArcTo(new Point(sx - sw, sy + sh * 0.1), new Size(sw, sh * 0.7), 0, false, SweepDirection.Clockwise);
            sctx.LineTo(new Point(sx - sw, sy - sh * 0.35));
            sctx.LineTo(new Point(sx, sy - sh * 0.5));
        }

        byte shieldFillAlpha = (byte)(isLight ? 60 : 95);
        byte shieldStrokeAlpha = (byte)(isLight ? 190 : 255);
        var shieldFill = new SolidColorBrush(Color.FromArgb(shieldFillAlpha, 239, 68, 68));
        var shieldPen = new Pen(new SolidColorBrush(Color.FromArgb(shieldStrokeAlpha, 245, 158, 11)), 1.5 * scale);
        context.DrawGeometry(shieldFill, shieldPen, shieldGeom);

        // 4. Central Shield Key checkmark
        var checkGeom = new StreamGeometry();
        using (var cctx = checkGeom.Open())
        {
            double cx = center.X;
            double cy = center.Y - 1 * scale;
            cctx.BeginFigure(new Point(cx - 5 * scale, cy), false);
            cctx.LineTo(new Point(cx - 1 * scale, cy + 4 * scale));
            cctx.LineTo(new Point(cx + 6 * scale, cy - 3 * scale));
        }
        var checkPen = new Pen(new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(255, 255, 255)), 2.0 * scale);
        context.DrawGeometry(null, checkPen, checkGeom);
    }

    private static void DrawAuthLockSymbol(DrawingContext context, Point center, double scale, double animPhase, bool isLight)
    {
        // Rotating cybernetic lock dial
        double angle = animPhase * 0.6;
        for (int i = 0; i < 6; i++)
        {
            double a = angle + (i * Math.PI / 3.0);
            double r1 = 20 * scale;
            double r2 = 25 * scale;
            var dialPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 40 : 70), 245, 158, 11)), 1.2 * scale);
            context.DrawLine(dialPen, new Point(center.X + Math.Cos(a) * r1, center.Y + Math.Sin(a) * r1),
                                      new Point(center.X + Math.Cos(a) * r2, center.Y + Math.Sin(a) * r2));
        }

        // Padlock body
        var lockGeom = new StreamGeometry();
        using (var ctx = lockGeom.Open())
        {
            double lx = center.X;
            double ly = center.Y + 2 * scale;
            ctx.BeginFigure(new Point(lx - 12 * scale, ly - 6 * scale), true);
            ctx.LineTo(new Point(lx + 12 * scale, ly - 6 * scale));
            ctx.LineTo(new Point(lx + 12 * scale, ly + 14 * scale));
            ctx.LineTo(new Point(lx - 12 * scale, ly + 14 * scale));
        }
        var lockFill = new SolidColorBrush(Color.FromArgb((byte)(isLight ? 45 : 75), 245, 158, 11));
        var lockPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 160 : 230), 245, 158, 11)), 1.5 * scale);
        context.DrawGeometry(lockFill, lockPen, lockGeom);

        // Padlock shackle
        var shackleGeom = new StreamGeometry();
        using (var sctx = shackleGeom.Open())
        {
            double lx = center.X;
            double ly = center.Y - 6 * scale;
            sctx.BeginFigure(new Point(lx - 7 * scale, ly), false);
            sctx.ArcTo(new Point(lx + 7 * scale, ly), new Size(7 * scale, 9 * scale), 0, false, SweepDirection.Clockwise);
        }
        var shacklePen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 180 : 255), 251, 191, 36)), 2.0 * scale);
        context.DrawGeometry(null, shacklePen, shackleGeom);
    }

    private static void DrawNotFoundSymbol(DrawingContext context, Point center, double scale, double animPhase, bool isLight)
    {
        // Magnifying glass lens
        var lensPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 140 : 210), 99, 102, 241)), 2.0 * scale);
        var lensFill = new SolidColorBrush(Color.FromArgb((byte)(isLight ? 30 : 50), 99, 102, 241));
        Point lensCenter = new Point(center.X - 4 * scale, center.Y - 4 * scale);
        context.DrawEllipse(lensFill, lensPen, lensCenter, 14 * scale, 14 * scale);

        // Handle
        var handlePen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 160 : 230), 139, 92, 246)), 2.5 * scale);
        context.DrawLine(handlePen, new Point(lensCenter.X + 10 * scale, lensCenter.Y + 10 * scale),
                                    new Point(lensCenter.X + 22 * scale, lensCenter.Y + 22 * scale));

        // Question / X in lens
        var xPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 180 : 255), 239, 68, 68)), 1.5 * scale);
        context.DrawLine(xPen, new Point(lensCenter.X - 5 * scale, lensCenter.Y - 5 * scale), new Point(lensCenter.X + 5 * scale, lensCenter.Y + 5 * scale));
        context.DrawLine(xPen, new Point(lensCenter.X + 5 * scale, lensCenter.Y - 5 * scale), new Point(lensCenter.X - 5 * scale, lensCenter.Y + 5 * scale));
    }

    private static void DrawTimeoutSymbol(DrawingContext context, Point center, double scale, double animPhase, bool isLight)
    {
        // Stop-clock / Hourglass circle
        var clockPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 140 : 210), 251, 146, 60)), 1.5 * scale);
        var clockFill = new SolidColorBrush(Color.FromArgb((byte)(isLight ? 30 : 50), 251, 146, 60));
        context.DrawEllipse(clockFill, clockPen, center, 16 * scale, 16 * scale);

        // Clock hand rotating
        double handAngle = animPhase * 1.5;
        var handPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 180 : 255), 244, 63, 94)), 2.0 * scale);
        context.DrawLine(handPen, center, new Point(center.X + Math.Cos(handAngle) * 11 * scale, center.Y + Math.Sin(handAngle) * 11 * scale));
        context.DrawLine(handPen, center, new Point(center.X, center.Y - 9 * scale));
    }

    private static void DrawDnsGlobeSymbol(DrawingContext context, Point center, double scale, double animPhase, bool isLight)
    {
        // Globe wireframe
        var globePen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 140 : 210), 6, 182, 212)), 1.5 * scale);
        var globeFill = new SolidColorBrush(Color.FromArgb((byte)(isLight ? 25 : 45), 6, 182, 212));
        context.DrawEllipse(globeFill, globePen, center, 16 * scale, 16 * scale);
        context.DrawEllipse(null, globePen, center, 7 * scale, 16 * scale);
        context.DrawLine(globePen, new Point(center.X - 16 * scale, center.Y), new Point(center.X + 16 * scale, center.Y));

        // Disconnected red signal pulses
        double pulse = (animPhase * 0.4) % 1.0;
        double radius = 18 * scale + pulse * 14 * scale;
        byte alpha = (byte)(Math.Max(0, (1.0 - pulse) * (isLight ? 70 : 110)));
        var sigPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 239, 68, 68)), 1.2 * scale);
        context.DrawEllipse(null, sigPen, center, radius, radius);
    }

    private static void DrawGenericHazardSymbol(DrawingContext context, Point center, double scale, double animPhase, bool isLight)
    {
        // Hazard Triangle
        var triGeom = new StreamGeometry();
        using (var ctx = triGeom.Open())
        {
            double tx = center.X;
            double ty = center.Y - 2 * scale;
            ctx.BeginFigure(new Point(tx, ty - 16 * scale), true);
            ctx.LineTo(new Point(tx + 18 * scale, ty + 15 * scale));
            ctx.LineTo(new Point(tx - 18 * scale, ty + 15 * scale));
        }
        var triFill = new SolidColorBrush(Color.FromArgb((byte)(isLight ? 35 : 60), 239, 68, 68));
        var triPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(isLight ? 160 : 230), 239, 68, 68)), 1.8 * scale);
        context.DrawGeometry(triFill, triPen, triGeom);

        // Exclamation Mark
        var markPen = new Pen(new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(255, 255, 255)), 2.0 * scale);
        context.DrawLine(markPen, new Point(center.X, center.Y - 7 * scale), new Point(center.X, center.Y + 4 * scale));
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 255, 255)), null, new Point(center.X, center.Y + 9 * scale), 1.2 * scale, 1.2 * scale);
    }
}
