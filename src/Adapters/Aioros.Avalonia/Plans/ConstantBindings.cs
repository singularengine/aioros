using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// Which bindings read constants of the page rather than the model: bindings to elements that hold what their XAML
/// gives them, template bindings of such elements, items of constant lists, and the values a plan learns one by one.
/// </summary>
internal static class ConstantBindings
{
    [ThreadStatic]
    private static HashSet<(AvaloniaObject, AvaloniaProperty)>? _learnedConstants;

    [ThreadStatic]
    private static HashSet<ItemsControl>? _branches;

    /// <summary>Whether a container of a tree is being learned (<see cref="Branch"/>).</summary>
    public static bool Branching => _branches is { Count: > 0 };

    /// <summary>Whether the list is a container of a tree being learned, whose children the tree writes (<see cref="Html.Templating.Hierarchies"/>).</summary>
    public static bool IsBranch(ItemsControl list) => _branches?.Contains(list) == true;

    /// <summary>Marks a container of a tree as one whose children the tree writes, while its looks are learned.</summary>
    public static IDisposable Branch(ItemsControl container)
    {
        (_branches ??= []).Add(container);
        return new Unbranch(container);
    }

    private sealed class Unbranch(ItemsControl container) : IDisposable
    {
        public void Dispose() => _branches?.Remove(container);
    }

    /// <summary>
    /// Marks a templated control's property the plan learns by value (<see cref="Html.Variants.LibraryValues"/>) while the control is
    /// written, so its template's bindings that read it are written as constants of each learned value.
    /// </summary>
    public static IDisposable LearnedConstant(AvaloniaObject control, AvaloniaProperty property)
    {
        (_learnedConstants ??= []).Add((control, property));
        return new Unlearned((control, property));
    }

    /// <summary>Whether the property is learned by value while its control is written, so what it gives is a constant of each learned value.</summary>
    public static bool IsLearnedConstant(AvaloniaObject control, AvaloniaProperty property) => _learnedConstants?.Contains((control, property)) == true;

    private sealed class Unlearned((AvaloniaObject, AvaloniaProperty) entry) : IDisposable
    {
        public void Dispose() => _learnedConstants?.Remove(entry);
    }

    /// <summary>
    /// For a path read from another element's property: the first binding of that element that reads the model, empty
    /// when it has none (or the property is styled, not inherited and still at its default, so no other value feeds it),
    /// or nothing when the path does not read an element's property. A property the plan learns by value while the
    /// element is written (<see cref="LearnedConstant"/>) reads no model there: what it gives is a constant of each value.
    /// </summary>
    public static string? ElementModelBinding(AvaloniaObject target, string path, int depth)
    {
        if (target is not StyledElement element || ElementBindings.Parse(path.TrimStart('!')) is not { Success: true } match
            || !match.Groups["property"].Success || ElementBindings.Source(element, match) is not { } source)
        {
            return null;
        }

        var name = match.Groups["property"].Value;
        var read = AvaloniaPropertyRegistry.Instance.GetRegistered(source).FirstOrDefault(candidate => candidate.Name == name);
        if (read is not null && IsLearnedConstant(source, read))
        {
            return string.Empty;
        }

        if (read is { IsDirect: false, Inherits: false } && OwnValue(source, read, depth))
        {
            return string.Empty;
        }

        if (read is { IsDirect: true } && source is ItemsControl container && IsBranch(container) && BindingOperations.GetBindingExpressionBase(source, read) is null)
        {
            return string.Empty;
        }

        return AvaloniaPropertyRegistry.Instance.GetRegistered(source).Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(source.GetType()))
            .Where(property => !IsLearnedConstant(source, property) && !EnabledCommand(source, property) && ReadsModel(source, property, depth))
            .Select(property => $"{source.GetType().Name}.{property.Name}")
            .FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Whether the property is the command of a control whose enabled state is learned (<see cref="Html.Variants.EnabledStates"/>):
    /// a command shows on the control only through the state it enables, a constant of each state.
    /// </summary>
    private static bool EnabledCommand(StyledElement source, AvaloniaProperty property) =>
        source is ICommandSource && property.Name is "Command" or "CommandParameter" && IsLearnedConstant(source, InputElement.IsEnabledProperty);

