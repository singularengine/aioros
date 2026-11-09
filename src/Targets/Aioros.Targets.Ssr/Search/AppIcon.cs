namespace Aioros.Targets.Ssr.Search;

/// <summary>The app's icon: <c>favicon.svg</c> or <c>favicon.png</c> in its web root, linked from every page's head.</summary>
public static class AppIcon
{
    private static readonly (string Name, string Type)[] Candidates = [("favicon.svg", "image/svg+xml"), ("favicon.png", "image/png")];

    /// <summary>The icon's path, or <see langword="null"/> when the web root holds none.</summary>
    /// <param name="exists">Whether the web root holds a file of that name.</param>
    public static string? Find(Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        return Candidates.FirstOrDefault(candidate => exists(candidate.Name)) is { Name: { } name } ? "/" + name : null;
    }

    internal static string Link(string? icon) => icon is null
        ? string.Empty
        : "<link rel=\"icon\" href=\"" + System.Net.WebUtility.HtmlEncode(icon) + "\" type=\""
            + (icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? Candidates[0].Type : Candidates[1].Type) + "\">\n";
}
