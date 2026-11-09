using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aioros.Avalonia.Html.Writers;

internal static class CollectionWriters
{
    public static void WriteTableView(HtmlContext context, TableView table, ParentKind parent)
    {
        using var columns = new TableColumns(context, table);
        Write(context, table, parent, columns);
    }

    private static void Write(HtmlContext context, TableView table, ParentKind parent, TableColumns columns)
    {
        var containers = ItemParts.Containers(table);
        var style = context.Box(table, parent);
        BoxStyles.Font(style, table.FontSize, table.FontWeight, table.Foreground);
        BoxStyles.Fill(style, table.Background, table.BorderBrush, table.BorderThickness, table.CornerRadius, default);
        style.Append("overflow:auto;");
        var themed = !FluentThemes.Owns(table, typeof(TableView));
        var rows = themed ? TableRows.Positions(context, table, ref containers) : [];
        var look = rows.Length > 0 ? rows[0].Class : containers.Count > 0 ? TableRows.Of(context, containers[0]).Class : string.Empty;
        var open = TableRows.Opener(context, table, look, ref containers);
        var cell = CellClass(context, table, themed ? ThemeRules(table, rows) : string.Empty);
        context.Plan.Literal($"<div{ChangedFields.Posts(table)} class=\"{context.Classes(table, "c", style)}\"><table class=\"{cell}\">");
        columns.Group();
        columns.Headers();
        context.Plan.Literal("<tbody>");
        var (items, bodies) = ItemParts.Bodies(context, table, containers, container =>
        {
            open(container);
            WriteCells(context, container, columns);
        });
        var markup = new ChoiceMarkup("<tr class=\"", "<tr class=\"sel ", null, "</tr>");
        var (selected, selection) = ItemParts.Selection(context, table, items is not null);
        context.Plan.Add(ItemIdentity.Keyed(context.Plan, new ChoiceNode(items?.Getter, selected, markup, bodies) { ItemsSource = items?.Spec, Selection = selection }, table, containers));
        context.Plan.Literal("</tbody></table></div>");
    }

    /// <summary>
    /// The rules a table's own theme gives it: its headers' height, fill and padding from the column header's theme, the line its
    /// scroll viewer's <c>PART_Header</c> draws under them, and its rows' bottom borders, the last row's from its position.
    /// </summary>
    private static string ThemeRules(TableView table, ImmutableArray<TableRows.RowLook> rows)
    {
        var header = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(table).OfType<TableViewColumnHeader>().FirstOrDefault();
        var fill = header is null ? null : CssValues.Brush(ItemLooks.Part<ContentPresenter>(header, "PART_ContentPresenter")?.Background ?? header.Background);
        var height = header is null || double.IsNaN(header.Height) ? string.Empty : $"height:{CssValues.Px(header.Height)};";
        var padding = header?.Padding ?? default;
        var css = new StringBuilder($"| th{{{height}background:{fill ?? "transparent"};padding:{CssValues.Px(padding.Top)} {CssValues.Px(padding.Right)} {CssValues.Px(padding.Bottom)} {CssValues.Px(padding.Left)}}}");
        css.Append(CultureInfo.InvariantCulture, $"| th:is(:first-child,:last-child){{padding-left:{CssValues.Px(padding.Left)};padding-right:{CssValues.Px(padding.Right)}}}");
        if (ItemLooks.Part<Border>(table, "PART_Header") is { BorderThickness.Bottom: > 0 } line)
        {
            css.Append(CultureInfo.InvariantCulture, $"| thead tr{{border-bottom:{TableRows.Line(line.BorderThickness.Bottom, line.BorderBrush)}}}");
        }

        if (rows.Length == 2)
        {
            css.Append(CultureInfo.InvariantCulture, $"| tbody tr{{border-bottom:{rows[0].Border}}}");
            css.Append(rows[1].Border == rows[0].Border ? string.Empty : $"| tbody tr:last-child{{border-bottom:{rows[1].Border}}}");
        }

        return css.ToString();
    }

    private static string CellClass(HtmlContext context, TableView table, string theme)
    {
        var row = table.TryFindResource("TableViewRowPadding", table.ActualThemeVariant, out var value) && value is Thickness padding ? padding : new Thickness(6, 9, 6, 12);
        var cell = table.TryFindResource("TableViewCellPadding", table.ActualThemeVariant, out var cellValue) && cellValue is Thickness cellPadding ? cellPadding : new Thickness(6, 0);
        var header = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(table).OfType<TableViewColumnHeader>().FirstOrDefault();
        var weight = (header is null ? 600 : (int)header.FontWeight).ToString(CultureInfo.InvariantCulture);
        var background = ItemLooks.Resource(table, "SystemControlBackgroundChromeMediumBrush") ?? "transparent";
        var separator = ItemLooks.Resource(table, "TableViewColumnHeaderSeparatorBackground") ?? "transparent";
        var css =
            $"{{border-collapse:collapse;border-spacing:0;table-layout:fixed;width:100%}}" +
            $"| :is(td,th){{padding:{CssValues.Px(row.Top + cell.Top)} {CssValues.Px(cell.Right)} {CssValues.Px(row.Bottom + cell.Bottom)} {CssValues.Px(cell.Left)};overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:left}}" +
            $"| :is(td,th):first-child{{padding-left:{CssValues.Px(row.Left + cell.Left)}}}" +
            $"| :is(td,th):last-child{{padding-right:{CssValues.Px(row.Right + cell.Right)}}}" +
            $"| th{{font-weight:{weight};background:{background};box-shadow:inset -1px 0 {separator}}}" +
            "| .r{text-align:right}| .m{text-align:center}" + theme;
        var name = "tt" + CssValues.Hash(css);
        context.Sheet.AddRule(name, string.Concat(css.Split('|').Select(rule => "." + name + rule)) + "\n");
        return name;
    }

    private static void WriteCells(HtmlContext context, Control container, TableColumns columns)
    {
        var cells = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(container).OfType<TableViewCell>().ToList();
        foreach (var column in columns.Written)
        {
            columns.Shown(column, 1, () => WriteCell(context, column, cells.FirstOrDefault(cell => cell.Column == column)));
        }
    }

    private static void WriteCell(HtmlContext context, TableViewColumn column, TableViewCell? cell)
    {
        var presenter = cell?.Column?.CellTemplate is not null ? TableColumns.Presenter(cell) : null;
        context.Plan.Literal($"<td{TableColumns.Align(column, presenter)}>");
        if (presenter is not null)
        {
            ItemParts.Content(context, presenter, ParentKind.Row);
        }
        else if (cell is not null)
        {
            context.Plan.Value(cell, ContentControl.ContentProperty);
        }

        context.Plan.Literal("</td>");
    }
}
