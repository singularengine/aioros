using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A table's columns: the ones a render writes, each under the condition of its bound visibility (Aioros.Cosmo's column
/// visibility, TanStack Table's <c>columnVisibility</c>), their widths, and their headers written as cells are. While a
/// plan is recorded, a column a bound visibility hides is shown for the trace, so its header and cells are realized and
/// written; its visibility is read at the table's scope, and from a row through the scope above the row's item.
/// </summary>
internal sealed class TableColumns : IDisposable
{
    private readonly HtmlContext _context;
    private readonly TableView _table;
    private readonly Dictionary<TableViewColumn, (string Path, bool Negated)> _flags = [];
    private readonly List<IDisposable> _shown = [];

    public TableColumns(HtmlContext context, TableView table)
    {
        (_context, _table) = (context, table);
        if (context.Plan.Planning)
        {
            foreach (var column in table.Columns.Where(Bound))
            {
                Follow(column);
            }

            _shown.AddRange(table.Columns.Where(column => !column.IsVisible && Bound(column))
                .Select(column => LocalBindings.Override(column, TableViewColumn.IsVisibleProperty, true)).OfType<IDisposable>());
            if (_shown.Count > 0)
            {
                Settled.Rebuilt(table);
            }
        }

        Written = [.. table.Columns.Where(column => column.IsVisible)];
    }

    /// <summary>The columns a render writes: the shown ones, and while a plan is recorded also those a bound visibility hides.</summary>
    public IReadOnlyList<TableViewColumn> Written { get; }

    public void Dispose()
    {
        if (_shown.Count > 0)
        {
            _shown.ForEach(lease => lease.Dispose());
            Settled.Rebuilt(_table);
        }
    }

    /// <summary>Writes a part of a column (its width, header or cell) under the condition of its bound visibility, read <paramref name="depth"/> scopes above the current one, or as it is.</summary>
    public void Shown(TableViewColumn column, int depth, Action write)
    {
        if (!_context.Plan.Planning || !_flags.TryGetValue(column, out var flag))
        {
            write();
            return;
        }

        var visible = Flags.Of(Above(flag.Path, depth), flag.Negated, fallback: true);
        _context.Plan.Begin(_context.Plan.Scope);
        write();
        _context.Plan.Add(new ConditionNode(visible.Getter, _context.Plan.End()) { Source = visible.Spec });
    }

    /// <summary>
    /// The columns' widths: pixels, and stars as their share of the stars in percent, which the fixed table layout scales
    /// into what the pixel columns leave (a <c>calc()</c> mixing a percentage and pixels would be <c>auto</c> there); when a
    /// column's visibility is bound, the shown stars are summed as the colgroup's <c>--ts</c>, each term under its column's
    /// condition.
    /// </summary>
    public void Group()
    {
        var stars = Written.Where(column => column.Width.IsStar).ToList();
        var summed = _table.Columns.Any(Bound) && _table.Columns.Any(column => column.Width.IsStar && (column.IsVisible || Bound(column)));
        _context.Plan.Literal(summed ? "<colgroup style=\"--ts:calc(0" : "<colgroup>");
        if (summed)
        {
            stars.ForEach(column => Shown(column, 0, () => _context.Plan.Literal($" + {CssValues.Num(column.Width.Value)}")));
            _context.Plan.Literal(")\">");
        }

        var star = stars.Sum(column => column.Width.Value);
        foreach (var column in Written)
        {
            var width = column.Width;
            var col = width.GridUnitType switch
            {
                GridUnitType.Pixel => $"<col style=\"width:{CssValues.Px(width.Value)}\">",
                GridUnitType.Star when summed => $"<col style=\"width:calc(100% * {CssValues.Num(width.Value)} / var(--ts))\">",
                GridUnitType.Star when star > 0 => $"<col style=\"width:{Share(width.Value, star)}\">",
                _ => "<col>",
            };
            Shown(column, 0, () => _context.Plan.Literal(col));
        }

        _context.Plan.Literal("</colgroup>");
    }

