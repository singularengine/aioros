using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Vectors;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.Avalonia.Html.Writers;

internal static class LayoutWriters
{
    private const double GroupBoxGutter = 6;

    public static void WriteSeparator(HtmlContext context, Separator separator, ParentKind parent)
    {
        var style = context.Box(separator, parent);
        style.Append(separator.Margin == default ? "margin:0;" : string.Empty);
        style.Append("border:0;flex-shrink:0;");
        style.Append(CssValues.Paint(separator.Background) is { } background ? $"background:{background};" : string.Empty);
        context.Plan.Literal("<hr");
        context.Root(separator, null, style);
        context.Plan.Literal(">");
    }

    public static void WriteGroupBox(HtmlContext context, GroupBox group, ParentKind parent)
    {
        if (context.Plan.Planning && LocalBindings.IsBound(group, HeaderedContentControl.HeaderProperty))
        {
            context.Plan.Fail("GroupBox.Header is bound");
        }

        var border = group.BorderThickness;
        var padding = new Thickness(
            GroupBoxGutter + group.Padding.Left - border.Left,
            group.Padding.Top,
            GroupBoxGutter + group.Padding.Right - border.Right,
            GroupBoxGutter + group.Padding.Bottom - border.Bottom);
        var style = context.Box(group, parent);
        style.Append(group.Margin == default ? "margin:0;" : string.Empty);
        style.Append("min-width:0;display:flex;flex-direction:column;");
        BoxStyles.Fill(style, group.Background, group.BorderBrush, border, group.CornerRadius, padding);
        context.Plan.Literal("<fieldset");
        context.Root(group, null, style);
        context.Plan.Literal(">");
        if (group.HeaderPresenter?.Child is { } header)
        {
            var legend = new System.Text.StringBuilder();
            legend.Append(CultureInfo.InvariantCulture, $"padding:0 3px;margin-left:{CssValues.Px(GroupBoxGutter - border.Left - padding.Left)};");
            context.Plan.Literal($"<legend class=\"{context.Sheet.Class(null, legend)}\">");
            context.Write(header, ParentKind.Row);
            context.Plan.Literal("</legend>");
        }

        if (group.Presenter?.Child is { } content)
        {
            context.Write(content, ParentKind.Column);
        }

        context.Plan.Literal("</fieldset>");
    }

    public static void WriteThemeScope(HtmlContext context, ThemeVariantScope scope, ParentKind parent)
    {
        var style = context.Box(scope, parent);
        style.Append(scope.ActualThemeVariant == ThemeVariant.Dark ? "color-scheme:dark;" : "color-scheme:light;");
        style.Append(CssValues.Brush(scope.GetValue(TextElement.ForegroundProperty)) is { } color ? $"color:{color};" : string.Empty);
        WriteDecorator(context, scope, style);
    }

    public static void WriteAcrylic(HtmlContext context, ExperimentalAcrylicBorder acrylic, ParentKind parent)
    {
        var style = context.Box(acrylic, parent);
        if (acrylic.Material is IExperimentalAcrylicMaterial material)
        {
            var tint = CssValues.Color(material.TintColor);
            style.Append(CultureInfo.InvariantCulture, $"background:linear-gradient({tint},{tint}),{CssValues.Color(material.MaterialColor)};backdrop-filter:blur(30px);");
        }

        BoxStyles.Fill(style, null, null, default, acrylic.CornerRadius, acrylic.Padding, resetBorder: false);
        WriteDecorator(context, acrylic, style);
    }

    public static void WriteLayoutTransform(HtmlContext context, LayoutTransformControl control, ParentKind parent)
    {
        if (control.Child is not { } child)
        {
            return;
        }

        if (control.LayoutTransform is not { Value: { IsIdentity: false } matrix })
        {
            context.Write(child, parent);
            return;
        }

        control.Measure(Size.Infinity);
        var size = control.DesiredSize.Deflate(control.Margin);
        var style = context.Box(control, parent, size: false);
        style.Append(CultureInfo.InvariantCulture, $"width:{CssValues.Px(size.Width)};height:{CssValues.Px(size.Height)};display:grid;place-items:center;");
        var transform = new System.Text.StringBuilder($"transform:{CssValues.Matrix(matrix)};");
        context.Plan.Literal($"<div class=\"{context.Classes(control, null, style)}\"><div class=\"{context.Sheet.Class(null, transform)}\">");
        context.Write(child, ParentKind.Single);
        context.Plan.Literal("</div></div>");
    }

    public static void WriteDrawing(HtmlContext context, Control control, ParentKind parent)
    {
        var style = context.Box(control, parent, size: false);
        style.Append("flex-shrink:0;");
        var name = global::Avalonia.Automation.AutomationProperties.GetName(control);
        var label = string.IsNullOrEmpty(name) ? " aria-hidden=\"true\"" : $" role=\"img\" aria-label=\"{CssValues.Encode(name)}\"";
        if (Drawings.Svg(control, $"{label} class=\"{context.Classes(control, null, style)}\"") is { } svg)
        {
            context.Plan.Literal(svg);
            return;
        }

        var png = Drawings.Render(control);
        context.Plan.Literal($"<img src=\"data:image/png;base64,{png.Base64}\" alt=\"{CssValues.Encode(name)}\" width=\"{png.Width}\" height=\"{png.Height}\" class=\"{context.Classes(control, null, style)}\">");
    }

    private static void WriteDecorator(HtmlContext context, Decorator decorator, System.Text.StringBuilder style)
    {
        context.Plan.Literal("<div");
        context.Root(decorator, "c", style);
        context.Plan.Literal(">");
        if (decorator.Child is { } child)
        {
            context.Write(child, ParentKind.Single);
        }

        context.Plan.Literal("</div>");
    }
}
