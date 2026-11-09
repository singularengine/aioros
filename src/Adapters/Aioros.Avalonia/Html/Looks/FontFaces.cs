using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Platform;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// A control's <c>FontFamily</c> as CSS <c>font-family</c>, written where it differs from what the control inherits, with
/// Avalonia's default family (Inter) as the fallback, as Avalonia falls back to it; a family whose source is a folder or a
/// file of an assembly's Avalonia resources (<c>avares://Assembly/Assets/Fonts#Geist</c>) also declares each of its font
/// files as an <c>@font-face</c>, its weight range and style read from the file's own tables, served from the
/// assembly's resources at <see cref="Path"/> without Avalonia; a list of such families (Avalonia's composite font, which
/// takes a glyph the first lacks from the next) declares each one's files, so the browser falls back as Avalonia does, and
/// is compared with the inherited family by every family it lists.
/// </summary>
internal static class FontFaces
{
    /// <summary>Where the web host serves an assembly's resource font: <c>{Path}/{assembly}/{path}</c>.</summary>
    public const string Path = "/_aioros/fonts/r";

    private const string Fallback = "Inter,system-ui,sans-serif";

    /// <summary>The family the web's base styles give the body, which the root inherits there.</summary>
    private const string WebDefault = "Inter";

    private static ImmutableDictionary<string, string> _faces = ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    public static void Append(StringBuilder style, Control control, StyleSheet sheet)
    {
        var family = Resolved(control.GetValue(TextElement.FontFamilyProperty));
        var inherited = control.Parent is Control parent and not TopLevel ? Resolved(parent.GetValue(TextElement.FontFamilyProperty)) : null;
        var names = Names(family);
        if (family.Equals(inherited) || (inherited is null ? family.FamilyNames.PrimaryFamilyName == WebDefault : names == Names(inherited)))
        {
            return;
        }

        style.Append(CultureInfo.InvariantCulture, $"font-family:{(names.Length == 0 ? Fallback : names + "," + Fallback)};");
        foreach (var (source, name) in Sources(family))
        {
            var key = source + "#" + name;
            sheet.AddRule("ff" + CssValues.Hash(key), ImmutableInterlocked.GetOrAdd(ref _faces, key, _ => Declare(source, name)));
        }
    }

