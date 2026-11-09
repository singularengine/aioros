using Aioros.Markup;
using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Page = Avalonia.Controls.Page;

namespace Aioros.Avalonia.Html.Writers;

internal static class PageWriters
{
    private const string BackIcon = "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M19 12H5\"/><path d=\"m12 19-7-7 7-7\"/></svg>";

    public static void WriteContentPage(HtmlContext context, ContentPage page, ParentKind parent)
    {
        var style = context.Box(page, parent);
        BoxStyles.Fill(style, page.Background, page.BorderBrush, page.BorderThickness, page.CornerRadius, page.Padding);
        style.Append(CssValues.Brush(page.Foreground) is { } color ? $"color:{color};" : string.Empty);
        context.Plan.Literal($"<section class=\"{context.Classes(page, "c", style)}\">");
        WriteBar(context, page.TopCommandBar);
        var inset = Inset(page);
        context.Plan.Literal($"<div class=\"c\" style=\"flex:1 0 auto{(inset == default ? string.Empty : ";padding:" + BoxStyles.Edges(inset))}\">");
        WriteChild(context, page.Content, ParentKind.Single);
        context.Plan.Literal("</div>");
        WriteBar(context, page.BottomCommandBar);
        context.Plan.Literal("</section>");
    }

    private static Thickness Inset(ContentPage page)
    {
        ThemeLooks.ExpandTemplates(page);
        var presenter = page.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(part => part.Name == "PART_ContentPresenter");
        return presenter is null
            ? default
            : presenter.GetSelfAndVisualAncestors().TakeWhile(visual => visual != page).OfType<Layoutable>().Aggregate(default(Thickness), static (sum, element) => sum + element.Margin);
    }

    public static void WriteNavigationPage(HtmlContext context, NavigationPage navigation, ParentKind parent)
    {
        var style = context.Box(navigation, parent);
        context.Plan.Literal($"<div class=\"{context.Classes(navigation, "c", style)}\">");
        var current = navigation.CurrentPage ?? navigation.Content as Page;
        if (current is null || NavigationPage.GetHasNavigationBar(current))
        {
            WriteNavigationBar(context, navigation, current);
        }

        if (current is not null)
        {
            ThemeLooks.ExpandTemplates(current);
            context.Write(current, ParentKind.Single);
        }

        context.Plan.Literal("</div>");
    }

    public static void WriteTabbedPage(HtmlContext context, TabbedPage tabbed, ParentKind parent)
    {
        var pages = (tabbed.Pages ?? []).ToList();
        var style = context.Box(tabbed, parent);
        style.Append(tabbed.TabPlacement == TabPlacement.Bottom ? "flex-direction:column-reverse;" : string.Empty);
        var look = TabLook(context, tabbed, pages.Count);
        context.Plan.Literal($"<div class=\"{context.Classes(tabbed, "tp " + look, style)}\"><div role=\"tablist\" class=\"tps\">");
        context.Fields.Add(tabbed);
        var bodies = pages.Select(page =>
        {
            context.Plan.Begin(context.Plan.Scope);
            context.Plan.Literal("<span>");
            context.Content(page.Header);
            context.Plan.Literal("</span>");
            return context.Plan.End();
        }).ToImmutableArray();
        var selection = context.Plan.Planning && context.Plan.BoundPath(tabbed, TabbedPage.SelectedIndexProperty) is { } path
            ? new SlotSpec("index-path", path)
            : new SlotSpec("indexes") { Values = [tabbed.SelectedIndex.ToString(CultureInfo.InvariantCulture)] };
        var markup = new ChoiceMarkup("<label class=\"tpt\">", "<label class=\"tpt\">", "<input type=\"radio\" class=\"cbx\"", "</label>");
        context.Plan.Add(new ChoiceNode(null, Slots.Predicate(selection), markup, bodies) { Selection = selection, Field = Forms.FieldSpecs.Of(context.Plan, tabbed) });
        context.Plan.Literal("</div>");
        foreach (var page in pages)
        {
            context.Plan.Literal("<div role=\"tabpanel\" class=\"tpp\">");
            ThemeLooks.ExpandTemplates(page);
            context.Write(page, ParentKind.Single);
            context.Plan.Literal("</div>");
        }

        context.Plan.Literal("</div>");
    }

