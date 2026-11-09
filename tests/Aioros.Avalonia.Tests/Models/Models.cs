using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Article(string title, string body = "")
{
    public string Title { get; } = title;

    public string Body { get; } = body;

    public double Fade { get; } = 0.5;
}

public sealed class Swatch
{
    public Swatch(string title)
    {
        Title = title;
        Fill = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Colors.Teal);
        OnUiThread = Fill.CheckAccess() && global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
    }

    public string Title { get; }

    public global::Avalonia.Media.SolidColorBrush Fill { get; }

    public bool OnUiThread { get; }
}

public sealed class Painted(global::Avalonia.Media.IBrush fill)
{
    public global::Avalonia.Media.IBrush Fill { get; } = fill;
}

public sealed class Secret(bool show)
{
    public bool Show { get; } = show;
}

public sealed record Item(string Name);

public sealed class Catalog(params Item[] items)
{
    public ImmutableArray<Item> Items { get; } = [.. items];
}

public sealed class Brand(string name)
{
    public string Name { get; } = name;
}

public sealed partial class Signup : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public string Token { get; set; } = "current";

    public int Saved { get; private set; }

    [RelayCommand]
    private void Save() => Saved++;
}

public sealed partial class Ledger : ObservableObject
{
    [ObservableProperty]
    public partial bool Flag { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public string Token { get; set; } = "current";

    public int Saved { get; private set; }

    [RelayCommand]
    private void Save() => Saved++;
}

public sealed partial class Profile : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = "Ada";

    [ObservableProperty]
    public partial bool Agree { get; set; } = true;

    [ObservableProperty]
    public partial string Plan { get; set; } = "Pro";

    public int Pings { get; private set; }

    [RelayCommand]
    private void Ping() => Pings++;
}

public sealed record Poster(string Url, string Title);

public sealed class Posters(params Poster[] posters)
{
    public ImmutableArray<Poster> Items { get; } = [.. posters];
}

public sealed class Traced(params double[] values)
{
    public IList<global::Avalonia.Point> Line { get; } = [.. values.Select((value, index) => new global::Avalonia.Point(index * 10, value))];
}

public sealed class LooseItems
{
    public System.Collections.ArrayList Loose { get; } = [];
}

public sealed class Tally(int count, params string[] shots)
{
    public int Count { get; } = count;

    public ImmutableArray<string> Shots { get; } = [.. shots];
}

public sealed record Ticket(string Name, int Priority, string? Owner);

public sealed class PriorityBrush : global::Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is int priority && priority > 2 ? global::Avalonia.Media.Brushes.Red : global::Avalonia.Media.Brushes.Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

public sealed class Shout : global::Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        (value as string)?.ToUpper(culture) + parameter;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

public sealed class Tuned : global::Avalonia.Data.Converters.IValueConverter
{
    public string Suffix { get; set; } = string.Empty;

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value + Suffix;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

public sealed class CornerConverter : global::Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        new global::Avalonia.CornerRadius(value is true ? 8 : 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

public sealed record Circle(double Radius);

public sealed record Square(double Side, string Label);

public sealed class Tray
{
    public IRelayCommand<string> Pick { get; } = new RelayCommand<string>(_ => { });
}

public sealed class Pin(string label, Tray tray)
{
    public string Label => label;

    public IRelayCommand<string> Pick => tray.Pick;
}

public sealed class Shapes(params object[] items)
{
    public ImmutableArray<object> Items { get; } = [.. items];
}

public sealed record Progress(int Done, int Total, bool Open, bool Mine);

public sealed class Ratio : global::Avalonia.Data.Converters.IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        values is [int done, int total] && total > 0 ? (done * 100 / total).ToString(culture) + "%" : "-";
}

public sealed class Corners : global::Avalonia.Data.Converters.IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => new global::Avalonia.CornerRadius(4);
}

public sealed class Tile(bool flag, string text, double size, string[] items, string color, string mode, object child, double size2, int col, string align)
{
    public static Tile On { get; } = new(true, "alpha", 0.5, ["one", "two"], "#ff0000", "on", new Circle(2), 12, 0, "Left");

    public static Tile Off { get; } = new(false, "beta", 0.9, ["x"], "#00ff00", "off", new Square(3, "sq"), 20, 1, "Right");

