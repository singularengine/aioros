using Aioros.Markup;
using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Pages;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>What the navigator reads from a path and writes onto the shown page's links and theme.</summary>
internal static class DesktopRequests
{
    public static bool IsLocal(string href) => href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal);

    public static ImmutableDictionary<string, string> Query(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(pair => Unescape(pair[0]), StringComparer.OrdinalIgnoreCase)
            .ToImmutableDictionary(group => group.Key, group => string.Join(',', group.Select(pair => pair.Length > 1 ? Unescape(pair[1]) : string.Empty)), StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a shared layout's own route values (its folders' parameters) are the same in both requests.</summary>
    public static bool SameValues(string layout, PageRequest before, PageRequest after) =>
        layout.Split('/').Where(static folder => folder.StartsWith('[') && folder.EndsWith(']'))
            .Select(static folder => folder.Trim('[', ']').TrimStart('.').Split(':')[0])
            .All(name => before.RouteValues.GetValueOrDefault(name) == after.RouteValues.GetValueOrDefault(name));

    public static void SwitchTheme(string name)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = name switch
        {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            "system" => ThemeVariant.Default,
            _ => application.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark,
        };
    }

    public static void MarkCurrent(Control? root, string route)
    {
        if (root is null)
        {
            return;
        }

        foreach (var link in root.GetLogicalDescendants().OfType<Control>().Prepend(root).Where(control => Web.GetHref(control) is not null))
        {
            ((IPseudoClasses)link.Classes).Set(":current", Runtime.Rendering.CurrentNode.IsCurrent(route, Web.GetHref(link), Web.GetExact(link)));
        }
    }

    /// <summary>Keeps relative links in the window and hides "open in the desktop app" links, since the window is the app.</summary>
    public static void KeepLinksInside(Control root)
    {
        foreach (var opener in root.GetLogicalDescendants().OfType<Control>().Prepend(root).Where(Web.GetOpenInApp))
        {
            opener.IsVisible = false;
        }

        foreach (var link in root.GetLogicalDescendants().OfType<HyperlinkButton>().Where(link => link.NavigateUri is { IsAbsoluteUri: false }))
        {
            Web.SetHref(link, link.NavigateUri!.OriginalString);
            link.SetCurrentValue(HyperlinkButton.NavigateUriProperty, null);
        }
    }

    private static string Unescape(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
