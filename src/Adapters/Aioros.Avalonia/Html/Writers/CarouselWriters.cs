using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Html.Panels;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A carousel as the WAI-ARIA carousel (<c>role="region"</c>, its slides <c>role="group"</c>): a scroll-snap strip of
/// slides along its <see cref="PageSlide"/>'s axis, each <see cref="Carousel.ViewportFraction"/> of the strip. A template
/// with <c>PART_Previous</c>, <c>PART_Next</c> or <c>PART_Indicators</c> (Aioros.Cosmo's) is written as it is, the strip
/// where its scroll viewer is, its buttons as the carousel module's steps and its indicators one per slide; the module
/// scrolls the strip, wraps when <see cref="SelectingItemsControl.WrapSelection"/> is set, posts a selection bound two way,
/// and, when <see cref="Carousel.IsSwipeEnabled"/> is set, drags the strip with the mouse as Embla does (<c>data-aioros-drag</c>).
/// </summary>
internal static class CarouselWriters
{
    private const string StripFill = "csf";

    public static void Write(HtmlContext context, Carousel carousel, ParentKind parent)
    {
        using var standIn = ItemParts.StandIn(context, carousel);
        var containers = ItemParts.Containers(carousel);
        var vertical = carousel.PageTransition is PageSlide { Orientation: PageSlide.SlideAxis.Vertical };
        var marks = " data-aioros-carousel" + (vertical ? "=\"v\"" : string.Empty) + (carousel.WrapSelection ? " data-aioros-wrap" : string.Empty) + (carousel.IsSwipeEnabled ? " data-aioros-drag" : string.Empty);
        var region = " role=\"region\" aria-roledescription=\"carousel\"" + ItemParts.PostsChange(carousel);
        var (previous, next, indicators) = (Part(carousel, "PART_Previous") as Button, Part(carousel, "PART_Next") as Button, Part(carousel, "PART_Indicators") as Panel);
        if (previous is null && next is null && indicators is null)
        {
            var style = context.Box(carousel, parent);
            BoxStyles.Fill(style, carousel.Background, carousel.BorderBrush, carousel.BorderThickness, carousel.CornerRadius, carousel.Padding);
            Strip(style, vertical);
            context.Plan.Literal($"<div{marks} data-aioros-strip{region} class=\"{context.Classes(carousel, null, style)}\">");
            Slides(context, carousel, containers);
            context.Plan.Literal("</div>");
            return;
        }

        var presenter = Part(carousel, "PART_ItemsPresenter");
        var viewer = Part(carousel, "PART_ScrollViewer") ?? presenter?.FindAncestorOfType<ScrollViewer>() ?? presenter;
        var substitutes = new Dictionary<Control, Action<ParentKind>>();
        if (viewer is not null)
        {
            if (vertical)
            {
                Fill(context, carousel, viewer);
            }

            substitutes[viewer] = at =>
            {
                var style = context.Box(viewer, at);
                Strip(style, vertical);
                context.Plan.Literal($"<div data-aioros-strip class=\"{context.Classes(viewer, null, style)}\">");
                Slides(context, carousel, containers);
                context.Plan.Literal("</div>");
            };
        }

        foreach (var (button, step) in new[] { (previous, "-1"), (next, "1") })
        {
            if (button is not null)
            {
                substitutes[button] = at => ButtonWriters.WriteScripted(context, button, at, $" data-aioros-slide=\"{step}\"");
            }
        }

        if (indicators is not null)
        {
            substitutes[indicators] = at => Indicators(context, carousel, indicators, containers.Count, at);
        }

        var box = context.Box(carousel, parent);
        context.Plan.Literal($"<div{marks}{region} class=\"{context.Classes(carousel, "c", box)}\">");
        foreach (var (part, write) in substitutes)
        {
            context.Substitutes[part] = write;
        }

        if (carousel.GetVisualChildren().OfType<Control>().FirstOrDefault() is { } root)
        {
            context.Write(root, ParentKind.Single);
        }

        foreach (var part in substitutes.Keys)
        {
            context.Substitutes.Remove(part);
        }

        context.Plan.Literal("</div>");
    }

    private static Control? Part(Carousel carousel, string name) =>
        carousel.GetVisualDescendants().OfType<Control>().FirstOrDefault(part => part.Name == name && ReferenceEquals(part.TemplatedParent, carousel));

    private static void Strip(StringBuilder style, bool vertical) =>
        style.Append(vertical
            ? "display:flex;flex-direction:column;overflow-y:auto;scroll-snap-type:y mandatory;scrollbar-width:none;overscroll-behavior-y:contain;"
            : "display:flex;flex-direction:row;overflow-x:auto;scroll-snap-type:x mandatory;scrollbar-width:none;overscroll-behavior-x:contain;");