    public static void WriteDrawerPage(HtmlContext context, DrawerPage drawer, ParentKind parent)
    {
        var id = CssValues.Encode(Web.GetId(drawer) ?? "drawer");
        var breakpoint = Docks(drawer) ? Math.Max(drawer.DrawerBehavior == DrawerBehavior.Locked ? 0 : drawer.DrawerBreakpointLength, 0) : 100000;
        var background = CssValues.Brush(drawer.DrawerBackground) ?? ThemeResources.Css(drawer, "SystemControlBackgroundChromeMediumLowBrush", "transparent");
        var pane = "pv" + CssValues.Hash($"{drawer.DrawerLength}|{breakpoint}|{background}");
        context.Sheet.AddRule(pane, CompositeWriters.PaneCss(pane, drawer.DrawerLength, breakpoint, background) + $".{pane}{{max-height:100%}}\n");
        var style = context.Box(drawer, parent);
        style.Append("display:flex;flex-direction:row;");
        context.Plan.Literal($"<div class=\"{context.Classes(drawer, null, style)}\">");
        if (drawer.DrawerBehavior != DrawerBehavior.Disabled)
        {
            context.Plan.Literal($"<aside popover id=\"{id}\" class=\"{pane}\">");
            WriteChild(context, drawer.DrawerHeader, ParentKind.Column);
            context.Plan.Literal("<div class=\"c\" style=\"flex:1 0 auto\">");
            WriteChild(context, drawer.Drawer, ParentKind.Single);
            context.Plan.Literal("</div>");
            WriteChild(context, drawer.DrawerFooter, ParentKind.Column);
            context.Plan.Literal("</aside>");
        }

        context.Plan.Literal("<div class=\"c\" style=\"flex:1 1 0;min-width:0\">");
        WriteDrawerBar(context, drawer, id, breakpoint);
        if (drawer.Content is Page page)
        {
            ThemeLooks.ExpandTemplates(page);
        }

        WriteChild(context, drawer.Content, ParentKind.Single);
        context.Plan.Literal("</div></div>");
    }

    /// <summary>
    /// Whether the drawer is docked beside its content above its breakpoint, as Avalonia resolves its display mode: locked,
    /// or open in a layout that sets it beside its content (<c>Split</c>, <c>CompactInline</c>); an overlay otherwise.
    /// </summary>
    private static bool Docks(DrawerPage drawer) =>
        drawer.DrawerBehavior == DrawerBehavior.Locked
        || (drawer.DrawerBehavior == DrawerBehavior.Auto && drawer.IsOpen && drawer.DrawerLayoutBehavior is DrawerLayoutBehavior.Split or DrawerLayoutBehavior.CompactInline);

    public static void WriteCarouselPage(HtmlContext context, CarouselPage carousel, ParentKind parent)
    {
        context.Sheet.AddRule("cp", ".cp{display:flex;flex-direction:row;overflow-x:auto;scroll-snap-type:x mandatory;scrollbar-width:none;scroll-marker-group:after}" +
            ".cp>.cpp{flex:0 0 100%;min-width:0;scroll-snap-align:start;display:flex;flex-direction:column}.cp>.cpi{scroll-initial-target:nearest}" +
            ".cp::scroll-marker-group{display:flex;justify-content:center;gap:8px;padding:8px}.cp>.cpp::scroll-marker{content:'';width:8px;height:8px;border-radius:50%;background:currentColor;opacity:.3}" +
            ".cp>.cpp::scroll-marker:target-current{opacity:.9}\n");
        if (context.Plan.Planning && LocalBindings.IsBound(carousel, SelectingMultiPage.SelectedIndexProperty))
        {
            context.Plan.Fail("a CarouselPage with a bound SelectedIndex");
        }

        var style = context.Box(carousel, parent);
        context.Plan.Literal($"<div class=\"{context.Classes(carousel, "cp", style)}\">");
        var index = 0;
        foreach (var page in carousel.Pages ?? [])
        {
            context.Plan.Literal(index == carousel.SelectedIndex ? "<div class=\"cpp cpi\">" : "<div class=\"cpp\">");
            ThemeLooks.ExpandTemplates(page);
            context.Write(page, ParentKind.Single);
            context.Plan.Literal("</div>");
            index++;
        }

        context.Plan.Literal("</div>");
    }

