using Aioros.Markup;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using System.Globalization;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class ButtonWriters
{

    /// <summary>The states of an option of a filtered list: a button's, and selected while it is <c>aria-selected</c>.</summary>
    private static readonly (string Pseudo, string Selector)[] OptionStates = [.. ThemeLooks.PressStates, (":selected", "is([aria-selected=true])")];

    /// <summary>The states of a button with a flyout: a button's, and its flyout open while its popover, written right after it, is open (Radix's <c>data-state=open</c>).</summary>
    private static readonly (string Pseudo, string Selector)[] FlyoutStates = [.. ThemeLooks.PressStates, (":flyout-open", "has(+:popover-open)")];

    /// <summary>The state of a copying button that has just copied, written while the copy module marks it <c>data-copied</c>.</summary>
    private static readonly (string Pseudo, string Selector)[] CopiedStates = [(":copied", "[data-copied]")];

    /// <summary>The state of an option of a list choosing in the browser while it is the chosen one, written while it is <c>aria-checked</c>.</summary>
    private static readonly (string Pseudo, string Selector)[] CheckedStates = [(":checked", "[aria-checked=true]")];

    /// <summary>
    /// The invoker command of a button that opens a library's overlay (<c>a:Web.Opens</c>, Radix's <c>DialogTrigger</c>) or
    /// closes the one it is in (<c>a:Web.Closes</c>, <c>DialogClose</c>), in the browser with no request; a closing button
    /// outside a library's overlay dismisses the notice card it is in (sonner's close button); or nothing.
    /// </summary>
    private static string? Command(HtmlContext context, Button button) =>
        Web.GetOpens(button) is { } owner && context.Modals.Id(owner) is { } dialog ? $" commandfor=\"{dialog}\" command=\"show-modal\""
        : Web.GetCloses(button) && context.Modals.Current is { } current ? $" commandfor=\"{current}\" command=\"close\""
        : Web.GetCloses(button) ? " data-aioros-closes"
        : null;

    public static void WriteButton(HtmlContext context, Button button, ParentKind parent)
    {
        if (Command(context, button) is { } command)
        {
            WriteScripted(context, button, parent, command, () => context.Plan.Literal(HotKeys.Attributes(button)));
            return;
        }

        if (Web.GetCopies(button) is not null || LocalBindings.IsBound(button, Web.CopiesProperty))
        {
            WriteCopying(context, button, parent);
            return;
        }

        if (Web.GetFlips(button) is { } flag)
        {
            WriteFlipping(context, button, parent, flag);
            return;
        }

        if (Resting(context, button) is { } state)
        {
            WriteAtRest(context, button, parent, state);
            return;
        }

        if (ChoiceLists.Owner(context, button) is not null)
        {
            StateStyles.RegisterTemplate(context, button, CheckedStates);
        }

        var style = context.Box(button, parent);
        var option = FilteredLists.IsOption(button);
        BoxStyles.Font(style, button.FontSize, button.FontWeight, option ? button.Foreground : null);
        var parts = Parts(button);
        var opens = ControlIds.OpensPopover(button);
        var states = option ? OptionStates : opens && button.Flyout is not null ? FlyoutStates : ThemeLooks.PressStates;
        var look = ThemeLooks.Of(button, parts is Border { Name: { Length: > 0 } root } ? root : "PART_ContentPresenter", states);
        style.Append(look.Css);
        Bare(style, parts);
        style.Append(button.MinHeight > 0 ? $"min-height:{CssValues.Px(button.MinHeight)};" : string.Empty);
        if (opens)
        {
            WriteToggleButton(context, button, look, style, parts);
            return;
        }

        style.Append(ContentAlignment(button, parts));
        WritePosting(context, button, look, style, parts, option);
    }

    /// <summary>A button that posts its command, or, with no command and an address, a link with its look (BUTTONS-124).</summary>
    private static void WritePosting(HtmlContext context, Button button, Look look, StringBuilder style, Control? parts, bool option)
    {
        if (button.Command is null && LinkAddresses.Links(button))
        {
            WriteAddressed(context, button, look, style, parts, option);
            return;
        }

        context.Plan.Literal("<button type=\"submit\" name=\"_btn\" value=\"");
        context.Button(button);
        context.Plan.Literal("|");
        context.Plan.Value(button, Button.CommandParameterProperty);
        context.Plan.Literal("\"" + HotKeys.Attributes(button));
        context.Root(button, context.Sheet.States(look), style);
        context.Plan.Literal(button.IsEffectivelyEnabled ? ">" : " disabled>");
        WriteInside(context, button, parts, option);
        context.Plan.Literal("</button>");
    }

    /// <summary>A button with no command and an address, written as a link with its look, as a <c>HyperlinkButton</c> is (BUTTONS-124).</summary>
    private static void WriteAddressed(HtmlContext context, Button button, Look look, StringBuilder style, Control? parts, bool option)
    {
        style.Append(look.Css.Contains("text-decoration:", StringComparison.Ordinal) ? string.Empty : "text-decoration:none;");
        style.Append(style.ToString().Contains("display:", StringComparison.Ordinal) ? string.Empty : "display:inline-flex;align-items:center;justify-content:center;");
        context.Plan.Literal("<a");
        LinkAddresses.Write(context.Plan, button);
        context.Root(button, CssValues.Join("lk", context.Sheet.States(look)), style);
        context.Plan.Literal(">");
        WriteInside(context, button, parts, option);
        context.Plan.Literal("</a>");
    }

    /// <summary>The state an option is written without, its look under it given by a rule: an option's selection, or its being the chosen one of a list choosing in the browser.</summary>
    private static string? Resting(HtmlContext context, Button button) =>
        ChoiceLists.Owner(context, button) is not null && button.Classes.Contains(":checked") ? ":checked"
        : FilteredLists.IsOption(button) && button.Classes.Contains(":selected") ? ":selected"
        : null;

    private static void WriteAtRest(HtmlContext context, Button button, ParentKind parent, string state)
    {
        var classes = (IPseudoClasses)button.Classes;
        Settled.Change(button, () => classes.Remove(state));
        try
        {
            WriteButton(context, button, parent);
        }
        finally
        {
            Settled.Change(button, () => classes.Add(state));
        }
    }

    /// <summary>
    /// A template's button the framework's script runs in the browser rather than a post (a carousel's previous and next):
    /// a <c>type="button"</c> with the given attributes, its look and its content, disabled as Avalonia rendered it.
    /// </summary>
    public static void WriteScripted(HtmlContext context, Button button, ParentKind parent, string attributes, Action? more = null)
    {
        var style = context.Box(button, parent);
        BoxStyles.Font(style, button.FontSize, button.FontWeight, null);
        var parts = Parts(button);
        var look = ThemeLooks.Of(button, parts is Border { Name: { Length: > 0 } root } ? root : "PART_ContentPresenter", ThemeLooks.PressStates);
        style.Append(look.Css);
        Bare(style, parts);
        context.Plan.Literal("<button type=\"button\"" + attributes);
        more?.Invoke();
        context.Root(button, context.Sheet.States(look), style);
        context.Plan.Literal(button.IsEffectivelyEnabled ? ">" : " disabled>");
        WriteInside(context, button, parts);
        context.Plan.Literal("</button>");
    }

    /// <summary>
    /// A button that copies its text (<c>a:Web.Copies</c>, literal or from the model) to the visitor's clipboard in the
    /// browser, with no request, taking the looks its theme gives it and its template's parts under <c>:copied</c> while
    /// the copy module marks it <c>data-copied</c>.
    /// </summary>
    private static void WriteCopying(HtmlContext context, Button button, ParentKind parent)
    {
        StateStyles.RegisterTemplate(context, button, CopiedStates);
        WriteScripted(context, button, parent, string.Empty, () =>
        {
            context.Plan.Literal(" data-aioros-copies=\"");
            context.Plan.Value(button, Web.CopiesProperty);
            context.Plan.Literal("\"");
        });
    }

    /// <summary>
    /// A button that switches the flag of the panel around it in the browser (<c>a:Web.Flips</c>), saying whether the
    /// panel is open as the WAI-ARIA disclosure pattern's <c>aria-expanded</c>.
    /// </summary>
    private static void WriteFlipping(HtmlContext context, Button button, ParentKind parent, string flag)
    {
        WriteScripted(context, button, parent, $" data-aioros-flips=\"{CssValues.Encode(flag)}\"", () =>
        {
            if (button.GetVisualAncestors().OfType<Panel>().FirstOrDefault(panel => Web.GetFlag(panel) == flag) is { } owner && Variants.BrowserFlags.Property(owner) is { } property)
            {
                Variants.FlagExpansions.WriteExpanded(context, owner, property);
            }

            context.Plan.Literal(HotKeys.Attributes(button));
        });
    }

    /// <summary>
    /// Resets the browser's button border and padding for a template whose root is neither a named border nor the content
    /// presenter (a panel of parts, Aioros.Cosmo's sidebar rail), which no look gives a border or a padding.
    /// </summary>
    internal static void Bare(StringBuilder style, Control? parts)
    {
        if (parts is not (null or Border { Name.Length: > 0 }))
        {
            style.Append("border-width:0;padding:0;");
        }
    }

    /// <summary>The class of a button's box and look, for markup a writer repeats (a carousel's indicators).</summary>
    public static string LookClass(HtmlContext context, Button button, ParentKind parent)
    {
        var style = context.Box(button, parent);
        var look = ThemeLooks.Of(button, Parts(button) is Border { Name: { Length: > 0 } root } ? root : "PART_ContentPresenter", ThemeLooks.PressStates);
        style.Append(look.Css);
        return context.Classes(button, context.Sheet.States(look), style);
    }

    /// <summary>
    /// The template's parts a button's element holds when its template is more than the content presenter: the root's
    /// children when the root is a named border (whose look the element takes), or the whole root; null for a template
    /// whose root is the content presenter (Fluent's), whose content alone is written.
    /// </summary>
    public static bool WritesParts(Button button) => Parts(button) is not null;

    internal static Control? Parts(Button button) =>
        button.GetVisualChildren().OfType<Control>().FirstOrDefault() is { } root && root is not ContentPresenter { Name: "PART_ContentPresenter" } ? root : null;

    internal static void WriteInside(HtmlContext context, Button button, Control? parts, bool option = false)
    {
        if (parts is null)
        {
            context.Content(button);
        }
        else if (parts is Border { Name.Length: > 0, Child: { } child })
        {
            Write(context, button, child, option);
        }
        else if (parts is not Border { Name.Length: > 0 })
        {
            Write(context, button, parts, option);
        }
    }

    /// <summary>Writes a button's parts; an option's texts take its color, so its selected look colors them as Avalonia's inherited foreground does.</summary>
    private static void Write(HtmlContext context, Button button, Control parts, bool option)
    {
        if (option)
        {
            context.WriteInItem(button, parts, ParentKind.Row);
        }
        else
        {
            context.Write(parts, ParentKind.Row);
        }
    }

    /// <summary>
    /// A link as an <c>&lt;a href&gt;</c>: through its template when the template is more than the content presenter (a
    /// library's link holding an icon beside its content, Aioros.Cosmo's <c>c:MarkerLink</c>), as a button's parts are
    /// written; otherwise its content with its look and padding.
    /// </summary>
    public static void WriteLink(HtmlContext context, HyperlinkButton link, ParentKind parent)
    {
        var style = context.Box(link, parent);
        BoxStyles.Font(style, link.FontSize, link.FontWeight, link.Foreground);
        var parts = Parts(link);
        var look = ThemeLooks.Of(link, parts is Border { Name: { Length: > 0 } root } ? root : "PART_ContentPresenter", ThemeLooks.PressStates);
        style.Append(look.Css);
        if (parts is not null)
        {
            Bare(style, parts);
            style.Append(ContentAlignment(link, parts) is { Length: > 0 } placed ? placed : CenteredLink(style));
            style.Append(look.Css.Contains("text-decoration:", StringComparison.Ordinal) ? string.Empty : "text-decoration:none;");
            OpenLink(context, link);
            context.Root(link, CssValues.Join("lk", context.Sheet.States(look)), style);
            context.Plan.Literal(">");
            WriteInside(context, link, parts, option: true);
            context.Plan.Literal("</a>");
            return;
        }

        if (link.Padding != default)
        {
            style.Append("padding:").Append(BoxStyles.Edges(link.Padding)).Append(';');
        }

        style.Append("display:inline-flex;align-items:center;");
        style.Append(link.HorizontalContentAlignment switch
        {
            HorizontalAlignment.Center => "justify-content:center;",
            HorizontalAlignment.Right => "justify-content:flex-end;",
            _ => string.Empty,
        });
        OpenLink(context, link);
        context.Root(link, CssValues.Join("lk", context.Sheet.States(look)), style);
        if (LocalBindings.IsBound(link, ContentControl.ContentProperty))
        {
            context.Plan.Literal(">");
            context.Plan.Value(link, ContentControl.ContentProperty);
            context.Plan.Literal("</a>");
        }
        else if (link.Content is Control)
        {
            context.Plan.Literal(">");
            context.Content(link);
            context.Plan.Literal("</a>");
        }
        else
        {
            context.Plan.Literal($">{CssValues.Encode(link.Content?.ToString())}</a>");
        }
    }

    private static void OpenLink(HtmlContext context, HyperlinkButton link)
    {
        if (LinkAddresses.Links(link))
        {
            context.Plan.Literal("<a");
            LinkAddresses.Write(context.Plan, link);
        }
        else
        {
            context.Plan.Literal("<a href=\"");
            context.Plan.Value(link, HyperlinkButton.NavigateUriProperty, ValueFormat.Href);
            context.Plan.Literal("\"");
        }
    }

    private static void WriteToggleButton(HtmlContext context, Button button, Look look, StringBuilder style, Control? parts)
    {
        style.Append("display:inline-flex;align-items:center;gap:8px;text-align:start;");
        var target = Web.GetToggles(button) ?? context.NextPopover();
        context.Plan.Literal($"<button type=\"button\" popovertarget=\"{target}\"{Hover(button)}{HotKeys.Attributes(button)}");
        context.Root(button, context.Sheet.States(look), style, button.Flyout is not null ? $"anchor-name:--{target}" : null);
        context.Plan.Literal(">");
        if (button.Flyout is not null)
        {
            OpenParts(context, button);
        }

        WriteInside(context, button, parts);
        context.Plan.Literal("</button>");
        if (button.Flyout is { } flyout)
        {
            SplitWriters.WriteFlyout(context, target, flyout, button);
        }
    }

    /// <summary>
    /// The hover opening a library declares for a button's flyout (<see cref="Web.HoverOpenDelayProperty"/>): the delays
    /// the overlay module waits before opening and closing it as the pointer comes and goes.
    /// </summary>
    private static string Hover(Button button) =>
        button.Flyout is not null && Web.GetHoverOpenDelay(button) is var open && !double.IsNaN(open)
            ? string.Create(CultureInfo.InvariantCulture, $" data-aioros-overlay data-hover-open=\"{Math.Max(0, open)}\" data-hover-close=\"{Math.Max(0, Web.GetHoverCloseDelay(button))}\"{(double.IsNaN(Web.GetSwitchOffset(button)) ? string.Empty : " data-switch")}")
            : string.Empty;

    /// <summary>
    /// The transform each of the button's template parts takes under <c>:flyout-open</c> (a chevron turned), written for
    /// the time its popover is open, with the parts' transitions held so the look read is the one they end on.
    /// </summary>
    private static void OpenParts(HtmlContext context, Button button)
    {
        var parts = button.GetVisualDescendants().OfType<Control>().ToList();
        var held = parts.ConvertAll(part => part.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation));
        var classes = (IPseudoClasses)button.Classes;
        var was = button.Classes.Contains(":flyout-open");
        try
        {
            classes.Remove(":flyout-open");
            var closed = parts.ConvertAll(Transform);
            classes.Add(":flyout-open");
            var opened = parts.ConvertAll(Transform);
            for (var index = 0; index < parts.Count; index++)
            {
                if (closed[index] != opened[index])
                {
                    var name = "fo" + CssValues.Hash(opened[index]);
                    context.Sheet.AddRule(name, $"[popovertarget]:has(+:popover-open) .{name}{{transform:{opened[index]}}}\n");
                    context.AddStateClass(parts[index], name, button);
                }
            }
        }
        finally
        {
            classes.Set(":flyout-open", was);
            held.ForEach(lease => lease?.Dispose());
        }
    }

    private static string Transform(Control part) => part.RenderTransform is { Value.IsIdentity: false } transform ? CssValues.Transform(transform) : "none";

    /// <summary>
    /// Where a button lays its content out, instead of the browser centering a button's content: as its content presenter
    /// aligns it when the template is that presenter or a named border around it (a one-cell grid, none when the presenter
    /// centers it both ways), or over the whole box with its texts from their start when the template is a panel of parts,
    /// as Avalonia stretches a template's root.
    /// </summary>
    /// <summary>A link's centered content, which an <c>&lt;a&gt;</c> does not center by itself as a <c>&lt;button&gt;</c> does.</summary>
    private static string CenteredLink(StringBuilder style) =>
        (style.ToString().Contains("display:", StringComparison.Ordinal) ? string.Empty : "display:inline-flex;") + "align-items:center;justify-content:center;";

    internal static string ContentAlignment(Button button, Control? parts)
    {
        var presenter = parts switch
        {
            null => button.GetVisualChildren().FirstOrDefault() as ContentPresenter,
            Border { Name.Length: > 0, Child: ContentPresenter child } => child,
            _ => null,
        };
        if (presenter is not { Name: "PART_ContentPresenter" })
        {
            return parts is null ? string.Empty : "display:grid;justify-items:stretch;align-items:stretch;text-align:start;";
        }

        if (presenter.HorizontalContentAlignment == HorizontalAlignment.Center && presenter.VerticalContentAlignment == VerticalAlignment.Center)
        {
            return string.Empty;
        }

        var across = presenter.HorizontalContentAlignment switch
        {
            HorizontalAlignment.Left => "start",
            HorizontalAlignment.Center => "center",
            HorizontalAlignment.Right => "end",
            _ => "stretch",
        };
        var down = presenter.VerticalContentAlignment switch
        {
            VerticalAlignment.Top => "start",
            VerticalAlignment.Center => "center",
            VerticalAlignment.Bottom => "end",
            _ => "stretch",
        };
        return $"display:grid;justify-items:{across};align-items:{down};text-align:{(across is "stretch" ? "start" : across)};";
    }
}
