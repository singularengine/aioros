using System.Collections.Frozen;
using Aioros.Pages;
using Gallery.Models;

namespace Gallery.Pages;

public static class ShellLoader
{
    internal static readonly FrozenDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["/controls/text"] = "Text",
        ["/controls/buttons"] = "Buttons",
        ["/controls/ranges"] = "Ranges and dates",
        ["/controls/items"] = "Items",
        ["/controls/navigation"] = "Navigation",
        ["/controls/media"] = "Media",
        ["/controls/vectors"] = "Vectors",
        ["/controls/charts"] = "Charts",
        ["/controls/layout"] = "Layout",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static Section Load(PageRequest request) => new(Names.GetValueOrDefault(request.Path.TrimEnd('/'))
        ?? (request.Path.StartsWith("/parity", StringComparison.OrdinalIgnoreCase) ? "Parity report" : "Overview"));
}
