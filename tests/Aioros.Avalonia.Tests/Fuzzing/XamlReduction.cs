using System.Xml.Linq;

namespace Aioros.Avalonia.Tests.Fuzzing;

/// <summary>
/// Reduces a page that fails to the smallest page that still fails (delta debugging, Zeller 2002, one removal at a time):
/// an element, an attribute or a style is removed whenever the page without it still fails, until no removal keeps it failing.
/// </summary>
public static class XamlReduction
{
    public static async Task<string> ReduceAsync(string body, Func<string, Task<bool>> fails)
    {
        ArgumentNullException.ThrowIfNull(fails);
        var page = XElement.Parse(Wrapped(body));
        var reduced = true;
        while (reduced)
        {
            reduced = false;
            foreach (var removal in Removals(page))
            {
                var candidate = new XElement(page);
                removal(candidate);
                if (await fails(Unwrapped(candidate)))
                {
                    page = candidate;
                    reduced = true;
                    break;
                }
            }
        }

        return Unwrapped(page);
    }

    private static IEnumerable<Action<XElement>> Removals(XElement page)
    {
        var elements = page.Descendants().ToList();
        for (var index = elements.Count - 1; index >= 1; index--)
        {
            var at = index;
            yield return candidate => candidate.Descendants().ElementAt(at).Remove();
        }

        for (var index = 0; index < elements.Count; index++)
        {
            var at = index;
            foreach (var name in elements[index].Attributes().Where(static attribute => !attribute.IsNamespaceDeclaration).Select(static attribute => attribute.Name).ToList())
            {
                yield return candidate => candidate.Descendants().ElementAt(at).Attribute(name)?.Remove();
            }
        }
    }

    private static string Wrapped(string body) =>
        $"""<Page xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev">{body}</Page>""";

    private static string Unwrapped(XElement page) =>
        string.Concat(page.Nodes().Select(static node => node.ToString(SaveOptions.DisableFormatting)))
            .Replace(" xmlns=\"https://github.com/avaloniaui\"", string.Empty, StringComparison.Ordinal)
            .Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", string.Empty, StringComparison.Ordinal)
            .Replace(" xmlns:a=\"https://aioros.dev\"", string.Empty, StringComparison.Ordinal);
}
