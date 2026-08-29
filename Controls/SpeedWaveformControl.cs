using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace DownloaderApp.Controls;

public class SpeedWaveformControl : Control
{
    public static readonly StyledProperty<double> CurrentSpeedProperty =
        AvaloniaProperty.Register<SpeedWaveformControl, double>(nameof(CurrentSpeed), 0.0);

    public static readonly StyledProperty<int> MaxDataPointsProperty =
        AvaloniaProperty.Register<SpeedWaveformControl, int>(nameof(MaxDataPoints), 60);

    public static readonly StyledProperty<bool> IsDarkModeProperty =
        AvaloniaProperty.Register<SpeedWaveformControl, bool>(nameof(IsDarkMode), true);

    private readonly List<double> _history = new();
    private double _peakSpeed = 1024 * 1024; // baseline 1 MB/s scale
    private readonly DispatcherTimer _sampleTimer;

    static SpeedWaveformControl()
    {
        AffectsRender<SpeedWaveformControl>(CurrentSpeedProperty, IsDarkModeProperty, BoundsProperty);
    }

    public SpeedWaveformControl()
    {
        for (int i = 0; i < 60; i++)
        {
            _history.Add(0);
        }

        _sampleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _sampleTimer.Tick += (s, e) =>
        {
            PushDataPoint(CurrentSpeed);
            InvalidateVisual();
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

    public void PushDataPoint(double speed)
    {
        if (_history.Count >= MaxDataPoints)
        {
            _history.RemoveAt(0);
        }
        _history.Add(speed);

        double max = _history.Max();
        _peakSpeed = Math.Max(max * 1.15, 1024 * 512); // At least 512 KB/s baseline
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 10 || bounds.Height <= 10 || _history.Count < 2) return;

        double width = bounds.Width;
        double height = bounds.Height;
        double stepX = width / (_history.Count - 1);

        // Grid lines
        var gridPen = new Pen(new SolidColorBrush(IsDarkMode ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0)), 1);
        context.DrawLine(gridPen, new Point(0, height * 0.5), new Point(width, height * 0.5));
        context.DrawLine(gridPen, new Point(0, height * 0.25), new Point(width, height * 0.25));
        context.DrawLine(gridPen, new Point(0, height * 0.75), new Point(width, height * 0.75));

        // Generate geometry points
        var points = new List<Point>();
        for (int i = 0; i < _history.Count; i++)
        {
            double val = _history[i];
            double normalizedY = Math.Clamp(val / _peakSpeed, 0.0, 1.0);
            double y = height - (normalizedY * (height - 6)) - 3;
            double x = i * stepX;
            points.Add(new Point(x, y));
        }

        // Create fill path under the curve
        var fillGeo = new StreamGeometry();
        using (var ctx = fillGeo.Open())
        {
            ctx.BeginFigure(new Point(0, height), true);
            ctx.LineTo(points[0]);

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var cp1 = new Point(p0.X + (p1.X - p0.X) * 0.5, p0.Y);
                var cp2 = new Point(p0.X + (p1.X - p0.X) * 0.5, p1.Y);
                ctx.CubicBezierTo(cp1, cp2, p1);
            }

            ctx.LineTo(new Point(width, height));
            ctx.EndFigure(true);
        }

        // Gradient Fill under curve
        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(90, 59, 130, 246), 0.0),
                new GradientStop(Color.FromArgb(10, 37, 99, 235), 0.7),
                new GradientStop(Color.FromArgb(0, 37, 99, 235), 1.0)
            }
        };
        context.DrawGeometry(fillBrush, null, fillGeo);

        // Line Stroke
        var strokeGeo = new StreamGeometry();
        using (var ctx = strokeGeo.Open())
        {
            ctx.BeginFigure(points[0], false);
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var cp1 = new Point(p0.X + (p1.X - p0.X) * 0.5, p0.Y);
                var cp2 = new Point(p0.X + (p1.X - p0.X) * 0.5, p1.Y);
                ctx.CubicBezierTo(cp1, cp2, p1);
            }
            ctx.EndFigure(false);
        }

        var strokeBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(6, 182, 212), 0.0),
                new GradientStop(Color.FromRgb(59, 130, 246), 0.5),
                new GradientStop(Color.FromRgb(147, 51, 234), 1.0)
            }
        };
        var strokePen = new Pen(strokeBrush, 2.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, strokePen, strokeGeo);

        // Glow indicator dot on latest point
        var lastPoint = points.Last();
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, 59, 130, 246)), null, lastPoint, 5, 5);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 255, 255)), null, lastPoint, 2.5, 2.5);
    }
}