    public static void WriteHost(HtmlContext context, PageNavigationHost host, ParentKind parent) =>
        WriteChild(context, host.Page ?? host.Content, parent);

    public static void WriteRefreshContainer(HtmlContext context, RefreshContainer container, ParentKind parent) =>
        WriteChild(context, container.Content, parent);

    public static void WriteTransitioning(HtmlContext context, TransitioningContentControl control, ParentKind parent)
    {
        var style = context.Box(control, parent);
        var animations = Animations(context, control.PageTransition).ToList();
        style.Append(animations.Count > 0 ? $"animation:{string.Join(',', animations)};overflow:hidden;" : string.Empty);
        context.Plan.Literal($"<div class=\"{context.Classes(control, "c", style)}\">");
        var presenter = control.GetVisualDescendants().OfType<Control>().FirstOrDefault(child => child.Name == "PART_ContentPresenter");
        if (presenter is not null)
        {
            context.Write(presenter, ParentKind.Single);
        }
        else
        {
            WriteChild(context, control.Content, ParentKind.Single);
        }

        context.Plan.Literal("</div>");
    }

    private static IEnumerable<string> Animations(HtmlContext context, IPageTransition? transition)
    {
        switch (transition)
        {
            case null:
                yield break;
            case CompositePageTransition composite:
                foreach (var animation in composite.PageTransitions.SelectMany(child => Animations(context, child)))
                {
                    yield return animation;
                }

                break;
            case PageSlide slide:
                var axis = slide.Orientation == PageSlide.SlideAxis.Horizontal ? "X" : "Y";
                context.Sheet.AddRule("tcs" + axis, $"@keyframes tcs{axis}{{from{{transform:translate{axis}(100%)}}}}\n");
                yield return $"tcs{axis} {CssValues.Ms(slide.Duration)} {CssValues.Ease(slide.SlideInEasing)} both";
                break;
            default:
                context.Sheet.AddRule("tcf", "@keyframes tcf{from{opacity:0}}\n");
                yield return $"tcf {CssValues.Ms(transition is CrossFade fade ? fade.Duration : TimeSpan.FromMilliseconds(125))} {CssValues.Ease((transition as CrossFade)?.FadeInEasing)} both";
                break;
        }
    }

    private static void WriteNavigationBar(HtmlContext context, NavigationPage navigation, Page? current)
    {
        var background = ThemeResources.Css(navigation, "NavigationBarBackground", "transparent");
        var foreground = ThemeResources.Css(navigation, "NavigationBarForeground", "inherit");
        var name = "nb" + CssValues.Hash(background + foreground + navigation.BarHeight.ToString(CultureInfo.InvariantCulture));
        context.Sheet.AddRule(name, $".{name}{{display:flex;flex-direction:row;align-items:center;gap:4px;min-height:{CssValues.Px(navigation.BarHeight)};background:{background};color:{foreground};padding:0 4px}}" +
            $".{name}>.nbt{{flex:1;font-size:20px;margin:0 12px 0 8px}}.{name}>.nbk{{display:flex;align-items:center;align-self:stretch;padding:0 8px;border-radius:4px}}" +
            $".{name}>.nbk:hover{{background:{ThemeResources.Css(navigation, "SystemControlHighlightListLowBrush", "rgba(0,0,0,.05)")}}}\n");
        context.Plan.Literal($"<header class=\"{name}\">");
        if (Web.GetHref(navigation) is { } back && (current is null || NavigationPage.GetHasBackButton(current)))
        {
            context.Plan.Literal($"<a class=\"nbk\" href=\"{CssValues.Encode(Runtime.Slots.LinkSchemes.Safe(back))}\" aria-label=\"Back\">{BackIcon}</a>");
        }

        context.Plan.Literal("<h1 class=\"nbt\">");
        context.Content(current?.Header);
        context.Plan.Literal("</h1>");
        if (current is not null)
        {
            WriteBar(context, NavigationPage.GetTopCommandBar(current));
        }

        context.Plan.Literal("</header>");
    }

