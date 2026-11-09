using System.Globalization;
using System.Text;

namespace Aioros.Avalonia.Html.Looks;

internal sealed class StyleSheet
{
    private readonly StringBuilder _css = new();
    private readonly HashSet<string> _rules = [];
    private readonly SortedSet<double> _stackBelow = [];
    private readonly SortedDictionary<string, (double Width, double Gap)> _stackGaps = new(StringComparer.Ordinal);
    private readonly SortedSet<double> _hideAbove = [];
    private readonly SortedSet<double> _hideBelow = [];

    public bool AddRule(string name, string css)
    {
        if (!_rules.Add(name))
        {
            return false;
        }

        _css.Append(css);
        return true;
    }

    public string Class(string? baseClasses, StringBuilder style)
    {
        var css = style.ToString();
        if (css.Length == 0)
        {
            return baseClasses ?? string.Empty;
        }

        var name = "k" + CssValues.Hash(css);
        AddRule(name, css.Contains("animation:", StringComparison.Ordinal) ? $".{name}{{{css}}}\n{StillUnderReducedMotion(name)}" : $".{name}{{{css}}}\n");
        return CssValues.Join(baseClasses, name);
    }

    /// <summary>
    /// Runs an element's animations at once, once, for a visitor who prefers reduced motion (MOTION-21): collapsing them rather
    /// than removing them keeps the end their fill holds, the visible state of an entrance.
    /// </summary>
    private static string StillUnderReducedMotion(string name) =>
        $"@media (prefers-reduced-motion:reduce){{.{name}{{animation-duration:0s!important;animation-delay:0s!important;animation-iteration-count:1!important}}}}\n";

    /// <summary>The class of a look's state rules, written together in their order so that each later rule wins over the earlier ones it ties with.</summary>
    public string States(Look look)
    {
        if (look.States.Count == 0)
        {
            return string.Empty;
        }

        var rules = string.Concat(look.States.Select(state => $":{state.Selector}{{{state.Rule.Replace(";", "!important;", StringComparison.Ordinal)}}}\n"));
        var name = "s" + CssValues.Hash(rules);
        AddRule(name, string.Concat(rules.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(rule => $".{name}{rule}\n")));
        return name;
    }

    /// <summary>
    /// The class of state rules given with their full selector fragments (<c>:hover</c>, <c>[aria-invalid="true"]</c>,
    /// <c>:has(>input:checked)</c>), written together in their order with <c>!important</c> over the element's own look.
    /// </summary>
    public string Rules(IEnumerable<(string Selector, string Rule)> rules)
    {
        var css = string.Concat(rules.Where(rule => rule.Rule.Length > 0).Select(rule => $"{rule.Selector}{{{rule.Rule.Replace(";", "!important;", StringComparison.Ordinal)}}}\n"));
        if (css.Length == 0)
        {
            return string.Empty;
        }

        var name = "s" + CssValues.Hash(css);
        AddRule(name, string.Concat(css.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(rule => $".{name}{rule}\n")));
        return name;
    }

    private const string Contents = "[style*=\"display:contents\"]";

    public string StackBelow(double width)
    {
        _stackBelow.Add(width);
        return $"sb{(int)width}";
    }

    /// <summary>The class that separates a grid's cells stacked below <paramref name="width"/> by its column spacing.</summary>
    public string StackGap(double width, double gap)
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"sg{(int)width}-{(int)Math.Round(gap * 100)}");
        _stackGaps[name] = (width, gap);
        return name;
    }

    public string HideAbove(double width)
    {
        _hideAbove.Add(width);
        return $"ha{(int)width}";
    }

    public string HideBelow(double width)
    {
        _hideBelow.Add(width);
        return $"hb{(int)width}";
    }

    public string Build(string colorScheme)
    {
        var css = new StringBuilder(_css.ToString());
        foreach (var width in _stackBelow)
        {
            var name = "sb" + ((int)width).ToString(CultureInfo.InvariantCulture);
            css.Append(CultureInfo.InvariantCulture, $"@media (max-width:{CssValues.Px(width)}){{.{name}{{grid-template-columns:minmax(0,1fr)!important;grid-auto-flow:row!important}}.{name}>*,.{name}>{Contents}>*,.{name}>{Contents}>{Contents}>*{{grid-column:1/-1!important;grid-row:auto!important}}}}\n");
        }

        foreach (var (name, (width, gap)) in _stackGaps)
        {
            css.Append(CultureInfo.InvariantCulture, $"@media (max-width:{CssValues.Px(width)}){{.{name}{{row-gap:{CssValues.Px(gap)}!important}}}}\n");
        }

        foreach (var width in _hideAbove)
        {
            css.Append(CultureInfo.InvariantCulture, $"@media (min-width:{CssValues.Num(width + 0.02)}px){{.ha{(int)width}{{display:none!important}}}}\n");
        }

        foreach (var width in _hideBelow)
        {
            css.Append(CultureInfo.InvariantCulture, $"@media (max-width:{CssValues.Px(width)}){{.hb{(int)width}{{display:none!important}}}}\n");
        }

        css.Append(CultureInfo.InvariantCulture, $":root{{color-scheme:{colorScheme}}}\n");
        return css.ToString();
    }
}
