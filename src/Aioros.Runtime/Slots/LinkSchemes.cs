namespace Aioros.Runtime.Slots;

/// <summary>
/// The addresses a link may point to: relative ones and the <c>http</c>, <c>https</c>, <c>mailto</c> and <c>tel</c>
/// schemes; any other (<c>javascript:</c>, <c>data:</c>, <c>vbscript:</c>) becomes <c>#</c>, as Angular's URL sanitizer
/// and React's blocking of <c>javascript:</c> URLs do, so a bound value a user controls cannot run script when followed.
/// The scheme is read as a browser reads it: leading spaces and control characters dropped, tabs and line breaks
/// inside it ignored, its case ignored.
/// </summary>
public static class LinkSchemes
{
    public static string Safe(string? address)
    {
        var end = address is null ? -1 : address.AsSpan().IndexOfAny(":/?#");
        return end < 0 || address![end] != ':' || Allowed(address.AsSpan(0, end)) ? address ?? string.Empty : "#";
    }

    /// <summary>Whether a scheme, read as a browser reads it (control characters, spaces and line breaks dropped, case ignored), is one a link may use.</summary>
    private static bool Allowed(ReadOnlySpan<char> written)
    {
        Span<char> scheme = stackalloc char[6];
        var length = 0;
        foreach (var character in written)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                continue;
            }

            if (length == scheme.Length)
            {
                return false;
            }

            scheme[length++] = char.ToLowerInvariant(character);
        }

        return scheme[..length] is "http" or "https" or "mailto" or "tel";
    }
}