    public bool Flag => flag;
    public string Text => text;
    public double Size => size;
    public ImmutableArray<string> Items => [.. items];
    public string Color => color;
    public string Mode => mode;
    public object Child => child;
    public double Size2 => size2;
    public int Col => col;
    public string Align => align;
    public global::Avalonia.Thickness Gap => new(size2);
    public global::Avalonia.Media.IBrush Brush => global::Avalonia.Media.Brush.Parse(color);
    public global::Avalonia.Media.BoxShadows Shadow => global::Avalonia.Media.BoxShadows.Parse(flag ? "0 2 4 #000000" : "0 8 16 #ff0000");
    public List<string?> Picked { get; } = [];

    public IRelayCommand<string> Act => field ??= new RelayCommand<string>(Picked.Add, parameter => flag && parameter is not null);
}

public sealed record Shaped(global::Avalonia.Thickness Pad, global::Avalonia.CornerRadius Radius, double Number, string Text, double AngleX, double AngleY, global::Avalonia.Matrix Matrix, string Transform, global::Avalonia.Media.ITransform? Turn)
{
    public static Shaped On { get; } = new(new(1, 2, 3, 4), new(5, 6, 7, 8), 6, "1,2,3,4", 12, -20, new(1, 0.5, 0, 1, 10, 20), "rotate(30deg) translate(4px,2px)", global::Avalonia.Media.Transformation.TransformOperations.Parse("scale(2,3)"));

    public static Shaped Off { get; } = new(new(9), new(2, 3), 0, "x", 0, 0, global::Avalonia.Matrix.Identity, "nonsense", null);
}

public static class TextConverters
{
    public static global::Avalonia.Data.Converters.FuncValueConverter<string?, string?> Whisper { get; } = new(text => text?.ToLowerInvariant() + "...");
}

public enum Level
{
    Low = 1,
    High = 3,
}

public sealed class LevelMatch : global::Avalonia.Data.Converters.IValueConverter
{
    public static LevelMatch Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is int level && parameter is Level expected && level == (int)expected;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

public sealed record Dated(double Size, DateTime Created, DateTime? Done);

public sealed class Captioned : global::Avalonia.Controls.Primitives.HeaderedContentControl
{
    protected override Type StyleKeyOverride => typeof(global::Avalonia.Controls.Primitives.HeaderedContentControl);
}

public sealed class BothSet : global::Avalonia.Data.Converters.IMultiValueConverter
{
    public static BothSet Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => values.All(value => value is not null and not global::Avalonia.UnsetValueType);
}

public sealed record Toggled(string Name, bool On);

public sealed class Chips(params Toggled[] items)
{
    public IReadOnlyList<Toggled> Items { get; } = items;
}

public sealed record Holding(Toggled Item);

public sealed class Drafts
{
    public string? Hinted { get; set; }

    public string? Bound { get; set; }

    public string? Both { get; set; }

    public string? Off { get; set; }

    public string? Plain { get; set; }
}

public sealed record CurrentCrumb(string Title, string? Mark);

public sealed class CurrentTrail(params CurrentCrumb[] crumbs)
{
    public ImmutableArray<CurrentCrumb> Crumbs { get; } = [.. crumbs];
}

/// <summary>A model whose alignment the page binds, so the plan writes the bound panel once per alignment.</summary>
public sealed class Aligned(global::Avalonia.Layout.HorizontalAlignment align)
{
    public global::Avalonia.Layout.HorizontalAlignment Align { get; } = align;
}

public interface IInterfaceRow
{
    string Title { get; }
}

public sealed record InterfaceRow(string Title) : IInterfaceRow;

public sealed class InterfaceRows(params IInterfaceRow[] rows)
{
    public ImmutableArray<IInterfaceRow> Items { get; } = [.. rows];
}

public abstract class BaseCard
{
    public abstract string Title { get; }
}

public sealed class ConcreteCard(string title) : BaseCard
{
    public override string Title { get; } = title;
}

public sealed class BaseCards(params BaseCard[] cards)
{
    public ImmutableArray<BaseCard> Items { get; } = [.. cards];
}

public abstract class UnmadeCard
{
    public abstract string Title { get; }
}

public sealed class UnmadeCards
{
    public ImmutableArray<UnmadeCard> Items { get; } = [];
}
