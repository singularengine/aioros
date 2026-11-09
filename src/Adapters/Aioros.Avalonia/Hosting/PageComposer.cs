using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Plans;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

public static class PageComposer
{
    public static Control Compose(XamlSources sources, PageDefinition page, ImmutableArray<object?> models)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(page);
        var chain = Chain([.. page.Sources.Select(source => (Func<Control>)(() => sources.Load(source)))], models);
        for (var segment = 0; segment < chain.Length; segment++)
        {
            BindViews(sources, page, chain[segment], segment, models);
        }

        return chain[0];
    }

    public static Control Compose(IReadOnlyList<Func<Control>> controls, ImmutableArray<object?> models) =>
        Chain(controls, models) is [var root, ..] ? root : throw new ArgumentException("A page needs at least one source.", nameof(controls));

    /// <summary>Creates each control with its model and places each one in the outlet of the one before, outermost first.</summary>
    public static ImmutableArray<Control> Chain(IReadOnlyList<Func<Control>> controls, ImmutableArray<object?> models)
    {
        ArgumentNullException.ThrowIfNull(controls);
        var chain = ImmutableArray.CreateBuilder<Control>(controls.Count);
        for (var index = 0; index < controls.Count; index++)
        {
            var control = controls[index]();
            control.DataContext = index < models.Length ? models[index] : null;
            FreshValues.Snapshot(control, control.DataContext);
            if (chain.Count > 0 && OutletOf(chain[^1]) is { } outlet)
            {
                outlet.Content = control;
            }

            chain.Add(control);
        }

        if (chain.Count > 0)
        {
            SystemFaces.Load(chain[0]);
        }

        return chain.MoveToImmutable();
    }

    public static Outlet? OutletOf(Control layout) => layout.GetLogicalDescendants().OfType<Outlet>().FirstOrDefault();

    /// <summary>
    /// Gives each view the segment's XAML places (nested ones included) its own model from its slot: a <c>UserControl</c>
    /// with <c>x:Class</c> becomes its view model's view and an <c>a:View</c> loads its source. A view whose slot holds
    /// <see cref="ViewState.Failed"/> shows the app's view error template (or a built-in message) instead of its content,
    /// and one holding <see cref="ViewState.Hidden"/> shows nothing. Instances are matched in document order by their
    /// class or source, within the segment and the view around them.
    /// </summary>
    public static void BindViews(XamlSources sources, PageDefinition page, Control segment, int index, ImmutableArray<object?> models)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(segment);
        if (!page.Views.IsEmpty)
        {
            Bind(segment, index, -1, new ViewBinding(sources, page, models, new bool[page.Views.Length], []));
        }
    }

    /// <summary>
    /// The model slot of the innermost view instance holding the control (itself included), or <c>-1</c> when the control
    /// belongs to the page or a layout: where a command it runs belongs.
    /// </summary>
    public static int ViewSlot(Control control) => ViewTarget(control).Slot;

    /// <summary>
    /// The innermost view instance holding the control (itself included), with its item in a repeated view, or the page's
    /// target when the control belongs to the page or a layout: where a command it runs belongs.
    /// </summary>
    public static ViewTarget ViewTarget(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        var view = control.GetSelfAndLogicalAncestors().Concat(control.GetVisualAncestors()).OfType<Control>().FirstOrDefault(static candidate => View.GetSlot(candidate) >= 0);
        return view is null ? Runtime.Rendering.ViewTarget.Page : new ViewTarget(View.GetSlot(view), View.GetItem(view));
    }

    /// <summary>The control shown where a view failed: the app's <c>Pages/ViewError.axaml</c>, or a built-in message.</summary>
    public static Control ViewError(XamlSources sources, PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(page);
        return page.ViewError is { } source
            ? sources.Load(source)
            : new Border { Padding = new Thickness(12), Child = new TextBlock { Text = "This part of the page could not be loaded.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap } };
    }

    private sealed record ViewBinding(XamlSources Sources, PageDefinition Page, ImmutableArray<object?> Models, bool[] Assigned, Dictionary<(int Segment, int Parent), int> Lists)
    {
        /// <summary>The ordinal of the next list with an item template in a host, counted in document order as the generator does.</summary>
        public int NextList(int segment, int parent)
        {
            var next = Lists.GetValueOrDefault((segment, parent));
            Lists[(segment, parent)] = next + 1;
            return next;
        }
    }

    private static void Bind(Control root, int segment, int parent, ViewBinding binding)
    {
        foreach (var child in root.GetLogicalChildren().OfType<Control>().ToList())
        {
            if (child is Outlet)
            {
                continue;
            }

            if (RepeatedTemplate.ItemTemplateOf(child) is { } template)
            {
                BindList(child, template, segment, parent, binding.NextList(segment, parent), binding);
            }

            var instance = Match(child, segment, parent, binding);
            if (instance >= 0)
            {
                Place(child, instance, binding);
            }

            Bind(child, segment, instance >= 0 ? instance : parent, binding);
        }
    }

    /// <summary>
    /// Gives the views a list's item template places their models per item: the list's template is wrapped once, so each
    /// item's view control takes its own model from its slot by the item's key as the list realizes it, and the realized
    /// ones take the current models when the page binds again.
    /// </summary>
    private static void BindList(Control list, AvaloniaProperty property, int segment, int parent, int ordinal, ViewBinding binding)
    {
        var views = binding.Page.Views;
        ImmutableArray<int> instances = [.. Enumerable.Range(0, views.Length).Where(index => views[index] is { List: { } placed } instance && placed.List == ordinal && instance.Segment == segment && instance.Parent == parent)];
        if (instances.IsEmpty)
        {
            return;
        }

        if (list.GetValue(property) is RepeatedTemplate repeated)
        {
            repeated.Models = binding.Models;
            repeated.Refresh();
            return;
        }

        list.SetValue(property, new RepeatedTemplate((global::Avalonia.Controls.Templates.IDataTemplate)list.GetValue(property)!, list, binding.Sources, binding.Page, instances) { Models = binding.Models });
    }

    private static int Match(Control control, int segment, int parent, ViewBinding binding)
    {
        var views = binding.Page.Views;
        for (var index = 0; index < views.Length; index++)
        {
            var instance = views[index];
            if (!binding.Assigned[index] && instance.List is null && instance.Segment == segment && instance.Parent == parent && Shows(control, instance.View))
            {
                binding.Assigned[index] = true;
                return index;
            }
        }

        return -1;
    }

    internal static bool Shows(Control control, ViewDefinition view) =>
        view.Type is { } type ? control.GetType() == type() : control is View { Source: { } source } && source.TrimStart('/') == view.Source;

    private static void Place(Control control, int instance, ViewBinding binding)
    {
        var slot = binding.Page.FirstView + instance;
        Show(control, binding.Sources, binding.Page, instance, slot < binding.Models.Length ? binding.Models[slot] : null);
    }

    /// <summary>
    /// Shows a view instance's model in its control: its slot and key (and, in a repeated view, its item), its model as the
    /// <c>DataContext</c>, the view error template when it failed and nothing when it is hidden.
    /// </summary>
    internal static void Show(Control control, XamlSources sources, PageDefinition page, int instance, object? model, string? item = null, object? listItem = null)
    {
        control.SetValue(View.SlotProperty, page.FirstView + instance);
        control.SetValue(View.KeyProperty, page.Views[instance].Key);
        if (item is not null)
        {
            control.SetValue(View.ItemProperty, item);
            control.SetValue(View.ListItemProperty, listItem);
        }

        if (control is not ContentControl host)
        {
            control.DataContext = model is ViewState ? null : model;
            FreshValues.Snapshot(control, control.DataContext);
            return;
        }

        var held = control.GetValue(View.HeldProperty);
        if (held is null && (control is not View || model is not ViewState))
        {
            held = control is View { Source: { } source } ? sources.Load(source.TrimStart('/')) : host.Content;
            control.SetValue(View.HeldProperty, held);
        }

        var content = ReferenceEquals(model, ViewState.Failed) ? ViewError(sources, page) : ReferenceEquals(model, ViewState.Hidden) ? null : held;
        control.DataContext = model is ViewState ? null : model;
        if (!ReferenceEquals(host.Content, content))
        {
            host.Content = content;
        }

        FreshValues.Snapshot(control, control.DataContext);
    }

    /// <summary>
    /// Disposes every binding of the subtree that writes into its model, so that tearing it down never changes a model.
    /// The rest is left to the garbage collector with the detached controls. A styled property is looked at only when it is
    /// set (<see cref="AvaloniaObject.IsSet"/>, true for a binding targeting it), a direct one always.
    /// </summary>
    public static void Release(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var seen = new HashSet<Control>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<Control>();
        pending.Push(root);
        while (pending.TryPop(out var control))
        {
            if (!seen.Add(control))
            {
                continue;
            }

            ReleaseBindings(control);
            foreach (var child in control.GetVisualChildren())
            {
                if (child is Control visual)
                {
                    pending.Push(visual);
                }
            }

            foreach (var child in control.GetLogicalChildren())
            {
                if (child is Control logical)
                {
                    pending.Push(logical);
                }
            }
        }
    }

    private static void ReleaseBindings(Control control)
    {
        foreach (var property in RegisteredProperties.Of(control.GetType()))
        {
            if ((property.IsDirect || control.IsSet(property)) && LocalBindings.Of(control, property) is { } binding && BindingModes.WritesToSource(binding))
            {
                binding.Dispose();
            }
        }
    }
}
