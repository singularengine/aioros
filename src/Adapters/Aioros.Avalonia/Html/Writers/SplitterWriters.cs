using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Markup;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A grid splitter as the WAI-ARIA window splitter: a focusable <c>role="separator"</c> holding its template's parts
/// (beyond Fluent's childless border), whose value is the track before it as a percentage of its grid's star tracks
/// within the definitions' limits, and whose <c>data-aioros-split</c> names its axis and the tracks it resizes (as
/// Avalonia's <see cref="GridSplitter"/> picks them by its <see cref="GridSplitter.ResizeBehavior"/>), so the framework's
/// split module drags it with the pointer and moves it with the keys.
/// </summary>
internal static class SplitterWriters
{
    public static void Write(HtmlContext context, GridSplitter splitter, ParentKind parent)
    {
        var columns = ResizesColumns(splitter);
        var style = context.Box(splitter, parent);
        style.Append(splitter.MinHeight > 0 ? string.Create(CultureInfo.InvariantCulture, $"min-height:{CssValues.Px(splitter.MinHeight)};") : string.Empty);
        BoxStyles.Fill(style, splitter.Background, splitter.BorderBrush, splitter.BorderThickness, splitter.CornerRadius, default, resetBorder: false);
        style.Append(columns ? "touch-action:none;cursor:col-resize;" : "touch-action:none;cursor:row-resize;");
        var orientation = columns ? "vertical" : "horizontal";
        context.Plan.Literal($"<div role=\"separator\" tabindex=\"0\" aria-orientation=\"{orientation}\"{Split(splitter, columns)}");
        context.Root(splitter, null, style);
        context.Plan.Literal(">");
        if (Position(splitter, columns) is { } position)
        {
            context.Plan.Literal("<input type=\"hidden\" name=\"f");
            context.Field(splitter);
            context.Plan.Literal($"\" value=\"{Percent(position.Value)}\"{Delayed(splitter)}>");
        }

        if (splitter.GetVisualChildren().OfType<Control>().FirstOrDefault() is { } parts && parts is not Border { Child: null })
        {
            context.Write(parts, ParentKind.Single);
        }

        context.Plan.Literal("</div>");
    }

