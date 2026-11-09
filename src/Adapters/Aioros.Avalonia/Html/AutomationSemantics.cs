using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html;

/// <summary>
/// The HTML element and the ARIA attributes of Avalonia's automation properties, as HTML-AAM and WAI-ARIA map the
/// roles UIA names: a landmark type to its landmark element, a heading level to <c>h1</c>-<c>h6</c> or
/// <c>role="heading"</c>, a live setting to the <c>alert</c> and <c>status</c> live regions, a control type override to
/// its role and a raw accessibility view to <c>aria-hidden</c>.
/// </summary>
internal static class AutomationSemantics
{
    private const string HeadingReset = "h3,h4,h5,h6{margin:0;font-size:inherit;font-weight:inherit}\n";

    /// <summary>Whether the control names anything this class writes, so a wrapper keeps its own element.</summary>
    public static bool Any(Control control) =>
        AutomationProperties.GetLandmarkType(control) is not null || AutomationProperties.GetHeadingLevel(control) > 0
        || AutomationProperties.GetLiveSetting(control) != AutomationLiveSetting.Off || Role(AutomationProperties.GetControlTypeOverride(control)) is not null
        || AutomationProperties.GetAccessibilityView(control) == AccessibilityView.Raw;

    /// <summary>The landmark element of the control's landmark type, or null (a form is a role, since the page is a form).</summary>
    public static string? Landmark(Control control) => AutomationProperties.GetLandmarkType(control) switch
    {
        AutomationLandmarkType.Navigation => "nav",
        AutomationLandmarkType.Main => "main",
        AutomationLandmarkType.Banner => "header",
        AutomationLandmarkType.ContentInfo => "footer",
        AutomationLandmarkType.Complementary => "aside",
        AutomationLandmarkType.Region => "section",
        AutomationLandmarkType.Search => "search",
        _ => null,
    };

    /// <summary>The heading element of a text's heading level from 1 to 6, with the reset of its default look added once; or null.</summary>
    public static string? Heading(Control control, StyleSheet sheet)
    {
        var level = AutomationProperties.GetHeadingLevel(control);
        if (level is < 1 or > 6)
        {
            return null;
        }

        if (level > 2)
        {
            sheet.AddRule("heading-reset", HeadingReset);
        }

        return "h" + level.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The ARIA attributes of a control written as <paramref name="tag"/>, each with its leading space; empty when it names none. A control its prompt announces (<see cref="HtmlContext.Announced"/>) has no live role.</summary>
    public static string Attributes(HtmlContext context, Control control, string tag)
    {
        var role = AutomationProperties.GetLandmarkType(control) == AutomationLandmarkType.Form ? "form"
            : tag is not ("h1" or "h2" or "h3" or "h4" or "h5" or "h6") && AutomationProperties.GetHeadingLevel(control) is > 0 and var level ? string.Create(CultureInfo.InvariantCulture, $"heading\" aria-level=\"{level}")
            : context.Announced.Contains(control) ? Role(AutomationProperties.GetControlTypeOverride(control))
            : AutomationProperties.GetLiveSetting(control) switch
            {
                AutomationLiveSetting.Assertive => "alert",
                AutomationLiveSetting.Polite => "status",
                _ => Role(AutomationProperties.GetControlTypeOverride(control)),
            };
        var hidden = AutomationProperties.GetAccessibilityView(control) == AccessibilityView.Raw ? " aria-hidden=\"true\"" : string.Empty;
        return (role is null ? string.Empty : $" role=\"{role}\"") + hidden;
    }

    private static string? Role(AutomationControlType? type) => type switch
    {
        AutomationControlType.List => "list",
        AutomationControlType.ListItem => "listitem",
        AutomationControlType.Group => "group",
        AutomationControlType.Separator => "separator",
        _ => null,
    };
}
