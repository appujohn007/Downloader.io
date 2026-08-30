using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DownloaderApp.Models;

namespace DownloaderApp.Controls;

public class SpeedWaveformControl : Control
{
    public static readonly StyledProperty<double> CurrentSpeedProperty =
        AvaloniaProperty.Register<SpeedWaveformControl, double>(nameof(CurrentSpeed), 0.0);

    public static readonly StyledProperty<int> MaxDataPointsProperty =
        AvaloniaProperty.Register<SpeedWaveformControl, int>(nameof(MaxDataPoints), 50);

    public static readonly StyledProperty<bool> IsDarkModeProperty =
        AvaloniaProperty.Register<SpeedWaveformControl, bool>(nameof(IsDarkMode), true);

    private readonly double[] _targetPoints;
    private readonly double[] _currentPoints;
    private double _peakSpeed = 1024 * 1024; // baseline 1 MB/s scale
    private double _smoothedPeak = 1024 * 1024;
    private double _incomingSmoothedSpeed = 0.0;
    private double _displayedSpeed = 0.0;
    private double _wavePhase = 0.0;
    private readonly DispatcherTimer _animTimer;
    private readonly DispatcherTimer _sampleTimer;

    static SpeedWaveformControl()
    {
        AffectsRender<SpeedWaveformControl>(CurrentSpeedProperty, IsDarkModeProperty, BoundsProperty);
    }

    public SpeedWaveformControl()
    {
        int count = 50;
        _targetPoints = new double[count];
        _currentPoints = new double[count];

        // 60 FPS Fluid Animation & Traveling Chromatic Wave Renderer
        _animTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animTimer.Tick += OnAnimationTick;
        _animTimer.Start();

        // Data sampling timer (every 250ms with low-pass filter)
        _sampleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _sampleTimer.Tick += (s, e) =>
        {
            PushDataPoint(CurrentSpeed);
        };
        _sampleTimer.Start();
    }

    public double CurrentSpeed
    {
        get => GetValue(CurrentSpeedProperty);
        set => SetValue(CurrentSpeedProperty, value);
    }

    public int MaxDataPoints
    {
        get => GetValue(MaxDataPointsProperty);
        set => SetValue(MaxDataPointsProperty, value);
    }

    public bool IsDarkMode
    {
        get => GetValue(IsDarkModeProperty);
        set => SetValue(IsDarkModeProperty, value);
    }

    private void PushDataPoint(double speed)
    {
        // Low-pass filter to dampen sensitivity and avoid rapid spikes
        _incomingSmoothedSpeed += (Math.Max(0, speed) - _incomingSmoothedSpeed) * 0.40;

        for (int i = 0; i < _targetPoints.Length - 1; i++)
        {
            _targetPoints[i] = _targetPoints[i + 1];
        }
        _targetPoints[^1] = _incomingSmoothedSpeed;

        double max = _targetPoints.Max();
        _peakSpeed = Math.Max(max * 1.25, 1024 * 512); // Steady baseline
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // Slower, graceful traveling wave phase (~3x slower than before)
        _wavePhase = (_wavePhase + 0.014) % (Math.PI * 40.0);

        // Relaxed, slow-damping peak scale transitions
        _smoothedPeak += (_peakSpeed - _smoothedPeak) * 0.04;

        // Smooth speed numerical interpolation
        _displayedSpeed += (CurrentSpeed - _displayedSpeed) * 0.065;

        // Smooth liquid damping interpolation for every point (relaxed, fluid 60fps)
        for (int i = 0; i < _currentPoints.Length; i++)
        {
            double diff = _targetPoints[i] - _currentPoints[i];
            _currentPoints[i] += diff * 0.065;
        }

        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animTimer.Stop();
        _sampleTimer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 10 || bounds.Height <= 8 || _currentPoints.Length < 4) return;

        double width = bounds.Width;
        double height = bounds.Height;
        int pointCount = _currentPoints.Length;
        double stepX = width / (pointCount - 1);

        bool isLight = !IsDarkMode;
        bool isZeroSpeed = CurrentSpeed <= 10;

        // Subtle background grid guidelines
        var gridPen = new Pen(new SolidColorBrush(isLight ? Color.FromArgb(10, 15, 23, 42) : Color.FromArgb(10, 255, 255, 255)), 0.85);
        context.DrawLine(gridPen, new Point(0, height * 0.33), new Point(width, height * 0.33));
        context.DrawLine(gridPen, new Point(0, height * 0.66), new Point(width, height * 0.66));

