using System.Buffers;
using System.Globalization;
using System.IO.Hashing;
using System.Text;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// Writes the rule sets slot renderers contribute to a page (<see cref="PageStyles"/>) as style elements named by their
/// key and the hash of their content (<c>data-aioros-style="{key}:{hash}"</c>): in a document's head, and before the
/// markup of an answer that is not a document, only in the versions the browser does not hold, as styled-components'
/// registry inserts a rule set only when its hash is new.
/// </summary>
internal static class KeyedStyles
{
    /// <summary>The header on the script's posts naming the versions of the rule sets the page holds, separated by spaces.</summary>
    public const string Header = "X-Aioros-Styles";

    /// <summary>A rule set's version: its key and the hash of its content.</summary>
    public static string Version(string key, string css) =>
        key + ":" + XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(css)).ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>The versions the script's request says the page holds.</summary>
    public static HashSet<string> Held(HttpContext http) =>
        [.. http.Request.Headers[Header].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Writes the rule sets whose versions are not among <paramref name="held"/> (all of them without it); <see langword="true"/> when one was written.</summary>
    public static bool Write(IBufferWriter<byte> writer, PageStyles? styles, Func<string, bool>? held = null)
    {
        var written = false;
        foreach (var (key, css) in styles?.All ?? [])
        {
            var version = Version(key, css);
            if (held?.Invoke(version) == true)
            {
                continue;
            }

            var output = new HtmlOutput(writer);
            output.WriteUtf8("<style data-css data-aioros-style=\""u8);
            output.WriteRaw(version);
            output.WriteUtf8("\">"u8);
            output.WriteRaw(css.Replace("</", "<\\/", StringComparison.Ordinal));
            output.WriteUtf8("</style>"u8);
            written = true;
        }

        return written;
    }

    /// <summary>The rule sets as their style elements, all of them, for the <c>s</c> member of a dynamics answer; none when there are none.</summary>
    public static string? Elements(PageStyles styles)
    {
        var buffer = new ArrayBufferWriter<byte>();
        return Write(buffer, styles) ? Encoding.UTF8.GetString(buffer.WrittenSpan) : null;
    }
}
