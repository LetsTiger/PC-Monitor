using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Controls;

public partial class MetricLineChart : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(MetricLineChart),
            new PropertyMetadata(null, OnChartPropertyChanged));

    public static readonly DependencyProperty MetricProperty =
        DependencyProperty.Register(
            nameof(Metric),
            typeof(string),
            typeof(MetricLineChart),
            new PropertyMetadata("Fps", OnChartPropertyChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string Metric
    {
        get => (string)GetValue(MetricProperty);
        set => SetValue(MetricProperty, value);
    }

    public MetricLineChart()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
    }

    private static void OnChartPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var chart =
            (MetricLineChart)dependencyObject;

        if (eventArgs.Property ==
            ItemsSourceProperty)
        {
            if (eventArgs.OldValue is
                INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -=
                    chart.OnItemsCollectionChanged;
            }

            if (eventArgs.NewValue is
                INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged +=
                    chart.OnItemsCollectionChanged;
            }
        }

        chart.Redraw();
    }

    private void OnItemsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        Redraw();
    }

    private void Redraw()
    {
        if (ChartCanvas is null)
        {
            return;
        }

        ChartCanvas.Children.Clear();

        FrameChartPoint[] points =
            ItemsSource?
                .OfType<FrameChartPoint>()
                .OrderBy(item => item.SecondsFromStart)
                .ToArray()
            ?? [];

        EmptyText.Visibility =
            points.Length < 2
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (points.Length < 2 ||
            ChartCanvas.ActualWidth <= 20 ||
            ChartCanvas.ActualHeight <= 20)
        {
            return;
        }

        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;
        double left = 42;
        double right = 8;
        double top = 8;
        double bottom = 24;
        double plotWidth = Math.Max(1, width - left - right);
        double plotHeight = Math.Max(1, height - top - bottom);

        Func<FrameChartPoint, double> selector =
            string.Equals(Metric, "FrameTime", StringComparison.OrdinalIgnoreCase)
                ? item => item.FrameTimeMilliseconds
                : item => item.FramesPerSecond;

        double minX = points[0].SecondsFromStart;
        double maxX = Math.Max(minX + 0.001, points[^1].SecondsFromStart);
        double minY = 0;
        double maxY = points.Max(selector);

        if (string.Equals(Metric, "FrameTime", StringComparison.OrdinalIgnoreCase))
        {
            maxY = Math.Min(Math.Max(33.3, maxY), 150);
        }
        else
        {
            maxY = Math.Max(60, Math.Ceiling(maxY / 20) * 20);
        }

        DrawGrid(left, top, plotWidth, plotHeight, minY, maxY, maxX - minX);

        var polyline = new Polyline
        {
            Stroke = FindResource("PrimaryBrush") as Brush ?? Brushes.DeepSkyBlue,
            StrokeThickness = 2
        };

        foreach (FrameChartPoint point in points)
        {
            double value = Math.Min(maxY, Math.Max(minY, selector(point)));
            double x = left + (point.SecondsFromStart - minX) /
                       (maxX - minX) * plotWidth;
            double y = top + plotHeight -
                       (value - minY) / Math.Max(0.001, maxY - minY) * plotHeight;
            polyline.Points.Add(new Point(x, y));

            if (point.IsSpike &&
                string.Equals(Metric, "FrameTime", StringComparison.OrdinalIgnoreCase))
            {
                var marker = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = Brushes.IndianRed
                };

                Canvas.SetLeft(marker, x - 2);
                Canvas.SetTop(marker, y - 2);
                ChartCanvas.Children.Add(marker);
            }
        }

        ChartCanvas.Children.Add(polyline);
    }

    private void DrawGrid(
        double left,
        double top,
        double plotWidth,
        double plotHeight,
        double minY,
        double maxY,
        double duration)
    {
        for (int index = 0; index <= 4; index++)
        {
            double ratio = index / 4d;
            double y = top + plotHeight - ratio * plotHeight;

            ChartCanvas.Children.Add(new Line
            {
                X1 = left,
                X2 = left + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = FindResource("BorderBrush") as Brush ?? Brushes.DimGray,
                StrokeThickness = 1
            });

            var label = new TextBlock
            {
                Text = (minY + ratio * (maxY - minY)).ToString("F0"),
                FontSize = 10,
                Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray
            };

            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y - 8);
            ChartCanvas.Children.Add(label);
        }

        var durationLabel = new TextBlock
        {
            Text = $"{duration:F0} s",
            FontSize = 10,
            Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray
        };

        Canvas.SetRight(durationLabel, 4);
        Canvas.SetBottom(durationLabel, 2);
        ChartCanvas.Children.Add(durationLabel);
    }
}
