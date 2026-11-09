using System.Collections.Frozen;

namespace Aioros.Targets.Ssr.Documents;

public static class WebFonts
{
    private const string Latin = "U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+02C6, U+02DA, U+02DC, U+0304, U+0308, U+0329, U+2000-206F, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD";
    private const string LatinExtended = "U+0100-02BA, U+02BD-02C5, U+02C7-02CC, U+02CE-02D7, U+02DD-02FF, U+0304, U+0308, U+0329, U+1D00-1DBF, U+1E00-1E9F, U+1EF2-1EFF, U+2020, U+20A0-20AB, U+20AD-20C4, U+2113, U+2C60-2C7F, U+A720-A7FF";

    private static readonly (string Name, int Weight)[] Weights = [("Thin", 100), ("Light", 300), ("Regular", 400), ("Medium", 500), ("SemiBold", 600), ("Bold", 700)];

    private static readonly FrozenDictionary<string, (int Weight, string Range)> Files = Weights
        .SelectMany(weight => new[] { ($"Inter-{weight.Name}-latin-ext.woff2", (weight.Weight, LatinExtended)), ($"Inter-{weight.Name}-latin.woff2", (weight.Weight, Latin)) })
        .ToFrozenDictionary(file => file.Item1, file => file.Item2, StringComparer.Ordinal);

    public static string Css(string path) =>
        string.Concat(Files.OrderBy(file => file.Value.Weight).ThenBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            $"@font-face{{font-family:Inter;src:url({path}/{file.Key}) format('woff2');font-weight:{file.Value.Weight};font-display:swap;unicode-range:{file.Value.Range}}}\n"))
        + "body{font-family:Inter,system-ui,-apple-system,\"Segoe UI\",Roboto,sans-serif}\n";

    public static async ValueTask<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        if (!Files.ContainsKey(name) || typeof(WebFonts).Assembly.GetManifestResourceStream($"Aioros.Targets.Ssr.Fonts.{name}") is not { } stream)
        {
            return null;
        }

        await using (stream)
        {
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, cancellationToken);
            return bytes.ToArray();
        }
    }
}
