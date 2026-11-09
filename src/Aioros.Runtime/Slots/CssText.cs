using System.Buffers;
using System.Globalization;
using System.Text;

namespace Aioros.Runtime.Slots;

/// <summary>CSS values as the adapter writes them: numbers rounded to three decimals and colors from hex text.</summary>
public static class CssText
{
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    public static string Num(double value) => Rounded(value).ToString(CultureInfo.InvariantCulture);

    /// <summary>A number as <see cref="Num"/> writes it, into <paramref name="destination"/>.</summary>
    public static bool TryNum(double value, Span<char> destination, out int written) =>
        Rounded(value).TryFormat(destination, out written, provider: CultureInfo.InvariantCulture);

    /// <summary>A color from its ARGB components: <c>#rrggbb</c> when opaque, <c>rgba(...)</c> otherwise.</summary>
    public static string Color(uint argb)
    {
        Span<char> text = stackalloc char[48];
        TryColor(argb, text, out var written);
        return new string(text[..written]);
    }

    /// <summary>A color as <see cref="Color(uint)"/> writes it, into <paramref name="destination"/>.</summary>
    public static bool TryColor(uint argb, Span<char> destination, out int written)
    {
        var alpha = argb >> 24;
        if (alpha != 255)
        {
            return destination.TryWrite(CultureInfo.InvariantCulture, $"rgba({(argb >> 16) & 0xFF},{(argb >> 8) & 0xFF},{argb & 0xFF},{Rounded(alpha / 255.0)})", out written);
        }

        written = 0;
        if (destination.Length < 7)
        {
            return false;
        }

        destination[0] = '#';
        (argb & 0xFFFFFF).TryFormat(destination[1..], out _, "x6", CultureInfo.InvariantCulture);
        written = 7;
        return true;
    }

    /// <summary>
    /// Hex text as the color <see cref="TryHex"/> reads and <see cref="TryColor(uint, Span{char}, out int)"/> writes, into
    /// <paramref name="destination"/>: <c>#rrggbb</c> is the same text in lower case, without reading its number.
    /// </summary>
    public static bool TryColor(string text, Span<char> destination, out int written)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 7 && text[0] == '#' && destination.Length >= 7 && !text.AsSpan(1).ContainsAnyExcept(HexDigits))
        {
            destination[0] = '#';
            Ascii.ToLower(text.AsSpan(1), destination[1..7], out _);
            written = 7;
            return true;
        }

        written = 0;
        return TryHex(text, out var argb) && TryColor(argb, destination, out written);
    }

    /// <summary>
    /// Reads <c>#rgb</c>, <c>#argb</c>, <c>#rrggbb</c> and <c>#aarrggbb</c> as Avalonia's <c>Color.TryParse</c> does,
    /// with no space allowed; other text is not a hex color.
    /// </summary>
    public static bool TryHex(string text, out uint argb)
    {
        ArgumentNullException.ThrowIfNull(text);
        argb = 0;
        if (text.Length is not (4 or 5 or 7 or 9) || text[0] != '#')
        {
            return false;
        }

        var digits = text.AsSpan(1);
        var doubled = digits.Length <= 4;
        var parsed = 0u;
        foreach (var digit in digits)
        {
            var nibble = Nibble(digit);
            if (nibble < 0)
            {
                return false;
            }

            parsed = doubled ? (parsed << 8) | (uint)(nibble * 0x11) : (parsed << 4) | (uint)nibble;
        }

        argb = digits.Length is 3 or 6 ? parsed | 0xFF000000 : parsed;
        return true;
    }

    private static int Nibble(char digit) => digit switch
    {
        >= '0' and <= '9' => digit - '0',
        >= 'a' and <= 'f' => digit - 'a' + 10,
        >= 'A' and <= 'F' => digit - 'A' + 10,
        _ => -1,
    };

    private static double Rounded(double value) => Math.Round(value, 3) + 0.0;
}
