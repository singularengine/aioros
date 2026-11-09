using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A <c>DataGrid</c> as a native <c>&lt;table&gt;</c>: a <c>&lt;colgroup&gt;</c> of its visible columns' widths, a
/// <c>&lt;thead&gt;</c> of their headers and a <c>&lt;tbody&gt;</c> row per item, each cell as its column builds it (a
/// text column's text, a template column's content). Rows bound to the model, or shown by a control template from its control's
/// bound list, are one repeat learned from the first row; rows of a constant list are written as rendered.
/// </summary>
internal static class DataGridWriters
{
    public static void WriteDataGrid(HtmlContext context, DataGrid grid, ParentKind parent)
    {
        using var realized = Realized(grid);
        var columns = grid.Columns.Where(column => column.IsVisible).OrderBy(column => column.DisplayIndex).ToList();
        var style = context.Box(grid, parent);
        BoxStyles.Font(style, grid.FontSize, grid.FontWeight, grid.Foreground);
        BoxStyles.Fill(style, grid.Background, grid.BorderBrush, grid.BorderThickness, grid.CornerRadius, default);
        style.Append("overflow:auto;");
        var table = TableClass(context, grid);
        context.Plan.Literal($"<div class=\"{context.Classes(grid, "c", style)}\"><table class=\"{table}\">{ColumnGroup(columns)}");
        if (grid.HeadersVisibility.HasFlag(DataGridHeadersVisibility.Column))
        {
            WriteHeaders(context, columns);
        }

        context.Plan.Literal("<tbody>");
        WriteRows(context, grid, columns);

        context.Plan.Literal("</tbody></table></div>");
    }

    private static void WriteLiteralRows(HtmlContext context, DataGrid grid, List<DataGridRow> rows, IReadOnlyList<DataGridColumn> columns)
    {
        var bound = !context.Plan.Planning && LocalBindings.IsBound(grid, DataGrid.ItemsSourceProperty);
        var key = bound ? ItemIdentity.Getter(ItemIdentity.Path(grid, rows.FirstOrDefault()?.DataContext)) : null;
        var outer = context.Fields.ItemKey;
        var mark = bound ? context.Fields.EnterList() : default;
        foreach (var row in rows)
        {
            if (bound)
            {
                context.EnterListItem(outer, key, row.DataContext);
            }

            WriteRow(context, row, columns);
        }

        if (bound)
        {
            context.EnterItem(outer);
            context.Fields.LeaveList(mark);
        }
    }

