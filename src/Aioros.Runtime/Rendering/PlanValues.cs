using System.Globalization;

namespace Aioros.Runtime.Rendering;

public static class PlanValues
{
    /// <summary>The characters a value is formatted into on the stack before a string is made for it.</summary>
    public const int SpanLength = 64;

    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(format: null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Formats a value as <see cref="Format"/> does, into <paramref name="destination"/>, when it is span-formattable and fits.</summary>
    public static bool TryFormat(object? value, Span<char> destination, out int written)
    {
        if (value is ISpanFormattable formattable)
        {
            return formattable.TryFormat(destination, out written, default, CultureInfo.CurrentCulture);
        }

        written = 0;
        return false;
    }

    public static void WriteAll(ref HtmlOutput output, ReadOnlySpan<IPlanNode> nodes, RenderScope scope)
    {
        var outer = OuterScopes.Swap(scope.Outer);
        try
        {
            foreach (var node in nodes)
            {
                node.Write(ref output, scope);
            }
        }
        finally
        {
            OuterScopes.Swap(outer);
        }
    }
}