    /// <summary>
    /// The separator's value and limits as percentages of the grid's star tracks, read from the definitions' stars and
    /// their <see cref="Web.MinSizeProperty"/> and <see cref="Web.MaxSizeProperty"/> (the trace lays nothing out), with
    /// their limits in pixels for the split module to add once the page is laid out.
    /// </summary>
    private static string Split(GridSplitter splitter, bool columns)
    {
        if (splitter.GetVisualParent() is not Grid grid)
        {
            return string.Empty;
        }

        var tracks = columns
            ? grid.ColumnDefinitions.Select(definition => new Track(definition, definition.Width, definition.MinWidth, definition.MaxWidth)).ToList()
            : grid.RowDefinitions.Select(definition => new Track(definition, definition.Height, definition.MinHeight, definition.MaxHeight)).ToList();
        var (before, after) = Neighbors(splitter, columns);
        if (before < 0 || after >= tracks.Count)
        {
            return string.Empty;
        }

        var (first, second) = (tracks[before], tracks[after]);
        var stars = tracks.Where(track => track.Length.IsStar).Sum(track => track.Length.Value);
        var value = first.Length.IsStar && second.Length.IsStar && stars > 0 ? first.Length.Value / stars * 100 : double.NaN;
        var total = double.IsNaN(value) ? 100 : value + second.Length.Value / stars * 100;
        var low = Math.Max(Limit(Web.GetMinSize(first.Definition), 0), total - Limit(Web.GetMaxSize(second.Definition), 100));
        var high = Math.Min(Limit(Web.GetMaxSize(first.Definition), 100), total - Limit(Web.GetMinSize(second.Definition), 0));
        var now = double.IsNaN(value) ? string.Empty : $" aria-valuenow=\"{Percent(value)}\"";
        var kinds = string.Join(' ', tracks.Select(track => track.Length.GridUnitType switch
        {
            GridUnitType.Star => "s",
            GridUnitType.Auto => "a",
            _ => CssValues.Px(track.Length.Value),
        }));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{now} aria-valuemin=\"{Percent(low)}\" aria-valuemax=\"{Percent(high)}\" data-aioros-split=\"{(columns ? 'c' : 'r')} {before} {after}\" data-aioros-tracks=\"{kinds}\"{Keys(splitter)}{Limits(first, second)}");
    }

    /// <summary>
    /// Applies a posted position as Avalonia's <see cref="GridSplitter"/> ends a drag: the two tracks it resizes take their
    /// new share of the grid's stars, the page is laid out again at the window's size (as a post lays it out before its command) and <c>DragCompleted</c> is raised, so a library that
    /// follows its splitter's drags (Aioros.Cosmo's resizable panels) reads the new sizes; a position already shown
    /// changes nothing.
    /// </summary>
    public static void Bind(GridSplitter splitter, string? value)
    {
        var columns = ResizesColumns(splitter);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var posted) || Position(splitter, columns) is not { } position
            || Math.Abs(posted - position.Value) < 0.05)
        {
            return;
        }

        var share = Math.Clamp(posted, 0, position.Total);
        Size(position.First, share / 100 * position.Stars);
        Size(position.Second, (position.Total - share) / 100 * position.Stars);
        if (TopLevel.GetTopLevel(splitter) is Window window)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Hosting.Realization.Measure(window);
        }

        splitter.RaiseEvent(new global::Avalonia.Input.VectorEventArgs { RoutedEvent = global::Avalonia.Controls.Primitives.Thumb.DragCompletedEvent, Source = splitter });
    }

    private static void Size(DefinitionBase definition, double stars)
    {
        var length = new GridLength(Math.Max(stars, 0.0001), GridUnitType.Star);
        if (definition is ColumnDefinition column)
        {
            column.Width = length;
        }
        else if (definition is RowDefinition row)
        {
            row.Height = length;
        }
    }

    /// <summary>The separator's position, the share of the grid's stars the track before it holds, when both its tracks are stars.</summary>
    private static Placement? Position(GridSplitter splitter, bool columns)
    {
        if (splitter.GetVisualParent() is not Grid grid)
        {
            return null;
        }

        var definitions = columns ? grid.ColumnDefinitions.Cast<DefinitionBase>().ToList() : [.. grid.RowDefinitions];
        var lengths = definitions.Select(definition => definition is ColumnDefinition column ? column.Width : ((RowDefinition)definition).Height).ToList();
        var (before, after) = Neighbors(splitter, columns);
        var stars = lengths.Where(length => length.IsStar).Sum(length => length.Value);
        if (before < 0 || after >= definitions.Count || !lengths[before].IsStar || !lengths[after].IsStar || stars <= 0)
        {
            return null;
        }

        return new Placement(definitions[before], definitions[after], stars, lengths[before].Value / stars * 100, (lengths[before].Value + lengths[after].Value) / stars * 100);
    }

    /// <summary><c> data-change data-delay="…"</c> for a splitter with <c>a:Web.Delay</c> above 0, whose drags a library follows into a model bound two way.</summary>
    private static string Delayed(GridSplitter splitter) =>
        Web.GetDelay(splitter) is var delay && delay > 0 ? string.Create(CultureInfo.InvariantCulture, $" data-change data-delay=\"{(long)delay}\"") : string.Empty;

    /// <summary>The step of an arrow key (a percentage, or Avalonia's <c>KeyboardIncrement</c> in pixels) and what Enter collapses the track before to.</summary>
    private static string Keys(GridSplitter splitter)
    {
        var step = Web.GetResizeStep(splitter) is var percent && double.IsFinite(percent) ? CssValues.Num(percent) + "%" : CssValues.Px(splitter.KeyboardIncrement);
        var collapse = Web.GetCollapsedSize(splitter) is var collapsed && double.IsFinite(collapsed) ? $" data-aioros-collapse=\"{CssValues.Num(collapsed)}\"" : string.Empty;
        return $" data-aioros-step=\"{step}\"{collapse}";
    }

    private static string Limits(Track first, Track second) =>
        first.Min > 0 || second.Min > 0 || double.IsFinite(first.Max) || double.IsFinite(second.Max)
            ? $" data-aioros-px=\"{Pixels(first.Min)} {Pixels(first.Max)} {Pixels(second.Min)} {Pixels(second.Max)}\""
            : string.Empty;

    private static double Limit(double percent, double otherwise) => double.IsFinite(percent) ? percent : otherwise;

    private static string Percent(double value) => CssValues.Num(Math.Round(Math.Clamp(value, 0, 100), 1));

    private static string Pixels(double value) => double.IsFinite(value) ? CssValues.Num(value) : "Infinity";

    /// <summary>The tracks the splitter resizes, as Avalonia's <see cref="GridSplitter"/> picks them.</summary>
    private static (int Before, int After) Neighbors(GridSplitter splitter, bool columns)
    {
        var index = columns ? Grid.GetColumn(splitter) : Grid.GetRow(splitter);
        var behavior = splitter.ResizeBehavior;
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            var start = columns ? splitter.HorizontalAlignment == HorizontalAlignment.Left : splitter.VerticalAlignment == VerticalAlignment.Top;
            var end = columns ? splitter.HorizontalAlignment == HorizontalAlignment.Right : splitter.VerticalAlignment == VerticalAlignment.Bottom;
            behavior = start ? GridResizeBehavior.PreviousAndCurrent : end ? GridResizeBehavior.CurrentAndNext : GridResizeBehavior.PreviousAndNext;
        }

        return behavior switch
        {
            GridResizeBehavior.PreviousAndCurrent => (index - 1, index),
            GridResizeBehavior.CurrentAndNext => (index, index + 1),
            _ => (index - 1, index + 1),
        };
    }

    private static bool ResizesColumns(GridSplitter splitter) => splitter.ResizeDirection switch
    {
        GridResizeDirection.Columns => true,
        GridResizeDirection.Rows => false,
        _ when splitter.HorizontalAlignment != HorizontalAlignment.Stretch => true,
        _ when splitter.VerticalAlignment != VerticalAlignment.Stretch => false,
        _ => double.IsNaN(splitter.Height) || !double.IsNaN(splitter.Width),
    };

    private readonly record struct Track(DefinitionBase Definition, GridLength Length, double Min, double Max);

    private sealed record Placement(DefinitionBase First, DefinitionBase Second, double Stars, double Value, double Total);
}
