using Avalonia.Data;
using System.Collections.Immutable;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Templating;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Aioros.Avalonia.Html.Writers;

internal static class ItemParts
{
    public const string HiddenInput = "vh";

    /// <summary>
    /// While a plan is recorded, a stand-in item in a bound list that is empty (<see cref="StandIns"/>), so the list's
    /// writer learns its item's look and template; nothing otherwise.
    /// </summary>
    public static IDisposable? StandIn(HtmlContext context, ItemsControl items) =>
        context.Plan.Planning && items.ItemCount == 0 && LocalBindings.IsBound(items, ItemsControl.ItemsSourceProperty) ? StandIns.Fill(items) : null;

    public static IReadOnlyList<Control> Containers(ItemsControl items)
    {
        if (items.ItemsPanelRoot is VirtualizingPanel virtualizing)
        {
            var orientation = virtualizing is VirtualizingStackPanel stack ? stack.Orientation : Orientation.Horizontal;
            items.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = orientation });
            Dispatcher.UIThread.RunJobs();
            ThemeLooks.ExpandTemplates(items);
        }

        return [.. Enumerable.Range(0, items.ItemCount).Select(items.ContainerFromIndex).OfType<Control>()];
    }

    /// <summary>
    /// <c> data-change</c> when the selection is bound two-way, so the model follows it: the framework's script posts the
    /// selection when it changes in the browser (LiveView's <c>phx-change</c>, htmx's <c>hx-trigger="change"</c>), and the
    /// answer shows what depends on it, such as a command it enables or a text bound to the selected item.
    /// </summary>
    public static string PostsChange(SelectingItemsControl list) =>
        new global::Avalonia.AvaloniaProperty[] { SelectingItemsControl.SelectedIndexProperty, SelectingItemsControl.SelectedItemProperty }
            .Any(property => (LocalBindings.Of(list, property) ?? (list.TemplatedParent is null ? null : BindingOperations.GetBindingExpressionBase(list, property))) is { } binding && BindingModes.WritesToSource(binding))
            ? " data-change"
            : string.Empty;

    public static string Input(HtmlContext context, bool multiple)
    {
        context.Sheet.AddRule(
            HiddenInput,
            $".{HiddenInput}{{position:absolute;opacity:0;width:1px;height:1px;margin:0;padding:0;border:0;pointer-events:none}}:has(>.{HiddenInput}:focus-visible){{outline:2px solid;outline-offset:-2px}}\n");
        return $"<input type=\"{(multiple ? "checkbox" : "radio")}\" class=\"{HiddenInput}\"";
    }

    public static (Slot? Items, ImmutableArray<ImmutableArray<IPlanNode>> Bodies) Bodies(HtmlContext context, ItemsControl control, IReadOnlyList<Control> containers, Action<Control> write)
    {
        if (context.Plan.Planning && context.Plan.BoundPath(control, ItemsControl.ItemsSourceProperty) is { } path)
        {
            if (containers.Count == 0)
            {
                context.Plan.Fail("an empty list: there is no item to learn the item template from");
                return (null, []);
            }

            context.Plan.Begin(containers[0].DataContext);
            write(containers[0]);
            return (Slots.Path(path), [context.Plan.End()]);
        }

        var bound = !context.Plan.Planning && LocalBindings.IsBound(control, ItemsControl.ItemsSourceProperty);
        var key = bound ? ItemIdentity.Getter(ItemIdentity.Path(control, containers.Count == 0 ? null : containers[0].DataContext)) : null;
        var mark = bound ? context.Fields.EnterList() : default;
        var outer = context.Fields.ItemKey;
        var bodies = ImmutableArray.CreateBuilder<ImmutableArray<IPlanNode>>(containers.Count);
        foreach (var container in containers)
        {
            context.Plan.Begin(context.Plan.Scope);
            if (bound)
            {
                context.EnterListItem(outer, key, container.DataContext);
            }

            write(container);
            if (bound)
            {
                context.EnterItem(outer);
            }

            bodies.Add(context.Plan.End());
        }

        if (bound)
        {
            context.Fields.LeaveList(mark);
        }

        return (null, bodies.MoveToImmutable());
    }

    public static (ChoicePredicate Predicate, SlotSpec? Spec) Selection(HtmlContext context, SelectingItemsControl list, bool repeat)
    {
        if (context.Plan.Planning && context.Plan.BoundPath(list, SelectingItemsControl.SelectedIndexProperty) is { } index)
        {
            return Described(new SlotSpec("index-path", index));
        }

        if (context.Plan.Planning && context.Plan.BoundPath(list, SelectingItemsControl.SelectedItemProperty) is { } path)
        {
            if (repeat)
            {
                return Described(new SlotSpec("item-path", path));
            }

            var items = list.Items.ToArray();
            if (items.All(item => item is string))
            {
                return Described(new SlotSpec("item-text", path) { Values = [.. items.Cast<string>()] });
            }

            var selectedItem = PropertyPaths.Getter(path);
            return ((scope, position, _) => position < items.Length && Equals(selectedItem(scope), items[position]), null);
        }

        if (context.Plan.Planning && list is ListBox && LocalBindings.IsBound(list, ListBox.SelectedItemsProperty) && ChosenItems(context, list, repeat) is { } chosen)
        {
            return chosen;
        }

        IEnumerable<int> selected = list is ListBox box ? box.Selection.SelectedIndexes : [list.SelectedIndex];
        return Described(new SlotSpec("indexes") { Values = [.. selected.Select(at => at.ToString(System.Globalization.CultureInfo.InvariantCulture))] });
    }

    /// <summary>
    /// The selection of a list whose <c>SelectedItems</c> is bound to a model collection: an item is selected while the
    /// collection holds it, matched as Avalonia's selection model matches it; nothing (and a plan failure) for a path the
    /// plan cannot follow.
    /// </summary>
    private static (ChoicePredicate Predicate, SlotSpec? Spec)? ChosenItems(HtmlContext context, SelectingItemsControl list, bool repeat)
    {
        if (context.Plan.BoundPath(list, ListBox.SelectedItemsProperty) is not { } path)
        {
            context.Plan.Fail("ListBox.SelectedItems is bound to a path the plan cannot follow");
            return null;
        }

        if (repeat)
        {
            return Described(new SlotSpec("items-path", path));
        }

        var items = list.Items.ToArray();
        var selected = PropertyPaths.Getter(path);
        return ((scope, position, _) => position < items.Length && selected(scope) is System.Collections.IEnumerable chosen && chosen.Cast<object?>().Contains(items[position]), null);
    }

    public static ContentPresenter? Presenter(Control container, string name) =>
        container as ContentPresenter ?? ItemLooks.Part<ContentPresenter>(container, name);

    public static void Content(HtmlContext context, ContentPresenter? presenter, ParentKind parent, bool looked = false)
    {
        if (presenter is null)
        {
            return;
        }

        if (presenter.Child is { } child && !IsPlainText(presenter, child))
        {
            if (looked)
            {
                context.WriteInItem(presenter, child, parent);
            }
            else
            {
                context.Write(child, parent);
            }

            return;
        }

        if (context.Plan.Planning && presenter.Content is not null && ReferenceEquals(presenter.Content, context.Plan.Scope))
        {
            context.Plan.Add(new ValueNode(Slots.Self.Getter) { Source = Slots.Self.Spec });
            return;
        }

        context.Plan.Text(PlanValues.Format(presenter.Content));
    }

    public static IReadOnlyList<int> Indices(string? value) => Runtime.Slots.FieldValues.Indices(value);

    private static (ChoicePredicate Predicate, SlotSpec? Spec) Described(SlotSpec spec) => (Slots.Predicate(spec), spec);

    private static bool IsPlainText(ContentPresenter presenter, Control child) =>
        child is TextBlock text && presenter.Content is not Control && !LocalBindings.IsBound(text, TextBlock.TextProperty)
        && string.Equals(text.Text, PlanValues.Format(presenter.Content), StringComparison.Ordinal);
}
