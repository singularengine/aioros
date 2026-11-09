using Aioros.Markup;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Writers;

internal static class CompositeWriters
{
    public static void WriteSplitView(HtmlContext context, SplitView split, ParentKind parent)
    {
        var id = Web.GetId(split) ?? "pane";
        var width = split.OpenPaneLength;
        var breakpoint = Web.GetCollapseBelow(split);
        var background = CssValues.Brush(split.PaneBackground) ?? "transparent";
        var pane = "pv" + CssValues.Hash($"{width}|{breakpoint}|{background}");
        context.Sheet.AddRule(pane, PaneCss(pane, width, breakpoint, background));
        var style = context.Box(split, parent);
        style.Append("display:flex;flex-direction:row;");
        context.Plan.Literal($"<div class=\"{context.Classes(split, null, style)}\">");
        context.Plan.Literal($"<aside popover id=\"{CssValues.Encode(id)}\" class=\"{pane}\">");
        if (split.Pane is Control content)
        {
            context.Write(content, ParentKind.Single);
        }

        context.Plan.Literal("</aside><div class=\"c\" style=\"flex:1 1 0;min-width:0\">");
        if (split.Content is Control main)
        {
            context.Write(main, ParentKind.Single);
        }

        context.Plan.Literal("</div></div>");
    }

    public static string PaneCss(string pane, double width, double breakpoint, string background) =>
        $".{pane}{{background:{background};color:inherit;margin:0;border:0;padding:0;flex-direction:column;overflow:auto;width:{CssValues.Px(width)}}}\n" +
        $"@media (min-width:{CssValues.Num(breakpoint + 0.02)}px){{.{pane}{{display:flex;position:sticky;top:0;height:100vh;flex:0 0 {CssValues.Px(width)};inset:auto}}}}\n" +
        $"@media (max-width:{CssValues.Px(breakpoint)}){{.{pane}{{position:fixed;inset:0 auto 0 0;height:100dvh;width:min({CssValues.Px(width)},85vw);transform:translateX(-100%);" +
        "transition:transform .26s cubic-bezier(.33,1,.68,1),display .26s allow-discrete,overlay .26s allow-discrete;box-shadow:0 0 40px rgba(0,0,0,.25)}" +
        $".{pane}:popover-open{{display:flex;transform:none}}.{pane}::backdrop{{background:rgba(0,0,0,.35)}}}}\n" +
        $"@starting-style{{.{pane}:popover-open{{transform:translateX(-100%)}}}}\n";

    public static void WriteExpander(HtmlContext context, Expander expander, ParentKind parent)
    {
        var style = context.Box(expander, parent);
        BoxStyles.Font(style, expander.FontSize, expander.FontWeight, expander.Foreground);
        BoxStyles.Fill(style, expander.Background, expander.BorderBrush, expander.BorderThickness, expander.CornerRadius, default);
        if (ExpanderWriters.Header(expander) is { } toggle)
        {
            ExpanderWriters.WriteThemed(context, expander, toggle, style);
            return;
        }

        context.Plan.Literal($"<details class=\"{context.Classes(expander, "c", style)}\"");
        ExpanderWriters.Open(context, expander);
        context.Plan.Literal(ExpanderWriters.Marks(context, expander));
        var summary = new StringBuilder();
        summary.Append(expander.Padding != default ? $"padding:{BoxStyles.Edges(expander.Padding)};" : string.Empty);
        context.Plan.Literal($"><summary class=\"{context.Sheet.Class(null, summary)}\">{InputWriters.IconSvg("chevron-right", 14, 2, "chev")}");
        context.Content(expander.Header);
        context.Plan.Literal("</summary>");
        ExpanderWriters.WriteField(context, expander);
        context.Plan.Literal("<div class=\"c\">");
        context.Content(expander.Content);
        context.Plan.Literal("</div></details>");
    }
}
