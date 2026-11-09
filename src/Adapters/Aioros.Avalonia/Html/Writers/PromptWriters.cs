using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// Writes what a post left for the user after the page: each host's prompts in a <c>&lt;dialog open&gt;</c> placed on
/// the host's box by CSS anchor positioning, which the framework's script shows as a modal dialog (the HTML
/// <c>dialog</c> element and <c>showModal()</c>), and each panel's notices in a manual popover at the corner where the
/// panel shows them (the popover API), which the script shows in the top layer.
/// </summary>
internal static class PromptWriters
{
    public const string PromptCss =
        "dialog.prompt{position:absolute;inset:auto;top:anchor(top);left:anchor(left);width:anchor-size(width);height:anchor-size(height);margin:0;padding:0;border:0;" +
        "background:none;color:inherit;max-width:none;max-height:none;overflow:hidden}dialog.prompt[open]{display:grid;grid-template:minmax(0,1fr)/minmax(0,1fr)}:where(dialog.prompt>div>*){max-width:100%}" +
        "dialog.prompt::backdrop{background:none}@supports not (anchor-name:--a){dialog.prompt{position:fixed;inset:0;width:auto;height:auto}}\n";


    public static void Write(HtmlContext context, Overlays overlays, Action<Control, ParentKind> write)
    {
        if (overlays.Prompts.IsEmpty && overlays.Replay is { } replay)
        {
            context.Plan.Literal($"<input type=\"hidden\" name=\"{PlanForms.PromptField}\" value=\"{CssValues.Encode(replay)}\">");
        }

        var start = context.Fields.Count;
        for (var index = 0; index < overlays.Prompts.Length; index++)
        {
            var layer = overlays.Prompts[index];
            context.Sheet.AddRule("prompt", PromptCss);
            var surfaces = layer.Controls.Where(prompt => Mask(prompt, layer.Host.Bounds.Size) is null).ToList();
            var labels = Labels(context, surfaces, "prompt" + (index + 1).ToString(CultureInfo.InvariantCulture));
            context.Plan.Literal($"<dialog open class=\"prompt\"{labels} style=\"position-anchor:--{overlays.Anchor(layer.Host)}\">");
            context.AutoFocus = Focused(layer.Host, surfaces, labels.StartsWith(" role=\"alertdialog\"", StringComparison.Ordinal));
            if (index == 0)
            {
                context.Plan.Literal($"<input type=\"hidden\" name=\"{PlanForms.PromptField}\" value=\"{CssValues.Encode(overlays.Replay)}\">");
            }

            foreach (var prompt in layer.Controls)
            {
                if (Mask(prompt, layer.Host.Bounds.Size) is { } fill)
                {
                    context.Plan.Literal($"<div class=\"{MaskClasses(context, prompt, fill)}\"></div>");
                    continue;
                }

                context.Plan.Literal($"<div class=\"{context.Sheet.Class("c", Placement(prompt))}\">");
                write(prompt, ParentKind.Single);
                context.Plan.Literal("</div>");
            }

            context.Plan.Literal("</dialog>");
        }

        if (overlays.Replay is not null)
        {
            var answers = context.Answers.Concat(Enumerable.Range(start, context.Fields.Count - start)).Where(index => !context.Fields.IsInert(index))
                .Select(index => (context.Fields.IsButton(index) ? string.Empty : "f") + context.Fields.Token(index));
            context.Plan.Literal($"<input type=\"hidden\" name=\"{PlanForms.AnswersField}\" value=\"{CssValues.Encode(string.Join(' ', answers))}\">");
        }

        NoticeWriters.Write(context, overlays, write);
    }

    /// <summary>The classes of a prompt's mask: its fill over the host, its transitions, and its entrance.</summary>
    public static string MaskClasses(HtmlContext context, Control mask, string fill)
    {
        var look = new StringBuilder($"grid-area:1/1;background:{fill};");
        look.Append(mask.Transitions is { Count: > 0 } moves && MotionStyles.TransitionList(moves) is { } transition ? $"transition:{transition};" : string.Empty);
        return CssValues.Join(CssValues.Join(context.Sheet.Class("pm", look), context.Extra(mask)), context.Entrance(mask));
    }

