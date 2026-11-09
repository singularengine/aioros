using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Tests.Models;

public sealed record Dot(int Value);

public sealed class Plotted(ImmutableArray<Dot> dots, string color = "red", int from = 0) : System.ComponentModel.INotifyPropertyChanged
{
    public ImmutableArray<Dot> Dots { get; set; } = dots;

    public string Color { get; set; } = color;

    public Dictionary<string, string> Palette { get; } = new(StringComparer.Ordinal) { ["red"] = "#e7000b" };

    public int From
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(From)));
        }
    } = from;

    public int To
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(To)));
        }
    }

    public string Mode
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Mode)));
        }
    } = "lines";

    public byte Level
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Level)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; set; } = "t";

    public ImmutableArray<string> Colors { get; set; } = [color];

    public List<string?> Picked { get; } = [];

    public ICommand Pick => new Picking(this);
}

public sealed class Picking(Plotted plotted) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        plotted.Picked.Add(parameter as string);
        plotted.From = int.Parse((string)parameter!, CultureInfo.InvariantCulture);
    }
}

public sealed class PlotLine : AvaloniaObject
{
    public static readonly StyledProperty<string?> StrokeProperty = AvaloniaProperty.Register<PlotLine, string?>(nameof(Stroke));

    public string? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }
}

