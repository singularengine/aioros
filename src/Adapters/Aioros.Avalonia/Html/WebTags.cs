using Aioros.Markup;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html;

/// <summary>
/// The elements <see cref="Web.TagProperty"/> may name: the sectioning elements, and the elements Avalonia's automation
/// properties have no vocabulary for, whose browser defaults (margins, padding, list markers, a monospace font) the
/// style sheet resets once with no specificity, so the element keeps Avalonia's look. A legend is floated, so it lays out
/// as the panel's child it is in Avalonia rather than as HTML's rendered legend drawn over the fieldset's border.
/// </summary>
internal static class WebTags
{
    private const string Reset = ":where(blockquote,p,ol,ul,li,pre){margin:0;padding:0;list-style:none}:where(kbd,code,pre,small){font:inherit}:where(fieldset){border:0;margin:0;padding:0;min-width:0}:where(legend){float:left;padding:0}\n";

    private static readonly HashSet<string> Sections = new(StringComparer.Ordinal) { "nav", "header", "footer", "section", "article", "aside", "main" };

    private static readonly HashSet<string> Phrases = new(StringComparer.Ordinal) { "kbd", "code", "pre", "small", "blockquote", "ol", "ul", "li" };

    private static readonly HashSet<string> Boxes = new(StringComparer.Ordinal) { "fieldset" };

    /// <summary>The element a container is written as from its tag, or null; a <c>p</c> is left out, since it cannot hold the blocks a container writes.</summary>
    public static string? Container(Control control, StyleSheet sheet) => Named(control, sheet, tag => Sections.Contains(tag) || Phrases.Contains(tag) || Boxes.Contains(tag));

    /// <summary>The element a text is written as from its tag, or null; a <c>legend</c> only as the first child of a fieldset, as HTML allows it.</summary>
    public static string? Text(Control control, StyleSheet sheet) => Named(control, sheet, tag => tag == "p" || Phrases.Contains(tag) || (tag == "legend" && Leads(control)));

    private static bool Leads(Control control) =>
        control.Parent is Panel panel && Web.GetTag(panel) == "fieldset" && panel.Children.FirstOrDefault() == control;

    private static string? Named(Control control, StyleSheet sheet, Func<string, bool> allowed)
    {
        if (Web.GetTag(control) is not { } tag || !allowed(tag))
        {
            return null;
        }

        if (!Sections.Contains(tag))
        {
            sheet.AddRule("tag-reset", Reset);
        }

        return tag;
    }
}