    /// <summary>
    /// Makes a vertical strip and every part between it and its carousel fill the carousel's height, as Avalonia hands a
    /// template's parts the carousel's bounded height: its slides are a share of the strip, which the browser would
    /// otherwise size by the slides themselves.
    /// </summary>
    private static void Fill(HtmlContext context, Carousel carousel, Control strip)
    {
        context.Sheet.AddRule(StripFill, $".{StripFill}{{min-height:0!important}}.c>.{StripFill}{{flex:1 1 0!important}}\n");
        for (var part = strip; part is not null && !ReferenceEquals(part, carousel); part = part.GetVisualParent() as Control)
        {
            context.AddStateClass(part, StripFill, carousel);
        }
    }

    /// <summary>The slides, the selected one holding the hidden field that names its index when the selection is bound two way.</summary>
    private static void Slides(HtmlContext context, Carousel carousel, IReadOnlyList<Control> containers)
    {
        context.Sheet.AddRule("cs", ".cs{flex:0 0 100%;min-width:0;scroll-snap-align:start}.cs.sel{scroll-initial-target:nearest}\n");
        var fraction = carousel.ViewportFraction is > 0 and < 1 ? Fraction(context, carousel.ViewportFraction) : string.Empty;
        var posts = ItemParts.PostsChange(carousel).Length > 0;
        if (posts)
        {
            context.Fields.Add(carousel);
        }

        var (items, bodies) = ItemParts.Bodies(context, carousel, containers, container =>
        {
            if (container is ContentPresenter presenter)
            {
                ItemParts.Content(context, presenter, ParentKind.Single);
            }
            else
            {
                context.Write(container, ParentKind.Single);
            }
        });
        const string Slide = "<div role=\"group\" aria-roledescription=\"slide\" class=\"c cs";
        var markup = new ChoiceMarkup($"{Slide}{fraction}\">", $"{Slide} sel{fraction}\">", posts ? "<input type=\"hidden\"" : null, "</div>") { InputOnSelected = true };
        var (selected, selection) = ItemParts.Selection(context, carousel, items is not null);
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new ChoiceNode(items?.Getter, selected, markup, bodies)
        {
            ItemsSource = items?.Spec, Selection = selection, Field = posts ? Forms.FieldSpecs.Of(context.Plan, carousel) : null,
        }, carousel, containers));
    }

    private static string Fraction(HtmlContext context, double fraction)
    {
        var css = $"flex-basis:{CssValues.Num(fraction * 100)}%;scroll-snap-align:center";
        var name = "cf" + CssValues.Hash(css);
        context.Sheet.AddRule(name, $".{name}{{{css}}}\n");
        return " " + name;
    }

    /// <summary>
    /// The indicators: the panel's box with one button per slide (per item of a bound list), in the look of the panel's
    /// button that looks unlike the others (the selected slide's, or the one at the selected index when they all look
    /// alike) for the selected slide and of another for the rest, the selected one <c>aria-current</c>.
    /// </summary>
    private static void Indicators(HtmlContext context, Carousel carousel, Panel panel, int count, ParentKind parent)
    {
        var style = context.Box(panel, parent);
        var classes = panel is StackPanel stack ? PanelLayouts.Stack(stack.Orientation, stack.Spacing, false, style).Classes : "c";
        context.Plan.Literal($"<div class=\"{context.Classes(panel, classes, style)}\">");
        var buttons = panel.Children.OfType<Button>().ToList();
        if (buttons.Count > 0)
        {
            var looks = buttons.ConvertAll(button => ButtonWriters.LookClass(context, button, ParentKind.Row));
            var single = looks.FindIndex(look => looks.Count(other => other == look) == 1 && looks.Count > 2);
            var on = single >= 0 ? single : Math.Clamp(carousel.SelectedIndex, 0, buttons.Count - 1);
            var off = looks.FindIndex(look => look != looks[on]);
            var markup = new ChoiceMarkup(
                $"<button type=\"button\" data-aioros-slide-to class=\"{looks[off < 0 ? on : off]}\">",
                $"<button type=\"button\" data-aioros-slide-to aria-current=\"true\" class=\"{looks[on]}\">",
                null,
                "</button>");
            var path = context.Plan.Planning ? context.Plan.BoundPath(carousel, ItemsControl.ItemsSourceProperty) : null;
            Slot? items = path is null ? null : Slots.Path(path);
            var (selected, selection) = ItemParts.Selection(context, carousel, items is not null);
            ImmutableArray<ImmutableArray<IPlanNode>> bodies = [.. Enumerable.Repeat(ImmutableArray<IPlanNode>.Empty, items is null ? count : 1)];
            context.Plan.Add(new ChoiceNode(items?.Getter, selected, markup, bodies) { ItemsSource = items?.Spec, Selection = selection });
        }

        context.Plan.Literal("</div>");
    }
}
