using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Gauge : TemplatedControl
{
    public static readonly StyledProperty<double> LevelProperty = AvaloniaProperty.Register<Gauge, double>(nameof(Level));

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Gauge, string?>(nameof(Title));

    public static readonly StyledProperty<System.Collections.IEnumerable?> PeopleProperty = AvaloniaProperty.Register<Gauge, System.Collections.IEnumerable?>(nameof(People));

    public static readonly StyledProperty<IList<string>?> MarksProperty = AvaloniaProperty.Register<Gauge, IList<string>?>(nameof(Marks));

    public static readonly StyledProperty<object?> PickedProperty = AvaloniaProperty.Register<Gauge, object?>(nameof(Picked));

    private TextBlock? _bar;

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public System.Collections.IEnumerable? People
    {
        get => GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    public IList<string>? Marks
    {
        get => GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public object? Picked
    {
        get => GetValue(PickedProperty);
        set => SetValue(PickedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<TextBlock>("PART_Bar");
        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LevelProperty)
        {
            Show();
        }
    }

    private void Show()
    {
        if (_bar is not null)
        {
            _bar.Text = new string('#', (int)Math.Round(Level));
        }
    }
}

public sealed record Gauged(double Level, string? Title = null, bool Busy = false, IList<string>? Marks = null, object? Picked = null, IList<Person>? People = null);

public sealed class Chooser : ItemsControl
{
    public static readonly StyledProperty<string?> ChosenProperty = AvaloniaProperty.Register<Chooser, string?>(nameof(Chosen));

    public string? Chosen
    {
        get => GetValue(ChosenProperty);
        set => SetValue(ChosenProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.Opacity = Equals(item, Chosen) ? 1 : 0.5;
    }
}

public sealed record Tint(string Name, global::Avalonia.Media.IBrush Paint, global::Avalonia.Media.IBrush Ink, IList<string>? Marks = null);

public sealed record Tints(IList<Tint> Items);

public sealed class Accounts : TemplatedControl
{
    public static readonly StyledProperty<System.Collections.IEnumerable?> RowsProperty = AvaloniaProperty.Register<Accounts, System.Collections.IEnumerable?>(nameof(Rows));

    public System.Collections.IEnumerable? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }
}
