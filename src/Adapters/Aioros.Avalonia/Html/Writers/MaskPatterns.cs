using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace Aioros.Avalonia.Html.Writers;

internal static class MaskPatterns
{
    private const string Syntax = @"^$\.*+?()[]{}|/";

    private static readonly FrozenDictionary<char, string> Positions = new Dictionary<char, string>
    {
        ['0'] = "[0-9]",
        ['9'] = "[0-9 ]?",
        ['#'] = @"[0-9 +\-]?",
        ['L'] = @"\p{L}",
        ['?'] = @"\p{L}?",
        ['&'] = ".",
        ['C'] = ".?",
        ['A'] = @"[\p{L}\p{N}]",
        ['a'] = @"[\p{L}\p{N}]?",
        ['<'] = string.Empty,
        ['>'] = string.Empty,
        ['|'] = string.Empty,
    }.ToFrozenDictionary();

    public static string Of(string mask, CultureInfo culture)
    {
        var pattern = new StringBuilder();
        var escaped = false;
        foreach (var ch in mask)
        {
            if (escaped)
            {
                pattern.Append(Literal(ch.ToString()));
                escaped = false;
            }
            else if (ch == '\\')
            {
                escaped = true;
            }
            else
            {
                pattern.Append(Positions.TryGetValue(ch, out var position) ? position : Literal(Separator(ch, culture)));
            }
        }

        return pattern.ToString();
    }

    private static string Separator(char ch, CultureInfo culture) => ch switch
    {
        '.' => culture.NumberFormat.NumberDecimalSeparator,
        ',' => culture.NumberFormat.NumberGroupSeparator,
        ':' => culture.DateTimeFormat.TimeSeparator,
        '/' => culture.DateTimeFormat.DateSeparator,
        '$' => culture.NumberFormat.CurrencySymbol,
        _ => ch.ToString(),
    };

    private static string Literal(string text) =>
        string.Concat(text.Select(ch => Syntax.Contains(ch, StringComparison.Ordinal) ? "\\" + ch : ch.ToString()));
}
