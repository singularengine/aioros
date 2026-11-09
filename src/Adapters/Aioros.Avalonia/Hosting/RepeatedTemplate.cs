using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Controls;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// A list's item template whose items place repeated views: it builds each item as the list's own template does, then
/// gives each view it placed the model of that item from the view's slot, found by the item's key, as React gives each
/// keyed child its own state. It is the list's template while the page is shown, so the items a list realizes later, and
/// the ones it recycles, take their models too.
/// </summary>
internal sealed class RepeatedTemplate(IDataTemplate inner, Control owner, XamlSources sources, PageDefinition page, ImmutableArray<int> instances) : IDataTemplate
{
    /// <summary>The page's models, updated on the UI thread when the page binds again.</summary>
    public ImmutableArray<object?> Models { get; set; } = [];

    /// <summary>The list's item template property, when the control has one set: an <c>ItemsControl</c>'s or any control's named <c>ItemTemplate</c>.</summary>
    public static AvaloniaProperty? ItemTemplateOf(Control control) =>
        AvaloniaPropertyRegistry.Instance.FindRegistered(control, "ItemTemplate") is { } property && control.GetValue(property) is IDataTemplate ? property : null;

    public bool Match(object? data) => inner.Match(data);

    public Control? Build(object? param)
    {
        var built = inner.Build(param);
        if (built is not null)
        {
            Place(built, param);
        }

        return built;
    }

    /// <summary>Gives the realized item controls of the list the current models.</summary>
    public void Refresh()
    {
        foreach (var control in owner.GetVisualDescendants().OfType<Control>().Where(control => View.GetItem(control) is not null && instances.Contains(View.GetSlot(control) - page.FirstView)).ToList())
        {
            Apply(control, View.GetSlot(control) - page.FirstView, control.GetValue(View.ListItemProperty));
        }
    }

    private void Place(Control built, object? item)
    {
        var candidates = built.GetSelfAndLogicalDescendants().OfType<Control>().ToList();
        var used = new HashSet<Control>();
        foreach (var instance in instances)
        {
            if (candidates.FirstOrDefault(control => !used.Contains(control) && PageComposer.Shows(control, page.Views[instance].View)) is { } control)
            {
                used.Add(control);
                Apply(control, instance, item);
            }
        }
    }

    private void Apply(Control control, int instance, object? item)
    {
        var slot = page.FirstView + instance;
        var held = slot < Models.Length ? Models[slot] : null;
        var key = KeyOf(held as ViewItems, page.Views[instance].List!, item);
        var model = held is ViewItems items && key is not null ? items.Model(key) : held as ViewState ?? ViewState.Hidden;
        PageComposer.Show(control, sources, page, instance, model, key ?? string.Empty, item);
    }

    /// <summary>An item's key: its declared key's text, or its position among the models' items or else in the list.</summary>
    private string? KeyOf(ViewItems? items, ViewList list, object? item) =>
        items?.KeyOf(item) ?? (list.Key is { } key ? ViewItems.Format(key(item)) : Position(item));

    private string? Position(object? item) =>
        owner is ItemsControl list && list.Items.IndexOf(item) is var index and >= 0 ? index.ToString(CultureInfo.InvariantCulture) : null;
}