        // Generate primary curve control points with gentle organic fluid harmonics
        var primaryPoints = new List<Point>(pointCount);
        var ghostPoints = new List<Point>(pointCount);
        var reflectionPoints = new List<Point>(pointCount);

        double baselineY = height - 5.0;

        for (int i = 0; i < pointCount; i++)
        {
            double val = _currentPoints[i];
            double normalizedY = Math.Clamp(val / Math.Max(1.0, _smoothedPeak), 0.0, 1.0);

            // Gentle organic fluid harmonic ripple
            double harmonic1 = Math.Sin(_wavePhase * 1.2 + (i * 0.28)) * 1.3;
            double harmonic2 = Math.Cos(_wavePhase * 0.7 + (i * 0.15)) * 0.8;
            double fluidRipple = (harmonic1 + harmonic2) * (isZeroSpeed ? 0.5 : 1.0) * Math.Clamp(normalizedY + 0.25, 0.25, 1.0);

            double baseSpan = height - 10.0;
            double yPrimary = baselineY - (normalizedY * baseSpan) - fluidRipple;
            yPrimary = Math.Clamp(yPrimary, 3.0, baselineY);

            double x = i * stepX;
            primaryPoints.Add(new Point(x, yPrimary));

            // Secondary ghost harmonic wave offset for layered depth
            double ghostRipple = Math.Sin(_wavePhase * 0.9 - (i * 0.24) + 1.2) * 1.5;
            double yGhost = baselineY - (normalizedY * 0.88 * baseSpan) - ghostRipple;
            yGhost = Math.Clamp(yGhost, 3.0, baselineY);
            ghostPoints.Add(new Point(x, yGhost));

            // ================= MINUTE MIRRORED REFLECTION =================
            double distFromBase = baselineY - yPrimary;
            double yReflect = baselineY + (distFromBase * 0.32);
            yReflect = Math.Clamp(yReflect, baselineY, height);
            reflectionPoints.Add(new Point(x, yReflect));
        }

        // ================= 1. GHOST / SECONDARY DEPTH WAVE =================
        var ghostGeo = BuildSmoothGeometry(ghostPoints, false, width, baselineY);
        var ghostColor = isLight ? Color.FromArgb(20, 59, 130, 246) : Color.FromArgb(24, 99, 102, 241);
        var ghostPen = new Pen(new SolidColorBrush(ghostColor), 1.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, ghostPen, ghostGeo);

        // ================= 2. LUMINOUS AURORA FILL UNDER CURVE =================
        var fillGeo = BuildSmoothGeometry(primaryPoints, true, width, baselineY);

        // Calculate dynamic flowing gradient colors with relaxed, slow shift
        double shift = (_wavePhase * 0.028) % 1.0;
        var c0 = SampleFlowingColor(shift + 0.0);
        var c1 = SampleFlowingColor(shift + 0.33);
        var c2 = SampleFlowingColor(shift + 0.66);
        var c3 = SampleFlowingColor(shift + 1.0);

