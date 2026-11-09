using Aioros.Markup;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Writers;

internal static class TextWriters
{
    private static readonly AvaloniaProperty[] InlineStyles =
    [
        TextElement.FontSizeProperty, TextElement.FontWeightProperty, TextElement.FontStyleProperty, TextElement.ForegroundProperty,
        TextElement.BackgroundProperty, Inline.TextDecorationsProperty,
    ];


    public static void WriteTextBlock(HtmlContext context, TextBlock text, ParentKind parent) =>
        WriteText(context, text, parent, null, null, null);

    public static void WriteSelectable(HtmlContext context, SelectableTextBlock text, ParentKind parent)
    {
        string? selection = null;
        if (CssValues.Brush(text.SelectionBrush) is { } background)
        {
            var foreground = CssValues.Brush(text.SelectionForegroundBrush) is { } color ? $";color:{color}" : string.Empty;
            selection = "sl" + CssValues.Hash(background + foreground);
            context.Sheet.AddRule(selection, $".{selection}::selection{{background:{background}{foreground}}}\n");
        }

        WriteText(context, text, parent, selection, "user-select:text;cursor:text;", null);
    }

    public static void WriteAccessText(HtmlContext context, AccessText text, ParentKind parent) =>
        WriteText(context, text, parent, null, null, Slots.FormatOf("access"));

    public static void WriteLabel(HtmlContext context, Label label, ParentKind parent)
    {
        var style = context.Box(label, parent);
        BoxStyles.Font(style, label.FontSize, label.FontWeight, label.Foreground);
        BoxStyles.Fill(style, label.Background, label.BorderBrush, label.BorderThickness, label.CornerRadius, label.Padding);
        ThemeLooks.ExpandTemplates(label);
        style.Append(ItemParts.Presenter(label, "PART_ContentPresenter")?.GetValue(TextBlock.LineHeightProperty) is { } line and > 0 ? $"line-height:{CssValues.Px(line)};" : string.Empty);
        context.Plan.Literal("<label");
        context.Attributes(label);
        var bound = context.Plan.Planning && label.Content is not Control && context.Plan.ReadsModel(label, ContentControl.ContentProperty);
        var (text, key) = label.Content is string content ? StripAccessKey(content) : (null, null);
        if (label.Target is Control target && Web.GetId(target) is { } id)
        {
            context.Plan.Literal($" for=\"{CssValues.Encode(id)}\"");
            context.Plan.Literal(key is { } access && !LocalBindings.IsBound(label, ContentControl.ContentProperty) ? $" accesskey=\"{CssValues.Encode(char.ToLowerInvariant(access).ToString())}\"" : string.Empty);
        }

        context.Root(label, null, style, attributes: false);
        context.Plan.Literal(">");
        if (bound)
        {
            if (!ContentTexts.Write(context.Plan, label, access: true))
            {
                context.Plan.Fail("Label.Content is bound to a path the plan cannot follow");
            }
        }
        else if (text is not null)
        {
            context.Plan.Text(text);
        }
        else
        {
            context.Content(label.Content);
        }

        context.Plan.Literal("</label>");
    }

    public static (string Text, char? Key) StripAccessKey(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (string.Empty, null);
        }

        var marker = AccessKeyMarker(text);
        char? key = null;
        if (marker >= 0 && marker < text.Length - 1)
        {
            key = text[marker + 1];
            text = text.Remove(marker, 1);
        }