    /// <summary>
    /// Whether a styled property holds a constant whatever the element's other bindings: its default, or a binding of
    /// its own to an element that holds a constant.
    /// </summary>
    private static bool OwnValue(StyledElement element, AvaloniaProperty property, int depth) =>
        BindingOperations.GetBindingExpressionBase(element, property) is null
            ? element.GetDiagnostic(property) is { Priority: BindingPriority.Unset, IsOverriddenCurrentValue: false }
            : LocalBindings.Of(element, property) is not null && !ReadsModel(element, property, depth);

    private static bool ReadsModel(StyledElement element, AvaloniaProperty property, int depth)
    {
        if (LocalBindings.Of(element, property) is { } binding)
        {
            if (element.TemplatedParent is StyledElement parent && TemplateBindings.Property(binding, parent) is { } shown)
            {
                return depth >= 4 || !ConstantProperty(parent, shown);
            }

            return !ElementBindings.IsElementReference(binding) && !(element is Control control && CommandPaths.IsElementMethod(control, property))
                && (depth >= 4 || binding is not IDescription { Description: { } path } || ElementBindings.IsSimplePath(path) || ElementModelBinding(element, path, depth + 1) is not "");
        }

        return MultiBindings.Has(element, property) && BindingOperations.GetBindingExpressionBase(element, property) is { } multi
            && !BindingSources.IsConstant(multi, element) && !TemplateBindings.ReadsConstantTemplate(element, multi);
    }

    public static bool ConstantProperty(StyledElement element, AvaloniaProperty property) =>
        BindingOperations.GetBindingExpressionBase(element, property) is not { } binding || (LocalBindings.Of(element, property) is not null && !ReadsModel(element, property, 0))
        || _learnedConstants?.Contains((element, property)) == true
        || (LocalBindings.Of(element, property) is null && binding is IDescription { Description: { } path } && !ElementBindings.IsSimplePath(path) && ElementModelBinding(element, path, 1) is "");

    /// <summary>
    /// Whether an element's data context is an item of a list whose items are constants of the page: a list set in the
    /// XAML (<c>x:Static</c>), or by a control template from a property its control holds as a constant (the palette of
    /// Semi's <c>ColorView</c>). Its bindings read that item and no model, so they give the same value on every render.
    /// </summary>
    public static bool ConstantItem(StyledElement element)
    {
        if (LearnedContext(element))
        {
            return true;
        }

        for (var current = element as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Control container && ItemsControl.ItemsControlFromItemContainer(container) is { } owner)
            {
                return Equals(container.DataContext, owner.ItemFromContainer(container)) && ConstantItems(owner);
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an element's data context is a value its library control is learned by (<see cref="LearnedConstant"/>), as
    /// the text a presenter builds for a learned header: its bindings read that value, the same for each learned case.
    /// </summary>
    private static bool LearnedContext(StyledElement element) =>
        _learnedConstants is { Count: > 0 } learned && element.DataContext is { } context
        && learned.Any(entry => entry.Item1 is Visual control && ((Visual)element).GetVisualAncestors().Contains(control) && Equals(control.GetValue(entry.Item2), context));

    private static bool ConstantItems(ItemsControl owner) =>
        IsLearnedConstant(owner, ItemsControl.ItemsSourceProperty)
        || BindingOperations.GetBindingExpressionBase(owner, ItemsControl.ItemsSourceProperty) is not { } binding
        || BindingSources.IsConstant(binding, owner)
        || (owner.TemplatedParent is StyledElement parent && TemplateBindings.Property(binding, parent) is { } property && ConstantProperty(parent, property));
}