    /// <summary>
    /// The fill of a prompt's mask: a control with no content of its own that covers its whole host with a background, as
    /// an overlay dialog host puts one under a modal dialog; it covers the dialog's box on the web, whatever its size.
    /// </summary>
    public static string? Mask(Control prompt, Size host) =>
        host.Width > 0 && Math.Abs(prompt.Bounds.Width - host.Width) < 1 && Math.Abs(prompt.Bounds.Height - host.Height) < 1 && !prompt.GetVisualChildren().Any()
        && AvaloniaPropertyRegistry.Instance.GetRegistered(prompt).FirstOrDefault(property => property.Name == "Background") is { } background
            ? CssValues.Brush(prompt.GetValue(background) as IBrush)
            : null;

    /// <summary>
    /// Where a prompt's surface sits in its host: by its alignment, as Avalonia's layout places it (centered, stretched or
    /// at a side, its margins written with it), within the host's box whatever the viewport (no wider or taller than the
    /// host, scrolling inside), not at the offsets Avalonia rounded for its own window.
    /// </summary>
    public static StringBuilder Placement(Control prompt)
    {
        var style = new StringBuilder("grid-area:1/1;min-height:0;min-width:0;max-width:100%;max-height:100%;overflow:auto;");
        style.Append(prompt.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => "justify-self:start;",
            HorizontalAlignment.Right => "justify-self:end;",
            HorizontalAlignment.Center => "justify-self:center;",
            _ => "justify-self:stretch;",
        });
        style.Append(prompt.VerticalAlignment switch
        {
            VerticalAlignment.Top => "align-self:start;",
            VerticalAlignment.Bottom => "align-self:end;",
            VerticalAlignment.Center => "align-self:center;",
            _ => "align-self:stretch;",
        });
        return style;
    }

    /// <summary>
    /// The control the dialog focuses when it opens: the one Avalonia focused in it, or in an alert dialog its cancel
    /// button (<c>IsCancel</c>, as Radix's alert dialog focuses its cancel action); otherwise the browser focuses the
    /// dialog's first focusable control, as Avalonia's dialogs do.
    /// </summary>
    private static Control? Focused(Panel host, List<Control> surfaces, bool alert) =>
        TopLevel.GetTopLevel(host)?.FocusManager?.GetFocusedElement() is Control focused && surfaces.Exists(surface => surface == focused || surface.IsVisualAncestorOf(focused))
            ? focused
            : alert ? surfaces.SelectMany(surface => surface.GetSelfAndVisualDescendants()).OfType<Button>().FirstOrDefault(button => button.IsCancel) : null;

    /// <summary>Whether a text sits beside the title: in the same panel, or in a part beside the title's part (a header's title and description presenters).</summary>
    private static bool Beside(TextBlock text, TextBlock title) =>
        text.GetVisualParent() == title.GetVisualParent() || (text.GetVisualParent()?.GetVisualParent() is { } panel && panel == title.GetVisualParent()?.GetVisualParent());

    /// <summary>
    /// The dialog's role and labels: <c>alertdialog</c> when its surface announces assertively
    /// (<c>AutomationProperties.LiveSetting</c>), labelled by its title (the first heading, or the first text of a
    /// semibold or heavier weight) and described by the text that follows the title beside it (in its panel, or in the part beside its part).
    /// </summary>
    public static string Labels(HtmlContext context, List<Control> surfaces, string prefix)
    {
        var assertive = surfaces.SelectMany(surface => surface.GetSelfAndVisualDescendants().OfType<Control>()).Where(part => AutomationProperties.GetLiveSetting(part) == AutomationLiveSetting.Assertive).ToList();
        context.Announced.UnionWith(assertive);
        var role = assertive.Count > 0 ? " role=\"alertdialog\"" : string.Empty;
        var texts = surfaces.SelectMany(surface => surface.GetSelfAndVisualDescendants()).OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(text.Text) && !text.GetVisualAncestors().Any(ancestor => ancestor is Button or TextBox))
            .ToList();
        var title = texts.Find(text => AutomationProperties.GetHeadingLevel(text) > 0) ?? texts.Find(text => text.FontWeight >= FontWeight.SemiBold);
        if (title is null)
        {
            return role;
        }

        context.Ids[title] = prefix + "-title";
        var labels = $"{role} aria-labelledby=\"{prefix}-title\"";
        if (texts.Skip(texts.IndexOf(title) + 1).FirstOrDefault(text => text.FontWeight < FontWeight.SemiBold && Beside(text, title)) is { } description)
        {
            context.Ids[description] = prefix + "-description";
            labels += $" aria-describedby=\"{prefix}-description\"";
        }

        return labels;
    }
}