public sealed class PlotWindow : AvaloniaObject
{
    public static readonly StyledProperty<int> FromProperty = AvaloniaProperty.Register<PlotWindow, int>(nameof(From), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> ToProperty = AvaloniaProperty.Register<PlotWindow, int>(nameof(To), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> ModeProperty = AvaloniaProperty.Register<PlotWindow, string?>(nameof(Mode), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<byte> LevelProperty = AvaloniaProperty.Register<PlotWindow, byte>(nameof(Level), defaultBindingMode: BindingMode.TwoWay);

    public byte Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public int From
    {
        get => GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public int To
    {
        get => GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public string? Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }
}

public sealed class Plot : Control, IWrittenBySlot
{
    public static readonly StyledProperty<System.Collections.IEnumerable?> PointsProperty = AvaloniaProperty.Register<Plot, System.Collections.IEnumerable?>(nameof(Points));

    public static readonly StyledProperty<string?> CaptionProperty = AvaloniaProperty.Register<Plot, string?>(nameof(Caption));

    public static readonly StyledProperty<AvaloniaList<PlotLine>> LinesProperty = AvaloniaProperty.Register<Plot, AvaloniaList<PlotLine>>(nameof(Lines));

    public static readonly StyledProperty<PlotWindow?> WindowProperty = AvaloniaProperty.Register<Plot, PlotWindow?>(nameof(Window));

    public static readonly StyledProperty<ICommand?> PickCommandProperty = AvaloniaProperty.Register<Plot, ICommand?>(nameof(PickCommand));

    public Plot() => SetValue(LinesProperty, []);

    public System.Collections.IEnumerable? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public AvaloniaList<PlotLine> Lines => GetValue(LinesProperty);

    public PlotWindow? Window
    {
        get => GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    public ICommand? PickCommand
    {
        get => GetValue(PickCommandProperty);
        set => SetValue(PickCommandProperty, value);
    }

    public bool Misdeclared { get; set; }

    public bool Ghost { get; set; }

    public bool Modes { get; set; }

    public string? Slide { get; set; }

    /// <summary>Writes a field over the window's byte level declared with a range wider than a byte.</summary>
    public bool Leveled { get; set; }

    /// <summary>A rule set contributed to the page, as <c>{key}:{order}</c>.</summary>
    public string? Styled { get; set; }

    public WrittenSlot? WrittenSlot
    {
        get
        {
            var parts = ImmutableArray.CreateBuilder<SlotPart>();
            parts.AddRange(Lines.Select((line, index) => SlotPart.Read("line" + index.ToString(CultureInfo.InvariantCulture), line, nameof(PlotLine.Stroke))));
            if (Window is { } window)
            {
                parts.Add(SlotPart.Number("from", window, nameof(PlotWindow.From), 0, 9));
            }

            if (IsSet(PickCommandProperty))
            {
                parts.Add(SlotPart.Command("pick", this, nameof(PickCommand)));
            }

            if (Misdeclared)
            {
                parts.Add(SlotPart.Read("missing", this, "Missing"));
            }

            if (Modes && Window is { } chosen)
            {
                parts.Add(SlotPart.Choice("mode", chosen, nameof(PlotWindow.Mode)));
            }

            if (Slide is not null && Window is { } slid)
            {
                parts.Add(SlotPart.Number("to", slid, nameof(PlotWindow.To), 0, 9));
                parts.Add(SlotPart.Composite("slide", [.. Slide.Split(',')]));
            }

            if (Leveled && Window is { } leveled)
            {
                parts.Add(SlotPart.Number("level", leveled, nameof(PlotWindow.Level), 0, int.MaxValue));
            }

            var argument = $"strokes={string.Join(',', Lines.Select(line => line.Stroke))};from={Window?.From ?? 0}{Flags()}";
            return WrittenSlot.Rendered<PlotRenderer>(argument, nameof(Points), parts.ToImmutable());
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(120, 40);

    private string Flags() => (Leveled ? ";level" : string.Empty) + (Window is null ? string.Empty : ";window") + (IsSet(PickCommandProperty) ? ";pick" : string.Empty) + (Ghost ? ";ghost" : string.Empty)
        + (Modes ? ";modes" : string.Empty) + (Slide is null ? string.Empty : ";slide") + (Styled is null ? string.Empty : ";style=" + Styled);
}

public sealed class PlotRenderer : ISlotRenderer
{
    public static SlotRender Create(string argument, MemberLookup members)
    {
        var settings = argument.Split(';');
        var strokes = settings[0]["strokes=".Length..].Split(',');
        var from = int.Parse(settings[1]["from=".Length..], CultureInfo.InvariantCulture);
        var window = settings.Contains("window");
        var pick = settings.Contains("pick");
        var ghost = settings.Contains("ghost");
        var modes = settings.Contains("modes");
        var slide = settings.Contains("slide");
        var level = settings.Contains("level");
        var styled = settings.FirstOrDefault(static setting => setting.StartsWith("style=", StringComparison.Ordinal))?["style=".Length..].Split(':');
        return (ref SlotOutput output) =>
        {
            Style(ref output, styled);
            Strokes(ref output, strokes);
            var start = output.TryRead("from", out var bound) && bound is int moved ? moved : from;
            if (window)
            {
                output.Buffer.Write(Encoding.UTF8.GetBytes($"<input type=\"range\" min=\"0\" max=\"9\" value=\"{start}\""));
                output.WriteField("from");
                output.Buffer.Write(">"u8);
            }

            Level(ref output, level);
            if (ghost)
            {
                output.Buffer.Write("<input"u8);
                output.WriteField("ghost");
                output.Buffer.Write(">"u8);
            }

            if (modes)
            {
                Modes(ref output);
            }

            if (slide)
            {
                output.Buffer.Write(Encoding.UTF8.GetBytes($"<input type=\"range\" class=\"slide\" data-to=\"{(output.TryRead("to", out var to) ? to : 0)}\" value=\"{start}\""));
                output.WriteField("slide");
                output.Buffer.Write(">"u8);
            }

            Items(ref output, members, start, pick);
            output.Buffer.Write("</svg>"u8);
        };
    }

    /// <summary>A slide posts its new start; the end moves by the same amount, from the current window.</summary>
    public static bool Decode(string key, string posted, ReadOnlySpan<object?> current, Span<string?> decoded)
    {
        if (key != "slide" || !int.TryParse(posted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var from))
        {
            return false;
        }

        var span = (int)current[1]! - (int)current[0]!;
        decoded[0] = from.ToString(CultureInfo.InvariantCulture);
        decoded[1] = (from + span).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static void Style(ref SlotOutput output, string[]? styled)
    {
        if (styled is [var key, var order] && output.NeedsStyle(key))
        {
            output.WriteStyle(key, $".{key}{{order:{order}}}");
        }
    }

    private static void Level(ref SlotOutput output, bool level)
    {
        if (level)
        {
            output.Buffer.Write("<input class=\"level\""u8);
            output.WriteField("level");
            output.Buffer.Write(">"u8);
        }
    }

    private static void Modes(ref SlotOutput output)
    {
        var mode = output.TryRead("mode", out var chosen) ? chosen as string : null;
        foreach (var option in (string[])["lines", "bars"])
        {
            output.Buffer.Write("<input type=\"radio\""u8);
            output.WriteFieldOption("mode", option);
            output.Buffer.Write(Encoding.UTF8.GetBytes(option == mode ? " checked>" : ">"));
        }

        output.Buffer.Write("<button type=\"submit\""u8);
        output.WriteFieldOption("mode", "area");
        output.Buffer.Write(Encoding.UTF8.GetBytes($"{(mode == "area" ? " aria-pressed=\"true\"" : string.Empty)}>Area</button>"));
    }

    private static void Strokes(ref SlotOutput output, string[] strokes)
    {
        var found = new List<string>();
        var colors = new string[strokes.Length];
        for (var index = 0; index < strokes.Length; index++)
        {
            var key = "line" + index.ToString(CultureInfo.InvariantCulture);
            colors[index] = output.TryRead(key, out var stroke) ? stroke as string ?? string.Empty : strokes[index];
            found.AddRange(output.TryRead(key, out _) ? [key] : []);
        }

        output.Buffer.Write(Encoding.UTF8.GetBytes($"<svg data-strokes=\"{string.Join(',', colors)}\" data-read=\"{string.Join(',', found)}\">"));
    }

    private static void Items(ref SlotOutput output, MemberLookup members, int start, bool pick)
    {
        var items = (output.Value as System.Collections.IEnumerable ?? Array.Empty<object>()).Cast<object>().ToList();
        for (var index = Math.Max(0, start); index < items.Count; index++)
        {
            var height = Convert.ToString(members(items[index].GetType(), nameof(Dot.Value))(items[index]), CultureInfo.InvariantCulture);
            if (pick)
            {
                output.Buffer.Write("<button"u8);
                output.WriteCommand("pick", index.ToString(CultureInfo.InvariantCulture));
                output.Buffer.Write(Encoding.UTF8.GetBytes($">{height}</button>"));
            }
            else
            {
                output.Buffer.Write(Encoding.UTF8.GetBytes($"<rect height=\"{height}\"/>"));
            }
        }
    }
}