    /// <summary>
    /// The resource sources of a family and the family each names: its own, or, for a list of families (Avalonia's
    /// composite font, <c>compositefont:avares://…#Geist, avares://…#Symbols</c>), each one's in order; a relative source
    /// (<c>/Assets/Fonts#Geist</c>) is resolved against the XAML file that named it, as Avalonia resolves it, and left out
    /// when there is none.
    /// </summary>
    private static IEnumerable<(Uri Source, string Name)> Sources(FontFamily family)
    {
        if (family.Key?.Source is not { } key)
        {
            yield break;
        }

        if (!key.IsAbsoluteUri || key.Scheme == "avares")
        {
            if (Resource(key, family.Key.BaseUri) is { } source)
            {
                yield return (source, family.FamilyNames.PrimaryFamilyName);
            }

            yield break;
        }

        if (key.Scheme != "compositefont")
        {
            yield break;
        }

        foreach (var member in key.OriginalString["compositefont:".Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var mark = member.LastIndexOf('#');
            if (mark > 0 && Uri.TryCreate(member[..mark], UriKind.RelativeOrAbsolute, out var part) && Resource(part, family.Key.BaseUri) is { } source)
            {
                yield return (source, member[(mark + 1)..]);
            }
        }
    }

    private static Uri? Resource(Uri source, Uri? file) =>
        (source.IsAbsoluteUri ? source : file is { IsAbsoluteUri: true } ? new Uri(file, source) : null) is { Scheme: "avares" } resource ? resource : null;

    private static FontFamily Resolved(FontFamily? family) =>
        family is null || family.FamilyNames.PrimaryFamilyName == FontFamily.DefaultFontFamilyName ? FontManager.Current.DefaultFontFamily : family;

    private static string Names(FontFamily family) =>
        string.Join(',', family.FamilyNames.Where(name => name != FontFamily.DefaultFontFamilyName).Select(name => "\"" + name.Replace("\"", string.Empty, StringComparison.Ordinal) + "\""));

    private static string Declare(Uri source, string family)
    {
        var css = new StringBuilder();
        var files = IsFont(source.AbsolutePath) ? [source] : AssetLoader.GetAssets(source, null).Where(asset => IsFont(asset.AbsolutePath));
        foreach (var file in files.OrderBy(file => file.AbsolutePath, StringComparer.Ordinal))
        {
            using var stream = AssetLoader.Open(file);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            if (Face.Read(bytes.ToArray()) is not { } face || !string.Equals(face.Family, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var format = file.AbsolutePath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ? "opentype" : "truetype";
            var weight = face.Light == face.Heavy ? face.Light.ToString(CultureInfo.InvariantCulture) : string.Create(CultureInfo.InvariantCulture, $"{face.Light} {face.Heavy}");
            css.Append(CultureInfo.InvariantCulture, $"@font-face{{font-family:\"{family}\";src:url({Path}/{file.Host}{file.AbsolutePath}) format('{format}'){(face.Color is { } color ? $" tech({color})" : string.Empty)};font-weight:{weight};font-style:{(face.Italic ? "italic" : "normal")};font-display:swap}}\n");
        }

        return css.ToString();
    }

    public static bool IsFont(string path) =>
        path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A font file's family (the name table's typographic family, or its family), weight range (OS/2's weight class, or the
    /// variation axis <c>wght</c>), style, and the technology of its color glyphs, as CSS's <c>tech()</c> names it, so a
    /// browser that does not draw them skips the source rather than rejecting the file.
    /// </summary>
    private sealed record Face(string Family, int Light, int Heavy, bool Italic, string? Color)
    {
        public static Face? Read(byte[] font)
        {
            var span = font.AsSpan();
            if (span.Length < 12)
            {
                return null;
            }

            var tables = new Dictionary<string, (int Offset, int Length)>(StringComparer.Ordinal);
            int count = BinaryPrimitives.ReadUInt16BigEndian(span[4..]);
            for (var index = 0; index < count && 12 + (index * 16) + 16 <= span.Length; index++)
            {
                var record = span.Slice(12 + (index * 16), 16);
                tables[Encoding.ASCII.GetString(record[..4])] = ((int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]), (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]));
            }

            if (!tables.TryGetValue("name", out var name) || !tables.TryGetValue("OS/2", out var os2) || Name(span.Slice(name.Offset, name.Length)) is not { } family)
            {
                return null;
            }

            var weight = BinaryPrimitives.ReadUInt16BigEndian(span[(os2.Offset + 4)..]);
            var italic = (BinaryPrimitives.ReadUInt16BigEndian(span[(os2.Offset + 62)..]) & 1) != 0;
            var (light, heavy) = tables.TryGetValue("fvar", out var fvar) && Axis(span.Slice(fvar.Offset, fvar.Length)) is { } range ? range : (weight, weight);
            return new Face(family, light, heavy, italic, Colored(span, tables));
        }

        private static string? Colored(ReadOnlySpan<byte> font, Dictionary<string, (int Offset, int Length)> tables) =>
            tables.TryGetValue("COLR", out var colr) && colr.Length >= 2 ? (BinaryPrimitives.ReadUInt16BigEndian(font[colr.Offset..]) == 0 ? "color-COLRv0" : "color-COLRv1")
            : tables.ContainsKey("SVG ") ? "color-SVG"
            : tables.ContainsKey("sbix") ? "color-sbix"
            : tables.ContainsKey("CBDT") ? "color-CBDT"
            : null;

        private static string? Name(ReadOnlySpan<byte> table)
        {
            int count = BinaryPrimitives.ReadUInt16BigEndian(table[2..]);
            int strings = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
            string? family = null;
            foreach (var wanted in (int[])[16, 1])
            {
                for (var index = 0; index < count && family is null; index++)
                {
                    var record = table.Slice(6 + (index * 12), 12);
                    int platform = BinaryPrimitives.ReadUInt16BigEndian(record);
                    int id = BinaryPrimitives.ReadUInt16BigEndian(record[6..]);
                    if (id != wanted || platform is not (0 or 3 or 1))
                    {
                        continue;
                    }

                    var text = table.Slice(strings + BinaryPrimitives.ReadUInt16BigEndian(record[10..]), BinaryPrimitives.ReadUInt16BigEndian(record[8..]));
                    family = platform == 1 ? Encoding.ASCII.GetString(text) : Encoding.BigEndianUnicode.GetString(text);
                }

                if (family is not null)
                {
                    return family;
                }
            }

            return null;
        }

        private static (int Light, int Heavy)? Axis(ReadOnlySpan<byte> table)
        {
            int axes = BinaryPrimitives.ReadUInt16BigEndian(table[4..]);
            int first = BinaryPrimitives.ReadUInt16BigEndian(table[8..]);
            int size = BinaryPrimitives.ReadUInt16BigEndian(table[10..]);
            for (var index = 0; index < axes; index++)
            {
                var axis = table.Slice(first + (index * size), size);
                if (Encoding.ASCII.GetString(axis[..4]) == "wght")
                {
                    return (BinaryPrimitives.ReadInt32BigEndian(axis[4..]) >> 16, BinaryPrimitives.ReadInt32BigEndian(axis[12..]) >> 16);
                }
            }

            return null;
        }
    }
}