    private static void WriteRows(HtmlContext context, DataGrid grid, IReadOnlyList<DataGridColumn> columns)
    {
        var rows = Rows(grid);
        var shown = context.Plan.Planning ? context.Plan.TemplateShown(grid, DataGrid.ItemsSourceProperty) : null;
        if (!context.Plan.Planning || (!LocalBindings.IsBound(grid, DataGrid.ItemsSourceProperty) && shown is null))
        {
            WriteLiteralRows(context, grid, rows, columns);
            return;
        }

        if (grid.ItemsSource is DataGridCollectionView { GroupDescriptions.Count: > 0 })
        {
            context.Plan.Fail("DataGrid.ItemsSource is grouped, and the plan writes rows without their groups");
            return;
        }

        if ((shown is { } templated ? Shown(context.Plan, grid, templated) : context.Plan.BoundPath(grid, DataGrid.ItemsSourceProperty)) is not { } path)
        {
            context.Plan.Fail("DataGrid.ItemsSource is bound to a path the plan cannot follow");
            return;
        }

        if (rows.FirstOrDefault() is not { } first)
        {
            context.Plan.Fail("a DataGrid bound to an empty list: there is no row to learn its cells from");
            return;
        }

        context.Plan.Begin(first.DataContext);
        WriteRow(context, first, columns);
        var source = SlotSpec.Bound(path);
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new RepeatNode(PropertyPaths.Getter(path), context.Plan.End()) { Source = source }, ItemIdentity.Path(grid, first.DataContext)));
    }

    /// <summary>The path of the list a grid in a control template shows from its control's bound list (<c>ItemsSource="{TemplateBinding Rows}"</c>), followed.</summary>
    private static string? Shown(PlanRecorder plan, DataGrid grid, (StyledElement Parent, AvaloniaProperty Property) shown)
    {
        plan.Follow(grid, DataGrid.ItemsSourceProperty);
        return plan.BoundPath(shown.Parent, shown.Property);
    }

    private static List<DataGridRow> Rows(DataGrid grid) =>
        [.. grid.GetVisualDescendants().OfType<DataGridRow>().Where(row => row.IsVisible && row.DataContext is not null && row.Index >= 0).OrderBy(row => row.Index)];

    /// <summary>
    /// Lays the grid out with no limit on its height while it is written, so every row is realized, as a table shows them
    /// all (a page is written without a layout, and a laid-out grid realizes only the rows its height shows); the layout it
    /// had comes back after.
    /// </summary>
    private static Restored Realized(DataGrid grid)
    {
        var width = grid.Bounds.Width > 0 ? grid.Bounds.Width : TopLevel.GetTopLevel(grid)?.Width is > 0 and var top ? top : 1280;
        grid.Measure(new Size(width, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, width, grid.DesiredSize.Height));
        Dispatcher.UIThread.RunJobs();
        ThemeLooks.ExpandTemplates(grid);
        return new Restored(grid);
    }

    private sealed class Restored(DataGrid grid) : IDisposable
    {
        public void Dispose()
        {
            grid.InvalidateMeasure();
            TopLevel.GetTopLevel(grid)?.UpdateLayout();
        }
    }

    private static void WriteRow(HtmlContext context, DataGridRow row, IReadOnlyList<DataGridColumn> columns)
    {
        context.Plan.Literal($"<tr class=\"{RowClass(context, row)}\">");
        foreach (var column in columns)
        {
            context.Plan.Literal("<td>");
            switch (column.GetCellContent(row))
            {
                case TextBlock text when column is DataGridTextColumn:
                    context.Plan.Value(text, TextBlock.TextProperty);
                    break;
                case Control content:
                    context.Write(content, ParentKind.Single);
                    break;
            }

            context.Plan.Literal("</td>");
        }

        context.Plan.Literal("</tr>");
    }

    /// <summary>The look of a row and its cells, from the row's style (its background, least height and cells' padding), which no item changes.</summary>
    private static string RowClass(HtmlContext context, DataGridRow row)
    {
        var cell = row.GetVisualDescendants().OfType<DataGridCell>().FirstOrDefault();
        var height = double.IsNaN(row.MinHeight) ? 0 : row.MinHeight;
        var css = $"{{background:{CssValues.Brush(row.Background) ?? "transparent"}}}|>td{{height:{CssValues.Px(height)};padding:{Padding(cell?.Padding ?? default)}}}";
        var name = "dr" + CssValues.Hash(css);
        context.Sheet.AddRule(name, string.Concat(css.Split('|').Select(rule => "." + name + rule)) + "\n");
        return name;
    }

    private static string TableClass(HtmlContext context, DataGrid grid)
    {
        var header = grid.GetVisualDescendants().OfType<DataGridColumnHeader>().FirstOrDefault(part => part.IsVisible);
        var lines = grid.GridLinesVisibility.HasFlag(DataGridGridLinesVisibility.Horizontal) ? CssValues.Brush(grid.HorizontalGridLinesBrush) : null;
        var css =
            "{border-collapse:collapse;border-spacing:0;table-layout:fixed;width:100%}" +
            "| :is(td,th){overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:left;vertical-align:middle}" +
            $"| th{{font-weight:{(int)(header?.FontWeight ?? FontWeight.SemiBold)};font-size:{CssValues.Px(header?.FontSize ?? grid.FontSize)};height:{CssValues.Px(header?.Bounds.Height ?? 32)};padding:{Padding(header?.Padding ?? default)};" +
            $"background:{CssValues.Brush(header?.Background) ?? "transparent"};color:{CssValues.Brush(header?.Foreground) ?? "inherit"}}}" +
            (lines is null ? string.Empty : $"| tr{{border-bottom:1px solid {lines}}}");
        var name = "dg" + CssValues.Hash(css);
        context.Sheet.AddRule(name, string.Concat(css.Split('|').Select(rule => "." + name + rule)) + "\n");
        return name;
    }

    private static string Padding(Thickness padding) =>
        $"{CssValues.Px(padding.Top)} {CssValues.Px(padding.Right)} {CssValues.Px(padding.Bottom)} {CssValues.Px(padding.Left)}";

    private static string ColumnGroup(IReadOnlyList<DataGridColumn> columns)
    {
        var stars = columns.Where(column => column.Width.IsStar).Sum(column => column.Width.Value);
        var html = new StringBuilder("<colgroup>");
        foreach (var width in columns.Select(column => column.Width))
        {
            html.Append(width switch
            {
                { IsAbsolute: true } => $"<col style=\"width:{CssValues.Px(width.Value)}\">",
                { IsStar: true } when stars > 0 => $"<col style=\"width:{TableColumns.Share(width.Value, stars)}\">",
                _ => "<col>",
            });
        }

        return html.Append("</colgroup>").ToString();
    }

    private static void WriteHeaders(HtmlContext context, IReadOnlyList<DataGridColumn> columns)
    {
        context.Plan.Literal("<thead><tr>");
        foreach (var header in columns.Select(column => column.Header))
        {
            context.Plan.Literal("<th>");
            if (header is Control content)
            {
                context.Write(content, ParentKind.Single);
            }
            else
            {
                context.Plan.Text(PlanValues.Format(header));
            }

            context.Plan.Literal("</th>");
        }

        context.Plan.Literal("</tr></thead>");
    }
}