        return (text.Replace("__", "_", StringComparison.Ordinal), key);
    }

    public static void WriteValue(HtmlContext context, AvaloniaObject target, AvaloniaProperty property, Format format)
    {
        if (context.Plan.Planning && target is AccessText && ContentTexts.Owner(context.Plan, target, property) is { } owner && ContentTexts.Write(context.Plan, owner.Owner, owner.Property, access: true))
        {
            return;
        }

        if (context.Plan.Planning && context.Plan.BoundPath(target, property) is { } path)
        {
            var slot = Slots.Path(path);
            context.Plan.Add(new ValueNode(slot.Getter, format.Formatter) { Source = slot.Spec, FormatSpec = format.Spec });
            return;
        }

        context.Plan.Text(format.Apply(target.GetValue(property)));
    }

    /// <summary>The text's <see cref="Web.TextWrapProperty"/> as CSS's <c>text-wrap</c>, when it is a value CSS knows.</summary>
    internal static string TextWrap(Control control) => Web.GetTextWrap(control) is "balance" or "pretty" ? $"text-wrap:{Web.GetTextWrap(control)};" : string.Empty;

    private static int AccessKeyMarker(string text)
    {
        var index = text.IndexOf('_', StringComparison.Ordinal);
        while (index >= 0 && index + 1 < text.Length && text[index + 1] == '_')
        {
            index = text.IndexOf('_', index + 2);
        }

        return index;
    }

    private static void WriteText(HtmlContext context, TextBlock text, ParentKind parent, string? baseClass, string? extra, Format? format)
    {
        var tag = AutomationSemantics.Heading(text, context.Sheet) ?? WebTags.Text(text, context.Sheet) ?? (text.Classes.Contains("h1") ? "h1" : text.Classes.Contains("h2") ? "h2" : "div");
        var style = context.Box(text, parent);
        BoxStyles.Font(style, LocalBindings.IsBound(text, TextElement.FontSizeProperty) ? double.NaN : text.FontSize, text.FontWeight, LocalBindings.IsBound(text, TextElement.ForegroundProperty) || context.ColorFromItem(text) ? null : text.Foreground, text.Bounds.Size);
        style.Append(Typeface(text));
        style.Append(text.TextWrapping == TextWrapping.NoWrap ? "white-space:pre;" : "white-space:pre-wrap;overflow-wrap:anywhere;");
        style.Append(TextWrap(text));
        style.Append(text.Padding == default || LocalBindings.IsBound(text, TextBlock.PaddingProperty) ? string.Empty : $"padding:{BoxStyles.Edges(text.Padding)};");
        style.Append(Clamp(text));
        style.Append(text.TextAlignment switch
        {
            TextAlignment.Center => "text-align:center;",
            TextAlignment.Right => "text-align:right;",
            _ => string.Empty,
        });
        style.Append(double.IsNaN(text.LineHeight) ? string.Empty : $"line-height:{CssValues.Px(text.LineHeight)};");
        style.Append(TextDecoration(text));
        style.Append(Features(text));
        style.Append(extra);
        context.Plan.Literal($"<{tag}{AutomationSemantics.Attributes(context, text, tag)}");
        context.Root(text, Shimmers.Mark(text, context.Sheet, baseClass), style);
        context.Plan.Literal(">");
        if (text.Inlines is { Count: > 0 } inlines)
        {
            WriteInlines(context, inlines);
        }
        else if (format is null)
        {
            context.Plan.Value(text, TextBlock.TextProperty);
        }
        else
        {
            WriteValue(context, text, TextBlock.TextProperty, format.Value);
        }

        context.Plan.Literal($"</{tag}>");
    }

    /// <summary>The OpenType features the text turns on or off (tabular digits for a progress, shadcn/ui's <c>tabular-nums</c>), as CSS's <c>font-feature-settings</c>.</summary>
    private static string Features(TextBlock text) =>
        text.FontFeatures is { Count: > 0 } features
            ? "font-feature-settings:" + string.Join(',', features.Select(static feature => string.Create(CultureInfo.InvariantCulture, $"\"{feature.Tag}\" {feature.Value}"))) + ";"
            : string.Empty;

    /// <summary>The text's own decorations or those its template's presenter gives it, with the underline's offset.</summary>
    private static string TextDecoration(TextBlock text) =>
        text.TextDecorations is { Count: > 0 } decorations
            ? $"text-decoration:{Decorations(decorations)};" + (Underlines.Offset(text, decorations) is not "auto" and var offset ? $"text-underline-offset:{offset};" : string.Empty)
            : string.Empty;

    /// <summary>
    /// How a text is cut: a wrapping text with <c>MaxLines</c> after that many lines, with an ellipsis where it trims
    /// (Tailwind's <c>line-clamp</c>), else its trimming on its one line.
    /// </summary>
    private static string Clamp(TextBlock text)
    {
        var trims = text.TextTrimming != TextTrimming.None;
        if (text.MaxLines <= 0 || text.TextWrapping == TextWrapping.NoWrap)
        {
            return trims ? "overflow:hidden;text-overflow:ellipsis;" : string.Empty;
        }

        return trims
            ? string.Create(CultureInfo.InvariantCulture, $"display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:{text.MaxLines};overflow:hidden;")
            : string.Create(CultureInfo.InvariantCulture, $"max-height:{text.MaxLines}lh;overflow:hidden;");
    }

    /// <summary>The text's letter spacing, added after each character as Avalonia adds it, and its italic or oblique face.</summary>
    private static string Typeface(TextBlock text) =>
        (text.LetterSpacing != 0 ? $"letter-spacing:{CssValues.Px(text.LetterSpacing)};" : string.Empty) + text.FontStyle switch
        {
            FontStyle.Italic => "font-style:italic;",
            FontStyle.Oblique => "font-style:oblique;",
            _ => string.Empty,
        };

    private static void WriteInlines(HtmlContext context, InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            WriteInline(context, inline);
        }
    }

    private static void WriteInline(HtmlContext context, Inline inline)
    {
        if (context.Plan.Planning && InlineStyles.FirstOrDefault(property => context.Plan.ReadsModel(inline, property)) is { } bound)
        {
            context.Plan.Fail($"{inline.GetType().Name}.{bound.Name} is bound");
        }

        if (inline is Run or Span && LinkAddresses.Links(inline))
        {
            WriteLinked(context, inline);
            return;
        }

        switch (inline)
        {
            case Run run:
                WriteRun(context, run);
                break;
            case LineBreak:
                context.Plan.Literal("<br>");
                break;
            case InlineUIContainer { Child: { } child } container:
                context.Plan.Literal($"<span class=\"{context.Sheet.Class(null, new StringBuilder(Embedded(container.BaselineAlignment)))}\">");
                context.Write(child, ParentKind.Row);
                context.Plan.Literal("</span>");
                break;
            case Span span:
                WriteSpan(context, span);
                break;
        }
    }

    /// <summary>
    /// A run or a span with <c>a:Web.Href</c>: an <c>&lt;a href&gt;</c> inside the text holding the inline as it is
    /// written without one, its color and its underline taken from the text unless the inline sets its own, as WinUI's
    /// <c>Hyperlink</c> inline sits in its paragraph.
    /// </summary>
    private static void WriteLinked(HtmlContext context, Inline inline)
    {
        context.Sheet.AddRule("il", ":where(a.il){color:inherit;text-decoration:inherit}\n");
        context.Plan.Literal("<a");
        LinkAddresses.Write(context.Plan, inline);
        context.Plan.Literal(" class=\"il\">");
        if (inline is Run run)
        {
            WriteRun(context, run);
        }
        else
        {
            WriteSpan(context, (Span)inline);
        }

        context.Plan.Literal("</a>");
    }

    private static void WriteRun(HtmlContext context, Run run)
    {
        var classes = context.Sheet.Class(null, InlineStyle(run, null));
        context.Plan.Literal(classes.Length == 0 ? string.Empty : $"<span class=\"{classes}\">");
        context.Plan.Value(run, Run.TextProperty);
        context.Plan.Literal(classes.Length == 0 ? string.Empty : "</span>");
    }

    private static void WriteSpan(HtmlContext context, Span span)
    {
        var (tag, implied) = span switch
        {
            Bold => ("b", TextElement.FontWeightProperty),
            Italic => ("i", TextElement.FontStyleProperty),
            Underline => ("u", Inline.TextDecorationsProperty),
            _ => ("span", (AvaloniaProperty?)null),
        };
        var classes = context.Sheet.Class(null, InlineStyle(span, implied));
        context.Plan.Literal(classes.Length == 0 ? $"<{tag}>" : $"<{tag} class=\"{classes}\">");
        WriteInlines(context, span.Inlines);
        context.Plan.Literal($"</{tag}>");
    }

    /// <summary>
    /// How an embedded control stands on its line, by its <c>BaselineAlignment</c>: on the line's baseline by default, as
    /// Avalonia's <c>EmbeddedControlRun</c> sets a control's bottom there (CSS sets an inline flex box without text by its
    /// bottom too, and one holding text by that text's baseline), or aligned as CSS's <c>vertical-align</c> names the same place.
    /// </summary>
    private static string Embedded(BaselineAlignment alignment) => alignment switch
    {
        BaselineAlignment.Top => "display:inline-flex;vertical-align:top;",
        BaselineAlignment.Center => "display:inline-flex;vertical-align:middle;",
        BaselineAlignment.Bottom => "display:inline-flex;vertical-align:bottom;",
        BaselineAlignment.TextTop => "display:inline-flex;vertical-align:text-top;",
        BaselineAlignment.TextBottom => "display:inline-flex;vertical-align:text-bottom;",
        BaselineAlignment.Subscript => "display:inline-flex;vertical-align:sub;",
        BaselineAlignment.Superscript => "display:inline-flex;vertical-align:super;",
        _ => "display:inline-flex;vertical-align:baseline;",
    };

    private static StringBuilder InlineStyle(Inline inline, AvaloniaProperty? implied)
    {
        bool Set(AvaloniaProperty property) => property != implied && inline.IsSet(property);
        var style = new StringBuilder();
        style.Append(Set(TextElement.FontSizeProperty) ? $"font-size:{CssValues.Px(inline.FontSize)};" : string.Empty);
        style.Append(Set(TextElement.FontWeightProperty) ? string.Create(CultureInfo.InvariantCulture, $"font-weight:{(int)inline.FontWeight};") : string.Empty);
        style.Append(Set(TextElement.FontStyleProperty) ? $"font-style:{(inline.FontStyle == FontStyle.Normal ? "normal" : "italic")};" : string.Empty);
        style.Append(Set(TextElement.ForegroundProperty) && CssValues.Brush(inline.Foreground) is { } color ? $"color:{color};" : string.Empty);
        style.Append(Set(TextElement.BackgroundProperty) && CssValues.Brush(inline.Background) is { } background ? $"background:{background};" : string.Empty);
        style.Append(Set(Inline.TextDecorationsProperty) ? $"text-decoration:{Decorations(inline.TextDecorations)};" : string.Empty);
        style.Append(Set(Inline.TextDecorationsProperty) && Underlines.Offset(inline, inline.TextDecorations) is not "auto" and var offset ? $"text-underline-offset:{offset};" : string.Empty);
        return style;
    }

    private static string Decorations(TextDecorationCollection? decorations)
    {
        var lines = (decorations ?? []).Select(decoration => decoration.Location switch
        {
            TextDecorationLocation.Underline => "underline",
            TextDecorationLocation.Strikethrough => "line-through",
            TextDecorationLocation.Overline => "overline",
            _ => null,
        }).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return lines.Count == 0 ? "none" : string.Join(' ', lines);
    }
}
