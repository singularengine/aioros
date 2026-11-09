using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// The look of a toggle button written as a label around a hidden checkbox, resolved from its control theme for each of
/// its classes as Avalonia's styles give it: its content presenter's fill, border, radius, color and shadow at rest
/// (unchecked) and in each combination of pointed at, pressed, checked and focused from the keyboard, so an
/// <c>outline</c> class's hover can differ from the default one's.
/// </summary>
internal static class ToggleLooks
{
    private static readonly string[] Painted = ["background", "border-color", "border-width", "border-radius", "color", "box-shadow", "text-decoration"];

    /// <summary>The states a toggle's label shows on the web: hovered, pressed, checked and focused, or only checked while it is disabled.</summary>
    public static IReadOnlyList<WebState> States(Control toggle) => toggle.IsEffectivelyEnabled
        ? [new(":pointerover", ":hover"), new(":pressed", ":active"), new(":checked", ":has(>input:checked)"), new(":focus-visible", ":has(>input:focus-visible)")]
        : [new(":checked", ":has(>input:checked)")];

    /// <summary>The label's look and the class of its state rules, or null when the theme has no content presenter to read.</summary>
    public static (string Css, string States)? Of(HtmlContext context, ContentControl toggle)
    {
        ThemeLooks.ExpandTemplates(toggle);
        if (toggle.GetVisualDescendants().OfType<Control>().FirstOrDefault(part => part.Name == "PART_ContentPresenter") is not { } part)
        {
            return null;
        }

        var look = StateLooks.Resolve(toggle, States(toggle), () => Padded(ThemeLooks.Snapshot(toggle, part), toggle));
        var rules = context.Sheet.Rules(look.Rules.Select(rule => (rule.Selector, string.Concat(rule.Rule.Select(entry => $"{entry.Key}:{entry.Value};")))));
        return (StateLooks.AtRest(toggle, States(toggle), () => Rest(toggle, part, look, context.MinHeight(toggle))), CssValues.Join(rules, Focus(context, toggle)));
    }

    /// <summary>The look of a toggle's state with its padding, which a style of a state may change (<c>ToggleButton:checked</c> setting <c>Padding</c>).</summary>
    private static Dictionary<string, string> Padded(Dictionary<string, string> look, ContentControl toggle)
    {
        look["padding"] = BoxStyles.Edges(toggle.Padding);
        return look;
    }

    private static string Rest(ContentControl toggle, Control part, StateLook look, double minHeight)
    {
        var css = new StringBuilder();
        foreach (var property in Painted)
        {
            if (look.Rest.TryGetValue(property, out var value) && value is not ("none" or "inherit"))
            {
                css.Append(CultureInfo.InvariantCulture, $"{property}:{value};");
            }
        }

        css.Append("border-style:solid;");
        css.Append(toggle.Padding != default ? $"padding:{BoxStyles.Edges(toggle.Padding)};" : string.Empty);
        css.Append(minHeight > 0 ? $"min-height:{CssValues.Px(minHeight)};" : string.Empty);
        css.Append(part.GetValue(TextBlock.LineHeightProperty) is var line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        css.Append(part.Transitions is { Count: > 0 } transitions && MotionStyles.TransitionList(transitions) is { } moves ? $"transition:{moves};" : string.Empty);
        css.Append("position:relative;display:inline-flex;align-items:center;cursor:pointer;user-select:none;");
        css.Append(toggle.HorizontalContentAlignment switch
        {
            HorizontalAlignment.Center => "justify-content:center;",
            HorizontalAlignment.Right => "justify-content:flex-end;",
            _ => string.Empty,
        });
        return css.ToString();
    }

    /// <summary>The browser's focus outline on the label while its input has the keyboard focus, unless the theme draws no focus adorner.</summary>
    private static string Focus(HtmlContext context, TemplatedControl toggle)
    {
        if ((toggle.IsSet(Control.FocusAdornerProperty) && toggle.FocusAdorner is null) || !toggle.IsEffectivelyEnabled)
        {
            return string.Empty;
        }

        context.Sheet.AddRule("tgf", ".tgf:has(>input:focus-visible){outline:2px solid;outline-offset:1px}\n");
        return "tgf";
    }
}
