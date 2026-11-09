using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// Bindings to another element (<c>$parent[Type]</c>, <c>$parent[Type;level]</c>, <c>$visualParent[...]</c> or
/// <c>#name</c>): the syntax of their paths and the element they name.
/// </summary>
internal static partial class ElementBindings
{
    /// <summary>Whether a path reads members of the data context alone (<c>Title</c>, <c>Order.Total</c>, <c>Items^</c>), the paths a slot follows.</summary>
    public static bool IsSimplePath(string path) => SimplePath().IsMatch(path);

    /// <summary>The parts of a binding to another element: its <c>kind</c>, <c>type</c>, <c>level</c> or <c>name</c>, the <c>property</c> it reads and the <c>rest</c> of its path.</summary>
    public static Match Parse(string description) => AncestorBinding().Match(description);

    /// <summary>The element a binding to one of its members names (<c>$parent[Type].Member</c>, <c>#name.Member</c>), or nothing.</summary>
    public static StyledElement? ElementOf(AvaloniaObject target, string description) =>
        target is StyledElement element && AncestorBinding().Match(description) is { Success: true } match && match.Groups["property"].Success ? Source(element, match) : null;

    /// <summary>A binding to an element itself (<c>{Binding #name}</c>, <c>{Binding $parent[Type]}</c>), which reads no model data.</summary>
    public static bool IsElementReference(BindingExpressionBase binding) =>
        (binding as IDescription)?.Description is { } description && ElementOnly().IsMatch(description);

    /// <summary>The element a parsed binding to another element names, or nothing when the tree holds none.</summary>
    public static StyledElement? Source(StyledElement element, Match match)
    {
        if (match.Groups["name"].Success)
        {
            var name = match.Groups["name"].Value;
            return element.GetLogicalAncestors().OfType<StyledElement>().FirstOrDefault(ancestor => ancestor.Name == name)
                ?? element.FindNameScope()?.Find<StyledElement>(name)
                ?? (element.TemplatedParent is Visual parent
                    ? parent.GetVisualDescendants().OfType<StyledElement>().FirstOrDefault(part => part.Name == name && ReferenceEquals(part.TemplatedParent, parent))
                    : null);
        }

        var ancestors = match.Groups["kind"].Value == "visualParent"
            ? (element as Visual)?.GetVisualAncestors().OfType<StyledElement>() ?? []
            : element.GetLogicalAncestors().OfType<StyledElement>();
        if (!match.Groups["type"].Success)
        {
            return ancestors.FirstOrDefault();
        }

        var type = match.Groups["type"].Value;
        type = type[(type.LastIndexOf(':') + 1)..];
        var level = match.Groups["level"].Success ? int.Parse(match.Groups["level"].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        return ancestors.Where(ancestor => Named(ancestor.GetType(), type)).Skip(level).FirstOrDefault();
    }

    private static bool Named(Type? type, string name)
    {
        if (type?.GetInterfaces().Any(contract => contract.Name == name) == true)
        {
            return true;
        }

        for (; type is not null; type = type.BaseType)
        {
            if (type.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^(?:(?:[A-Za-z_][A-Za-z0-9_]*\^?|(?=\[))(?:\[\d+\])*(?:\.[A-Za-z_][A-Za-z0-9_]*\^?(?:\[\d+\])*)*)?$")]
    private static partial Regex SimplePath();

    [GeneratedRegex(@"^(?:\$(?<kind>parent|visualParent)(?:\[(?<type>[A-Za-z_][\w:.]*)(?:\s*[;,]\s*(?<level>\d+))?\])?|#(?<name>[A-Za-z_]\w*))\.(?:\(\([^()]+\)DataContext\)|DataContext\([^()]+\)|DataContext|(?<property>[A-Za-z_]\w*))(?:\.(?<rest>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*))?$")]
    private static partial Regex AncestorBinding();

    [GeneratedRegex(@"^(?:#[A-Za-z_]\w*|\$(?:self|parent|visualParent)(?:\[[^\]]+\])?)$")]
    private static partial Regex ElementOnly();
}
