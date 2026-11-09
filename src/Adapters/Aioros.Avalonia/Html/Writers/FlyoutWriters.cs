using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class FlyoutWriters
{
    private static readonly AttachedProperty<bool?> ShownProperty = AvaloniaProperty.RegisterAttached<PopupFlyoutBase, bool?>("Shown", typeof(FlyoutWriters));

    /// <summary>Writes the flyout open or closed by a plan's variant, whatever its popup shows, until disposed.</summary>
    public static IDisposable Shown(PopupFlyoutBase flyout, bool open)
    {
        flyout.SetValue(ShownProperty, open);
        return new Unshown(flyout);
    }

    private sealed class Unshown(PopupFlyoutBase flyout) : IDisposable
    {
        public void Dispose() => flyout.ClearValue(ShownProperty);
    }

    private static bool WrittenOpen(PopupFlyoutBase flyout) => flyout.GetValue(ShownProperty) ?? (flyout.IsOpen || Forms.FlyoutPosts.Kept(flyout));

    /// <summary>The base rules of every flyout's popover: anchored in the top layer, none of the browser's own popover look.</summary>
    internal const string FlyCss =
        "[popover].fly{margin:0;position:fixed;inset:auto;overflow:visible}:where([popover].fly){padding:0;border:0 solid;background:none;color:inherit}" +
        "[popover].fly:popover-open{display:flex;flex-direction:column}@supports not (anchor-name:--a){[popover].fly{inset:0;margin:auto}}\n";

    /// <summary>
    /// Writes a flyout with the presenter Avalonia would show it in at its target, realized for the time it is written
    /// (<see cref="Probes"/>): its theme, its classes and, for a menu, its items styled and laid out as Avalonia shows them;
    /// the presenter, or nothing when there is none to realize, is given to the writer. A flyout open already is written
    /// with its own presenter, also when a plan's variant writes it closed (<see cref="Shown"/>).
    /// </summary>
    public static void Opened(PopupFlyoutBase flyout, Control target, Action<Control?> write)
    {
        if (flyout is MenuFlyout menu)
        {
            Take(menu.Items.OfType<MenuItem>(), target.DataContext);
        }

        if (flyout.IsOpen && (Presenter(target) ?? OpenPresenter(flyout)) is { } open)
        {
            write(open);
            return;
        }

        Control? presenter = flyout switch
        {
            MenuFlyout items => new MenuFlyoutPresenter { ItemsSource = items.Items, Theme = items.FlyoutPresenterTheme },
            Flyout panel => new FlyoutPresenter { Theme = panel.FlyoutPresenterTheme, Content = panel.Content },
            _ => null,
        };
        if (presenter is null)
        {
            write(null);
            return;
        }

        presenter.Classes.AddRange(flyout switch
        {
            MenuFlyout items => items.FlyoutPresenterClasses,
            Flyout panel => panel.FlyoutPresenterClasses,
            _ => [],
        });
        try
        {
            Probes.With(target, presenter, write);
        }
        finally
        {
            switch (presenter)
            {
                case MenuFlyoutPresenter realized:
                    realized.ItemsSource = null;
                    break;
                case FlyoutPresenter realized:
                    realized.Content = null;
                    break;
            }
        }
    }

    /// <summary>The presenter an open flyout's content is shown in, in the popup's own top level.</summary>
    private static Control? OpenPresenter(PopupFlyoutBase flyout) => flyout switch
    {
        Flyout { Content: Visual content } => content.FindAncestorOfType<FlyoutPresenter>(),
        MenuFlyout menu => menu.Items.OfType<Visual>().Select(item => item.FindAncestorOfType<MenuFlyoutPresenter>()).FirstOrDefault(presenter => presenter is not null),
        _ => null,
    };

    /// <summary>The presenter of the flyout open at this target: the one whose popup is placed at it, or the last one opened.</summary>
    private static Control? Presenter(Control target)
    {
        var presenters = TopLevel.GetTopLevel(target)?.GetVisualDescendants().OfType<Control>().Where(control => control is FlyoutPresenter or MenuFlyoutPresenter).ToList() ?? [];
        return presenters.Find(control => control.Parent is Popup popup && ReferenceEquals(popup.PlacementTarget, target)) ?? presenters.LastOrDefault();
    }

    public static void Inherit(IEnumerable<Control> controls, object? dataContext)
    {
        foreach (var control in controls.Where(control => control.DataContext is null))
        {
            control.DataContext = dataContext;
        }
    }

    private static void Take(IEnumerable<Control> controls, object? dataContext)
    {
        foreach (var control in controls)
        {
            control.SetValue(StyledElement.DataContextProperty, dataContext, BindingPriority.Style);
        }
    }

    /// <summary>
    /// Writes a flyout as a popover anchored to its target: <c>role="dialog"</c>, placed as Avalonia's positioner places
    /// it (<see cref="Placements"/>), with its presenter's look (<see cref="SurfaceLooks"/>) and its content. A hover
    /// card is a <c>hint</c> popover, which closes no menu or other popover when it opens.
    /// </summary>
    public static void WriteFlyout(HtmlContext context, string id, Flyout flyout, Control owner, Control? presenter, string kind = "", string? anchor = null, string? extra = null)
    {
        context.Sheet.AddRule("fly", FlyCss);
        var entrance = presenter is null ? null : Entered(context, presenter, Web.GetSwitchOffset(owner));
        var look = presenter is null ? Look(context, owner) : context.Sheet.Class(null, SurfaceLooks.Of(presenter));
        var place = extra is null ? $" style=\"position-anchor:{anchor ?? "--" + id};{Placements.Of(flyout)}\"" : string.Empty;
        context.Plan.Literal($"<div popover{kind} id=\"{id}\" role=\"dialog\"{(WrittenOpen(flyout) ? " data-aioros-open" : string.Empty)} class=\"{CssValues.Join(CssValues.Join("fly " + look, extra), entrance)}\"{place}>");
        if (flyout.Content is Control content)
        {
            Forms.FlyoutPosts.Owns(content, flyout);
            ThemeLooks.ExpandTemplates(content);
            var opener = context.Opener;
            context.Opener = owner;
            try
            {
                context.Write(content, ParentKind.Column);
            }
            finally
            {
                context.Opener = opener;
            }
        }
        else
        {
            context.Plan.Text(flyout.Content?.ToString());
        }

        context.Plan.Literal("</div>");
    }

    /// <summary>
    /// Settles the transitions an overlay's surface started as Avalonia showed it, and gives the class of the look it
    /// enters from (its own or its template's root's), which is also its look once closed, so it leaves as it came.
    /// </summary>
    public static string? Entered(HtmlContext context, Control surface, double switchOffset = double.NaN)
    {
        context.Entrances = context.Entrances.SetItems(Entrances.Settle(surface));
        var root = surface.GetVisualChildren().OfType<Control>().FirstOrDefault();
        var moving = context.Entrances.ContainsKey(surface) ? surface : root;
        if (moving is null || context.Entrances.GetValueOrDefault(moving) is not { } css)
        {
            return null;
        }

        var origin = moving.RenderTransformOrigin == RelativePoint.Center ? string.Empty : $"transform-origin:{CssValues.Origin(moving.RenderTransformOrigin)}";
        var closed = Entrances.Leaving(surface, moving) is { } leaving
            ? leaving.Css + (leaving.Transitions is { } transitions ? $"transition:{transitions},display {CssValues.Ms(leaving.Longest)} allow-discrete,overlay {CssValues.Ms(leaving.Longest)} allow-discrete;" : string.Empty)
            : css;
        var switches = double.IsNaN(switchOffset) ? string.Empty : CssValues.Px(switchOffset);
        var name = "en" + CssValues.Hash(css + closed + origin + switches);
        context.Sheet.AddRule(name, Entrances.Rule(name, css) + $"[popover].{name}:not(:popover-open){{{closed}}}\n" + (origin.Length == 0 ? string.Empty : $".{name}{{{origin}}}\n")
            + (switches.Length == 0 ? string.Empty : Switches(name, switches, css, closed)));
        return name;
    }

    /// <summary>
    /// The looks of a popover that replaces another of its row's (Radix's NavigationMenu <c>data-motion</c>, set by the
    /// overlay module): entering from the side of the one it replaced and leaving toward the other side, by
    /// <paramref name="offset"/> on top of its own entrance and exit, as tw-animate-css's <c>slide-in-from-right-52</c>
    /// adds its translation before the entrance's scale.
    /// </summary>
    private static string Switches(string name, string offset, string entrance, string closed)
    {
        static string Moved(string look, string by)
        {
            var at = look.IndexOf("transform:", StringComparison.Ordinal);
            if (at < 0)
            {
                return look + $"transform:translateX({by});";
            }

            var start = at + "transform:".Length;
            var end = look.IndexOf(';', start);
            var value = look[start..end];
            return look[..start] + (value == "none" ? $"translateX({by})" : $"translateX({by}) {value}") + look[end..];
        }

        var back = "-" + offset;
        var starting = $".{name}.{name}.{name}.{name}";
        return $"@starting-style{{{starting}[data-motion=from-end]{{{Moved(entrance, offset)}}}{starting}[data-motion=from-start]{{{Moved(entrance, back)}}}}}\n"
            + $"[popover].{name}[data-motion=to-start]:not(:popover-open){{{Moved(closed, back)}}}[popover].{name}[data-motion=to-end]:not(:popover-open){{{Moved(closed, offset)}}}\n";
    }

    public static void WritePresenter(HtmlContext context, FlyoutPresenter presenter, ParentKind parent)
    {
        var style = context.Box(presenter, parent);
        style.Append("position:static;display:flex;flex-direction:column;opacity:1;transform:none;");
        context.Plan.Literal($"<div class=\"{context.Classes(presenter, "fly " + Look(context, presenter), style)}\">");
        context.Content(presenter.Content);
        context.Plan.Literal("</div>");
    }

    /// <summary>The look of a flyout Avalonia did not show (no presenter to read): Fluent's resources.</summary>
    private static string Look(HtmlContext context, Control owner)
    {
        var background = ThemeResources.Css(owner, "FlyoutPresenterBackground", "#fff");
        var border = ThemeResources.Css(owner, "FlyoutBorderThemeBrush", "rgba(0,0,0,.1)");
        var padding = ThemeResources.Css(owner, "FlyoutContentThemePadding", "16px");
        var radius = ThemeResources.Css(owner, "OverlayCornerRadius", "8px");
        var minimum = ThemeResources.Css(owner, "FlyoutThemeMinWidth", "96px");
        var maximum = ThemeResources.Css(owner, "FlyoutThemeMaxWidth", "456px");
        var name = "fl" + CssValues.Hash(background + border + padding + radius + minimum + maximum);
        context.Sheet.AddRule(name, $".{name}{{background:{background};border:1px solid {border};padding:{padding};border-radius:{radius};min-width:{minimum};max-width:{maximum};color:inherit;flex-direction:column}}\n");
        return name;
    }
}
