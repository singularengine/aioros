using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace Aioros.Avalonia.Html.Variants;

/// <summary>
/// Avalonia's container queries (<c>ContainerQuery</c> over a <c>Container.Name</c> with <c>Container.Sizing</c>) as CSS
/// container queries: the container is an inline-size container named for it, and each width query that names it (or
/// names none) is a state its panel is traced in, its looks given under <c>@container</c>. Avalonia measures a container's
/// whole box and CSS its content box, so each width is written less the container's padding and border. Height queries
/// stay evaluated at the compile width.
/// </summary>
internal static class ContainerQueries
{
    private const double Unbounded = 100000;

    /// <summary>A width query: any of its groups holds when every feature of the group does.</summary>
    public sealed record Query(ContainerQuery Style, ImmutableArray<ImmutableArray<(string Feature, double Value)>> Any)
    {
        public bool Holds(double width) => Any.Any(group => group.All(part => part.Feature switch
        {
            "min-width" => width >= part.Value,
            "max-width" => width <= part.Value,
            _ => Math.Abs(width - part.Value) < 0.001,
        }));
    }

    public static bool IsContainer(Control control) => Container.GetSizing(control) != ContainerSizing.Normal;

    /// <summary>The element's container declarations: an inline-size container named for it, filling its slot as Avalonia measures a container at its available width.</summary>
    public static string Box(Control control)
    {
        if (!IsContainer(control))
        {
            return string.Empty;
        }

        var css = new StringBuilder("container-type:inline-size;");
        css.Append(Container.GetName(control) is { Length: > 0 } name ? $"container-name:{Name(name)};" : string.Empty);
        if (control.HorizontalAlignment != HorizontalAlignment.Stretch && double.IsNaN(control.Width))
        {
            var sides = control.Margin.Left + control.Margin.Right;
            css.Append(sides == 0 ? "width:100%;" : $"width:calc(100% - {CssValues.Px(sides)});");
        }

        return css.ToString();
    }

    /// <summary>The next width query naming the container, among those its panel is not traced in yet.</summary>
    public static Query? Next(Control container, IEnumerable<ContainerQuery> traced)
    {
        if (!IsContainer(container))
        {
            return null;
        }

        var name = Container.GetName(container);
        var done = traced.ToHashSet();
        return Styles(container).OfType<ContainerQuery>()
            .Where(query => (query.Name is null || query.Name == name) && !done.Contains(query))
            .Select(Parse).FirstOrDefault(query => query is not null);
    }

    /// <summary>The container's width as Avalonia's query reads it: its measured width less its margin, unbounded before it is measured.</summary>
    public static double Width(Control container) =>
        container.IsMeasureValid && container.DesiredSize.Width > 0
            ? container.DesiredSize.Width - container.Margin.Left - container.Margin.Right
            : Unbounded;

    /// <summary>A width at which each query holds or not as asked, within the container's own bounds, or none.</summary>
    public static double? Probe(Control container, IReadOnlyList<(Query Query, bool Holds)> states)
    {
        var candidates = states.SelectMany(state => state.Query.Any.SelectMany(group => group))
            .SelectMany(part => (double[])[part.Value, part.Value - 1, part.Value + 1])
            .Append(Unbounded).Append(1)
            .Where(width => width >= Math.Max(1, container.MinWidth) && width <= container.MaxWidth)
            .Distinct().Order();
        return candidates.Select(width => (double?)width).FirstOrDefault(width => states.All(state => state.Query.Holds(width!.Value) == state.Holds));
    }

    /// <summary>The rule's prelude for the looks a query gives while it holds, or while it does not.</summary>
    public static string Prelude(Control container, Query query, bool holds)
    {
        var inset = Inset(container);
        var groups = query.Any.Select(group => group.Select(part =>
            string.Create(CultureInfo.InvariantCulture, $"({part.Feature}:{CssValues.Px(part.Value - inset)})")).ToList()).ToList();
        var clauses = groups.Select(group => group.Count == 1 ? group[0] : $"({string.Join(" and ", group)})").ToList();
        var condition = clauses.Count == 1 ? clauses[0] : $"({string.Join(" or ", clauses)})";
        var name = query.Style.Name is { Length: > 0 } named ? Name(named) + " " : string.Empty;
        return $"@container {name}{(holds ? condition : "not " + condition)}";
    }

    private static string Name(string name) =>
        "a-" + string.Concat(name.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private static double Inset(Control container)
    {
        var (padding, border) = container switch
        {
            Border framed => (framed.Padding, framed.BorderThickness),
            Decorator decorator => (decorator.Padding, default),
            TemplatedControl templated => (templated.Padding, templated.BorderThickness),
            _ => (default(Thickness), default(Thickness)),
        };
        return padding.Left + padding.Right + border.Left + border.Right;
    }

    private static Query? Parse(ContainerQuery style)
    {
        var any = new List<ImmutableArray<(string, double)>>();
        foreach (var group in (style.Query?.ToString(style) ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = new List<(string, double)>();
            foreach (var feature in group.Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = feature.Split(':', StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || pair[0] is not ("min-width" or "max-width" or "width")
                    || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    return null;
                }

                parts.Add((pair[0], value));
            }

            any.Add([.. parts]);
        }

        return any.Count == 0 ? null : new Query(style, [.. any]);
    }

    private static IEnumerable<IStyle> Styles(Control container)
    {
        var sources = new List<IStyle>();
        for (StyledElement? element = container; element is not null; element = element.Parent as StyledElement)
        {
            sources.AddRange(element.Styles);
        }

        sources.AddRange(container.GetLogicalDescendants().OfType<StyledElement>().SelectMany(element => element.Styles));
        if (Application.Current is { } application)
        {
            sources.AddRange(application.Styles);
        }

        return Flatten(sources);
    }

    private static IEnumerable<IStyle> Flatten(IEnumerable<IStyle> styles) =>
        styles.SelectMany(style => style switch
        {
            global::Avalonia.Styling.Styles nested => Flatten(nested),
            StyleInclude { Loaded: { } loaded } => Flatten([loaded]),
            _ => [style],
        });
}
