using System.Collections;
using System.Collections.Specialized;
using Aioros.Avalonia.Charts;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Aioros.Avalonia.Controls;

public enum ChartKind
{
    Line,
    Area,
    Column,
    Candle,
    Donut,
}

public sealed class Chart : Control
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Chart, string?>(nameof(Title));

    public static readonly StyledProperty<bool> ShowAxesProperty = AvaloniaProperty.Register<Chart, bool>(nameof(ShowAxes), true);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Chart>();

    public static readonly StyledProperty<IBrush?> GridLinesProperty = AvaloniaProperty.Register<Chart, IBrush?>(nameof(GridLines));

    static Chart() => AffectsRender<Chart>(TitleProperty, ShowAxesProperty, ForegroundProperty, GridLinesProperty);

    public Chart() => Series.CollectionChanged += OnSeriesChanged;

    [Content]
    public AvaloniaList<ChartSeries> Series { get; } = [];

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowAxes
    {
        get => GetValue(ShowAxesProperty);
        set => SetValue(ShowAxesProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? GridLines
    {
        get => GetValue(GridLinesProperty);
        set => SetValue(GridLinesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var model = ChartModels.Build(ChartDefinitions.Of(this, null), [.. Series.Select(series => series.ItemsSource)]);
        _ = ChartModels.Render(model, new AvaloniaRenderContext(context), Bounds.Width, Bounds.Height);
    }

    internal void Changed() => InvalidateVisual();

    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var series in e.OldItems?.OfType<ChartSeries>() ?? [])
        {
            LogicalChildren.Remove(series);
            series.Owner = null;
        }

        foreach (var series in e.NewItems?.OfType<ChartSeries>() ?? [])
        {
            series.Owner = this;
            LogicalChildren.Add(series);
        }

        InvalidateVisual();
    }
}

public sealed class ChartSeries : StyledElement
{
    public static readonly StyledProperty<ChartKind> KindProperty = AvaloniaProperty.Register<ChartSeries, ChartKind>(nameof(Kind));

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<ChartSeries, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string?> XPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(XPath));

    public static readonly StyledProperty<string?> YPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(YPath));

    public static readonly StyledProperty<string?> OpenPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(OpenPath));

    public static readonly StyledProperty<string?> HighPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(HighPath));

    public static readonly StyledProperty<string?> LowPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(LowPath));

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<ChartSeries, double>(nameof(StrokeThickness), 2);

    private INotifyCollectionChanged? _observed;

    internal Chart? Owner { get; set; }

    public ChartKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? XPath
    {
        get => GetValue(XPathProperty);
        set => SetValue(XPathProperty, value);
    }

    public string? YPath
    {
        get => GetValue(YPathProperty);
        set => SetValue(YPathProperty, value);
    }

    public string? OpenPath
    {
        get => GetValue(OpenPathProperty);
        set => SetValue(OpenPathProperty, value);
    }

    public string? HighPath
    {
        get => GetValue(HighPathProperty);
        set => SetValue(HighPathProperty, value);
    }

    public string? LowPath
    {
        get => GetValue(LowPathProperty);
        set => SetValue(LowPathProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _observed?.CollectionChanged -= OnItemsChanged;
            _observed = ItemsSource as INotifyCollectionChanged;
            _observed?.CollectionChanged += OnItemsChanged;
        }

        Owner?.Changed();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Owner?.Changed();
}