    /// <summary>A star column's share of the stars, in percent.</summary>
    public static string Share(double star, double stars) => CssValues.Num(star / stars * 100) + "%";

    /// <summary>The headers, each a <c>&lt;th&gt;</c> holding its header's content as Avalonia presents it: a control or a template's content written as a cell's is, a text as text.</summary>
    public void Headers()
    {
        var headers = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(_table).OfType<TableViewColumnHeader>().ToList();
        _context.Plan.Literal("<thead><tr>");
        foreach (var column in Written)
        {
            Shown(column, 0, () => Header(column, headers.FirstOrDefault(header => header.Column == column)));
        }

        _context.Plan.Literal("</tr></thead>");
    }

    /// <summary>The content presenter of a header or a cell, its templates applied, so its content is the control Avalonia shows.</summary>
    public static ContentPresenter? Presenter(TemplatedControl part)
    {
        ThemeLooks.ExpandTemplates(part);
        return ItemLooks.Part<ContentPresenter>(part, "PART_ContentPresenter");
    }

    /// <summary>
    /// The class that aligns a header's or a cell's content: the column's content alignment, or, when the column stretches
    /// its content, the alignment its content asks for (a right-aligned amount).
    /// </summary>
    public static string Align(TableViewColumn column, ContentPresenter? presenter) =>
        (column.HorizontalContentAlignment == HorizontalAlignment.Stretch && presenter?.Child is { } child ? child.HorizontalAlignment : column.HorizontalContentAlignment) switch
        {
            HorizontalAlignment.Right => " class=\"r\"",
            HorizontalAlignment.Center => " class=\"m\"",
            _ => string.Empty,
        };

    private void Header(TableViewColumn column, TableViewColumnHeader? header)
    {
        var presenter = header is not null && (column.Header is Control || column.HeaderTemplate is not null) ? Presenter(header) : null;
        _context.Plan.Literal($"<th{Align(column, presenter)}>");
        if (presenter is null)
        {
            _context.Plan.Value(column, TableViewColumn.HeaderProperty);
        }
        else if (column.Header is Control)
        {
            ItemParts.Content(_context, presenter, ParentKind.Row);
        }
        else if (LocalBindings.IsBound(column, TableViewColumn.HeaderProperty))
        {
            _context.Plan.Fail("TableViewColumn.Header is bound and shown through a HeaderTemplate, which the plan cannot follow");
            ItemParts.Content(_context, presenter, ParentKind.Row);
        }
        else
        {
            _context.Plan.WriteConstant(presenter, () => ItemParts.Content(_context, presenter, ParentKind.Row));
        }

        _context.Plan.Literal("</th>");
    }

    private void Follow(TableViewColumn column)
    {
        if (_context.Plan.NegatedPath(column, TableViewColumn.IsVisibleProperty) is { } negated)
        {
            _flags[column] = (negated, true);
        }
        else if (_context.Plan.BoundPath(column, TableViewColumn.IsVisibleProperty) is { } path)
        {
            _flags[column] = (path, false);
        }
        else
        {
            _context.Plan.Fail("TableViewColumn.IsVisible is bound to a path the plan cannot follow");
        }
    }

    /// <summary>A path of the table's scope read <paramref name="depth"/> scopes below it (<c>^1.Path</c> from a row).</summary>
    private static string Above(string path, int depth)
    {
        if (depth == 0)
        {
            return path;
        }

        if (!path.StartsWith('^'))
        {
            return string.Create(CultureInfo.InvariantCulture, $"^{depth}.{path}");
        }

        var dot = path.IndexOf('.', StringComparison.Ordinal);
        var levels = int.Parse(path.AsSpan(1, (dot < 0 ? path.Length : dot) - 1), CultureInfo.InvariantCulture) + depth;
        return string.Create(CultureInfo.InvariantCulture, $"^{levels}{(dot < 0 ? string.Empty : path[dot..])}");
    }

    private static bool Bound(TableViewColumn column) => LocalBindings.IsBound(column, TableViewColumn.IsVisibleProperty);
}
