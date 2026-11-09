using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// A control written once per value of its binding's source that the compiled page holds, as Avalonia renders each (an
/// icon whose converter turns a name into a geometry): each render writes the body learned for the scope's value, found by
/// its type and text. A value the plan never learned is a <see cref="PlanMissException"/>.
/// </summary>
public sealed record LearnedNode(ValueGetter Get, ImmutableArray<string> Keys, ImmutableArray<ImmutableArray<IPlanNode>> Bodies) : IPlanNode
{
    private readonly FrozenDictionary<string, int> _index = Keys.Select((key, index) => KeyValuePair.Create(key, index)).ToFrozenDictionary(StringComparer.Ordinal);

    public SlotSpec? Source { get; init; }

    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        var value = Get(scope.Current);
        if (!_index.TryGetValue(KeyOf(value), out var index))
        {
            throw new PlanMissException($"the value '{PlanValues.Format(value)}' of '{Source?.Path}', which the compiled page did not hold");
        }

        PlanValues.WriteAll(ref output, Bodies[index].AsSpan(), scope);
    }

    /// <summary>A value's key: its type's full name and its invariant text, or for a list of any type the keys of its items in order.</summary>
    public static string KeyOf(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => value.GetType().FullName + ":" + formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        string => value.GetType().FullName + ":" + value,
        System.Collections.IEnumerable items => "[" +string.Join('\u001F', items.Cast<object?>().Select(KeyOf)) + "]",
        _ => value.GetType().FullName + ":" + value,
    };
}
