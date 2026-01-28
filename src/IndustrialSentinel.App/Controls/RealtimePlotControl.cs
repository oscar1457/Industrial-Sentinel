using System.Windows;
using System.Windows.Media;
using IndustrialSentinel.Core.Telemetry;

namespace IndustrialSentinel.App.Controls;

public sealed class RealtimePlotControl : FrameworkElement
{
    public static readonly DependencyProperty SeriesBufferProperty = DependencyProperty.Register(
        nameof(SeriesBuffer),
        typeof(TelemetrySeriesBuffer),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MetricProperty = DependencyProperty.Register(
        nameof(Metric),
        typeof(TelemetryMetric),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(TelemetryMetric.Rpm, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(
        nameof(MinValue),
        typeof(double),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue),
        typeof(double),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(Brushes.Lime, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush),
        typeof(Brush),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundBrushProperty = DependencyProperty.Register(
        nameof(BackgroundBrush),
        typeof(Brush),
        typeof(RealtimePlotControl),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly Pen _gridPen = new(new SolidColorBrush(Color.FromRgb(32, 40, 52)), 1);

    public RealtimePlotControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SnapsToDevicePixels = true;
        _gridPen.Freeze();
    }

    public TelemetrySeriesBuffer? SeriesBuffer
    {
        get => (TelemetrySeriesBuffer?)GetValue(SeriesBufferProperty);
        set => SetValue(SeriesBufferProperty, value);
    }

    public TelemetryMetric Metric
    {
        get => (TelemetryMetric)GetValue(MetricProperty);
        set => SetValue(MetricProperty, value);
    }

    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public Brush BackgroundBrush
    {
        get => (Brush)GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(BackgroundBrush, null, rect);
        DrawGrid(dc, rect);

        var buffer = SeriesBuffer;
        if (buffer is null)
        {
            return;
        }

        var snapshot = buffer.Snapshot();
        var values = Metric switch
        {
            TelemetryMetric.Rpm => snapshot.Rpm,
            TelemetryMetric.Temperature => snapshot.Temperature,
            TelemetryMetric.Vibration => snapshot.Vibration,
            _ => snapshot.Rpm
        };

        if (values.Length < 2 || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var min = MinValue;
        var max = MaxValue;
        if (max <= min)
        {
            return;
        }

        var points = new Point[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var x = rect.Left + (i / (double)(values.Length - 1)) * rect.Width;
            var normalized = (values[i] - min) / (max - min);
            normalized = Math.Clamp(normalized, 0, 1);
            var y = rect.Bottom - normalized * rect.Height;
            points[i] = new Point(x, y);
        }

        var fill = FillBrush is not null;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], fill, fill);
            context.PolyLineTo(points, true, true);
            if (fill)
            {
                context.LineTo(new Point(rect.Right, rect.Bottom), true, false);
                context.LineTo(new Point(rect.Left, rect.Bottom), true, false);
            }
        }
        geometry.Freeze();

        var pen = new Pen(LineBrush, 1.6);
        pen.Freeze();

        dc.DrawGeometry(FillBrush, pen, geometry);
    }

    private void DrawGrid(DrawingContext dc, Rect rect)
    {
        const int divisions = 4;
        for (var i = 1; i < divisions; i++)
        {
            var y = rect.Top + (rect.Height / divisions) * i;
            dc.DrawLine(_gridPen, new Point(rect.Left, y), new Point(rect.Right, y));
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}