    private static void WriteDrawerBar(HtmlContext context, DrawerPage drawer, string id, double breakpoint)
    {
        if (drawer.DrawerBehavior is DrawerBehavior.Disabled or DrawerBehavior.Locked)
        {
            return;
        }

        var background = ThemeResources.Css(drawer, "NavigationControlNavBarBackground", "transparent");
        var height = ThemeResources.Css(drawer, "NavigationControlNavBarHeight", "48px");
        var name = "db" + CssValues.Hash(background + height);
        context.Sheet.AddRule(name, $".{name}{{display:flex;flex-direction:row;align-items:center;gap:8px;padding:5px;min-height:{height};background:{background}}}" +
            $".{name}>button{{display:flex;align-items:center;justify-content:center;align-self:stretch;min-width:40px;border:0;border-radius:4px}}.{name}>button:hover{{background:{ThemeResources.Css(drawer, "SystemControlHighlightListLowBrush", "rgba(0,0,0,.05)")}}}" +
            $".{name}>.dbt{{font-size:20px}}\n");
        var hide = breakpoint is > 0 and < 100000 ? " " + context.Sheet.HideAbove(breakpoint) : string.Empty;
        context.Plan.Literal($"<div class=\"{name}\"><button type=\"button\" popovertarget=\"{id}\" class=\"dbm{hide}\" aria-label=\"Toggle navigation drawer\">{InputWriters.IconSvg("menu", 20, 2)}</button>");
        var header = (drawer.Content as Page)?.Header ?? drawer.Header;
        if (header is not null)
        {
            context.Plan.Literal("<div class=\"dbt\">");
            context.Content(header);
            context.Plan.Literal("</div>");
        }

        context.Plan.Literal("</div>");
    }

    private static string TabLook(HtmlContext context, TabbedPage tabbed, int count)
    {
        for (var index = 1; index <= count; index++)
        {
            context.Sheet.AddRule($"tpi{index}", $".tp:has(>.tps>:nth-child({index}) :checked)>.tpp:nth-child({index + 1}){{display:flex}}.tp>.tps>:nth-child({index}):has(:checked){{color:var(--tp-on);box-shadow:inset 0 -2px var(--tp-on)}}\n");
        }

        var selected = ThemeResources.Css(tabbed, "TabbedPageTabItemHeaderForegroundSelected", "#0078d4");
        var unselected = ThemeResources.Css(tabbed, "TabbedPageTabItemHeaderForegroundUnselected", "rgba(0,0,0,.6)");
        var hover = ThemeResources.Css(tabbed, "TabbedPageTabItemHeaderBackgroundUnselectedPointerOver", "rgba(0,0,0,.05)");
        var name = "tl" + CssValues.Hash(selected + unselected + hover);
        context.Sheet.AddRule("tp", ".tp{display:flex;flex-direction:column}.tps{display:grid;grid-auto-flow:column;grid-auto-columns:minmax(0,1fr)}" +
            ".tpt{display:flex;align-items:center;justify-content:center;min-height:48px;padding:4px 8px;font-size:12px;cursor:pointer;user-select:none}" +
            ".tpt:has(:focus-visible){outline:2px solid;outline-offset:-2px}.tpp{display:none;flex:1 0 auto;flex-direction:column}.cbx{position:absolute;opacity:0;width:1px;height:1px;pointer-events:none}\n");
        context.Sheet.AddRule(name, $".{name}{{--tp-on:{selected}}}.{name}>.tps>.tpt{{color:{unselected}}}.{name}>.tps>.tpt:hover{{background:{hover}}}\n");
        return name;
    }

    private static void WriteBar(HtmlContext context, object? bar)
    {
        if (bar is Control control)
        {
            context.Write(control, ParentKind.Column);
        }
    }

    private static void WriteChild(HtmlContext context, object? child, ParentKind parent)
    {
        if (child is Control control)
        {
            context.Write(control, parent);
        }
        else if (child is not null)
        {
            context.Plan.Text(child.ToString());
        }
    }
}