        byte fillTopAlpha = isLight ? (byte)26 : (byte)40;
        byte fillMidAlpha = isLight ? (byte)10 : (byte)16;

        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(fillTopAlpha, c1.R, c1.G, c1.B), 0.0),
                new GradientStop(Color.FromArgb(fillMidAlpha, c2.R, c2.G, c2.B), 0.65),
                new GradientStop(Color.FromArgb(0, c3.R, c3.G, c3.B), 1.0)
            }
        };
        context.DrawGeometry(fillBrush, null, fillGeo);

        // ================= 3. MINUTE SUBTLE REFLECTION EFFECT =================
        var reflectGeo = BuildSmoothGeometry(reflectionPoints, false, width, height);
        byte reflectAlpha = isLight ? (byte)18 : (byte)24;
        var reflectBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(reflectAlpha, c1.R, c1.G, c1.B), 0.0),
                new GradientStop(Color.FromArgb(0, c2.R, c2.G, c2.B), 1.0)
            }
        };
        var reflectPen = new Pen(reflectBrush, 1.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, reflectPen, reflectGeo);

        // ================= 4. SOFT AMBIENT GLOW HALO STROKE =================
        var strokeGeo = BuildSmoothGeometry(primaryPoints, false, width, baselineY);
        byte haloAlpha = isLight ? (byte)30 : (byte)42;
        var haloPen = new Pen(new SolidColorBrush(Color.FromArgb(haloAlpha, c0.R, c0.G, c0.B)), 3.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, haloPen, strokeGeo);

        // ================= 5. DYNAMIC FLOWING CHROMATIC NEON STROKE =================
        var strokeBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(c0, 0.0),
                new GradientStop(c1, 0.35),
                new GradientStop(c2, 0.70),
                new GradientStop(c3, 1.0)
            }
        };
        var strokePen = new Pen(strokeBrush, 2.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, strokePen, strokeGeo);

        // ================= 6. LEADING CREST PULSE SPARK =================
        var headPoint = primaryPoints.Last();
        double pulseScale = 1.0 + (Math.Sin(_wavePhase * 2.2) * 0.20);
        double outerRadius = 5.0 * pulseScale;

        // Outer glowing halo
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, c3.R, c3.G, c3.B)), null, headPoint, outerRadius, outerRadius);
        // Inner vibrant core
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(c3.R, c3.G, c3.B)), null, headPoint, 2.8, 2.8);
        // White center spark
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 255, 255)), null, headPoint, 1.4, 1.4);

        // ================= 7. FLOATING DYNAMIC SPEED BADGE / BOX =================
        string speedText = _displayedSpeed > 50 ? $"{DownloadItem.FormatBytes((long)_displayedSpeed)}/s" : "0 B/s";

        var textBrush = new SolidColorBrush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(248, 250, 252));
        var formatted = new FormattedText(
            speedText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            9.0,
            textBrush);

        double badgeW = formatted.Width + 18.0;
        double badgeH = 16.0;

        // Position pill smoothly anchored to the head point
        double badgeX = headPoint.X - badgeW - 8.0;
        if (badgeX < 2.0) badgeX = 2.0;

        double badgeY = headPoint.Y - (badgeH * 0.5);
        badgeY = Math.Clamp(badgeY, 2.0, height - badgeH - 2.0);

        var badgeRect = new Rect(badgeX, badgeY, badgeW, badgeH);
        var badgeRRect = new RoundedRect(badgeRect, 4.0);

        // Frosted glass background
        var badgeBg = new SolidColorBrush(isLight ? Color.FromArgb(220, 255, 255, 255) : Color.FromArgb(210, 15, 23, 42));
        var badgeBorder = new Pen(new SolidColorBrush(Color.FromArgb(170, c3.R, c3.G, c3.B)), 1.0);

        context.DrawRectangle(badgeBg, badgeBorder, badgeRRect);

        // Glowing mini indicator dot inside badge
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(c3.R, c3.G, c3.B)), null, new Point(badgeX + 6.5, badgeY + badgeH * 0.5), 2.0, 2.0);

        // Speed text
        context.DrawText(formatted, new Point(badgeX + 11.5, badgeY + (badgeH - formatted.Height) * 0.5));
    }

    /// <summary>
    /// Builds a silky smooth cubic Bezier spline with natural Catmull-Rom curvature tangents
    /// </summary>
    private static StreamGeometry BuildSmoothGeometry(List<Point> points, bool isFill, double width, double height)
    {
        var geo = new StreamGeometry();
        using var ctx = geo.Open();

        if (isFill)
        {
            ctx.BeginFigure(new Point(0, height), true);
            ctx.LineTo(points[0]);
        }
        else
        {
            ctx.BeginFigure(points[0], false);
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i < points.Count - 2 ? points[i + 2] : p2;

            double tension = 0.5;
            var cp1 = new Point(
                p1.X + (p2.X - p0.X) * tension / 3.0,
                p1.Y + (p2.Y - p0.Y) * tension / 3.0);

            var cp2 = new Point(
                p2.X - (p3.X - p1.X) * tension / 3.0,
                p2.Y - (p3.Y - p1.Y) * tension / 3.0);

            ctx.CubicBezierTo(cp1, cp2, p2);
        }

        if (isFill)
        {
            ctx.LineTo(new Point(width, height));
            ctx.EndFigure(true);
        }
        else
        {
            ctx.EndFigure(false);
        }

        return geo;
    }

    /// <summary>
    /// Continuous 360-degree seamless circular chromatic color flow without any discrete palette jumps or abrupt transitions.
    /// </summary>
    private static Color SampleFlowingColor(double t)
    {
        t = (t % 1.0 + 1.0) % 1.0;
        double hue = t * 360.0;
        return HslToRgb(hue, 0.92, 0.54);
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
}
