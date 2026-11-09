using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Tests.Models;

public sealed record Bar(int Value);

public sealed record Barred(ImmutableArray<Bar> Bars, string Title = "", ImmutableDictionary<string, ImmutableArray<Bar>>? Groups = null);

public sealed class Bars : Control, IWrittenBySlot
{
    public static readonly StyledProperty<System.Collections.IEnumerable?> PointsProperty = AvaloniaProperty.Register<Bars, System.Collections.IEnumerable?>(nameof(Points));

    public static readonly StyledProperty<string?> CaptionProperty = AvaloniaProperty.Register<Bars, string?>(nameof(Caption));

    public static ImmutableArray<Bar> Sample { get; } = [new Bar(2), new Bar(9)];

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

    public WrittenSlot? WrittenSlot => WrittenSlot.Of<BarsWriter>("fill=#123", nameof(Points));

    protected override Size MeasureOverride(Size availableSize) => new(120, 40);
}

public sealed class BarsWriter : ISlotWriter
{
    public static SlotWrite Create(string argument, MemberLookup members)
    {
        var open = Encoding.UTF8.GetBytes($"<svg data-a=\"{argument}\">");
        return (value, output) =>
        {
            output.Write(open);
            foreach (var item in value as System.Collections.IEnumerable ?? Array.Empty<object>())
            {
                output.Write(Encoding.UTF8.GetBytes($"<rect height=\"{Convert.ToString(members(item.GetType(), nameof(Bar.Value))(item), CultureInfo.InvariantCulture)}\"/>"));
            }

            output.Write("</svg>"u8);
        };
    }
}
