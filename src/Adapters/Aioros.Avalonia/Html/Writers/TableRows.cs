using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// The look of a table's rows: the class of a row's template border and its bottom border, and, in a bound table whose
/// rows look different while a flag of their item is set, the class each row takes from it. The flag is one the row binds
/// two way (its check box, Aioros.Cosmo's row selection, as TanStack Table's row check box and <c>data-state="selected"</c>
/// read the same row selection). A plan learns the look of each value from a stand-in first row, its toggle checked and
/// given back during the trace so the model keeps its value, and follows the toggle's binding; a render without a plan
/// reads each row's look at the first position from a stand-in row of its item, one per state of the rows' toggles.
/// </summary>
internal static class TableRows
{
    public sealed record RowLook(string Class, string Border);

    /// <summary>The flag a row's look follows: the index of its two-way toggle among the row's toggles, and the row's class while it is set and while it is not.</summary>
    private sealed record RowFlag(int Toggle, string On, string Off);

    public static RowLook Of(HtmlContext context, Control container) =>
        new(ClassOf(context, container), ItemLooks.Part<Border>(container, "Border") is { } border ? Line(border.BorderThickness.Bottom, border.BorderBrush) : Line(0, null));

    public static string Line(double width, IBrush? brush) => $"{CssValues.Px(width)} solid {(width > 0 ? CssValues.Brush(brush) : null) ?? "transparent"}";

    /// <summary>
    /// The looks of a bound themed table's first and last rows, learned from stand-in rows of its first item so a row
    /// style by position (<c>:nth-last-child</c>) is written by position whatever the number of rows, or of its own rows.
    /// </summary>
    public static ImmutableArray<RowLook> Positions(HtmlContext context, TableView table, ref IReadOnlyList<Control> containers)
    {
        if (LocalBindings.IsBound(table, ItemsControl.ItemsSourceProperty))
        {
            var positions = ItemPositions.Containers(table, container => Of(context, container));
            containers = ItemParts.Containers(table);
            return positions.IsEmpty ? [] : [positions[0], positions[2]];
        }

        return containers.Count == 0 ? [] : [Of(context, containers[0]), Of(context, containers[^1])];
    }

    /// <summary>
    /// What writes the end of each row's opening tag, its class and <c>"&gt;</c>: <paramref name="look"/>, the first row's,
    /// or in a bound table the class of the row's flag, followed through the toggle's binding while a plan is recorded.
    /// </summary>
    public static Action<Control> Opener(HtmlContext context, TableView table, string look, ref IReadOnlyList<Control> containers)
    {
        if (containers.Count == 0 || !LocalBindings.IsBound(table, ItemsControl.ItemsSourceProperty))
        {
            return _ => context.Plan.Literal(look + "\">");
        }

        if (context.Plan.Planning)
        {
            var flag = ItemPositions.First(table, table.ItemFromContainer(containers[0]), container => Flagged(context, container));
            containers = ItemParts.Containers(table);
            return flag is null ? _ => context.Plan.Literal(look + "\">") : container => Followed(context, container, look, flag);
        }

        var states = containers.Select(State).ToList();
        var classes = new Dictionary<string, string>(StringComparer.Ordinal) { [states[0]] = look };
        foreach (var index in Enumerable.Range(0, states.Count).DistinctBy(index => states[index]).Where(index => index > 0).ToList())
        {
            classes[states[index]] = ItemPositions.First(table, table.ItemFromContainer(containers[index]), container => ClassOf(context, container)) ?? look;
        }

        if (classes.Count > 1)
        {
            containers = ItemParts.Containers(table);
        }

        return container => context.Plan.Literal(classes.GetValueOrDefault(State(container), look) + "\">");
    }

    private static string ClassOf(HtmlContext context, Control container) =>
        ItemLooks.Part<Border>(container, "Border") is { } border && container is TableViewRow row ? ItemLooks.Class(context, row, border, border, ItemLooks.Marked) : string.Empty;

    private static void Followed(HtmlContext context, Control container, string look, RowFlag flag)
    {
        if (Toggles(container).ElementAtOrDefault(flag.Toggle) is { } toggle && context.Plan.BoundPath(toggle, ToggleButton.IsCheckedProperty) is { } path)
        {
            var on = Flags.Of(path, negate: false, fallback: false);
            var off = Flags.Of(path, negate: false, fallback: false, complement: true);
            context.Plan.Add(new ConditionNode(on.Getter, [new LiteralNode(flag.On + "\">")]) { Source = on.Spec });
            context.Plan.Add(new ConditionNode(off.Getter, [new LiteralNode(flag.Off + "\">")]) { Source = off.Spec });
            return;
        }

        context.Plan.Fail("a table row's look follows a toggle the plan cannot follow");
        context.Plan.Literal(look + "\">");
    }

    private static RowFlag? Flagged(HtmlContext context, Control container)
    {
        var toggles = Toggles(container);
        var look = ClassOf(context, container);
        for (var index = 0; index < toggles.Count; index++)
        {
            var toggle = toggles[index];
            var set = toggle.IsChecked == true;
            string other;
            using (LocalBindings.Override(toggle, ToggleButton.IsCheckedProperty, !set))
            {
                other = ClassOf(context, container);
            }

            if (other != look)
            {
                return new RowFlag(index, set ? look : other, set ? other : look);
            }
        }

        return null;
    }

    /// <summary>The state of a row's two-way toggles, which tells the rows that may look different apart.</summary>
    private static string State(Control container) => string.Concat(Toggles(container).Select(toggle => toggle.IsChecked switch { true => '1', false => '0', null => '-' }));

    private static List<ToggleButton> Toggles(Control container) =>
        [.. global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(container).OfType<ToggleButton>()
            .Where(toggle => toggle.TemplatedParent is null && LocalBindings.Of(toggle, ToggleButton.IsCheckedProperty) is { } binding && BindingModes.WritesToSource(binding))];
}
