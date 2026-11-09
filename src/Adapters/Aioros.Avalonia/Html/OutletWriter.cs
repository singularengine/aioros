using Aioros.Markup;
using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Html;

/// <summary>
/// Writes what a layout's outlet holds: the first outlet of a page without its own <c>main</c> as the page's
/// <c>main</c>, the innermost page between the region marks navigation sends alone, and the page's model as a scope.
/// </summary>
internal sealed class OutletWriter(HtmlContext context)
{
    /// <summary>Marks the page inside its layouts, so navigation can send the page alone when the layouts did not change.</summary>
    public const string PageOpen = PageRegion.Open;

    public const string PageClose = PageRegion.Close;

    private bool _mainWritten;

    public void Write(Outlet outlet, ParentKind parent)
    {
        if (outlet.Content is not Control content)
        {
            return;
        }

        var plan = context.Plan;
        var main = !_mainWritten && !HasMain(outlet, content);
        _mainWritten |= main;
        var page = !content.GetLogicalDescendants().OfType<Outlet>().Any();
        plan.Literal(main ? "<main style=\"display:contents\">" : string.Empty);
        plan.Literal(page ? PageOpen : string.Empty);
        var index = context.Models.IndexOf(content.DataContext, ReferenceEqualityComparer.Instance);
        if (!plan.Planning || index < 0)
        {
            context.Write(content, parent);
        }
        else
        {
            plan.Begin(content.DataContext);
            context.Write(content, parent);
            plan.Add(new ScopeNode(index, plan.End()));
        }

        plan.Literal(page ? PageClose : string.Empty);
        plan.Literal(main ? "</main>" : string.Empty);
    }

    private static bool HasMain(Outlet outlet, Control content) =>
        outlet.GetLogicalAncestors().OfType<Control>().Concat(content.GetLogicalDescendants().OfType<Control>().Prepend(content))
            .Any(control => Web.GetTag(control) == "main");
}
