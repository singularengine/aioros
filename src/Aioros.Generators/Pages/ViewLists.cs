using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

/// <summary>
/// How a view placed in a list's item template repeats: the code of its <c>ViewList</c> (the host model's items, the
/// item's key and the key's parser), or why it cannot repeat. Every path is resolved against the view models' types at
/// build time, so the generated getters read members directly, without reflection.
/// </summary>
internal static class ViewLists
{
    public static (string? Code, string? Problem) Of(Compilation compilation, XamlList list, string? hostType, (string? Path, string Binding)? itemKey, PageLoader loader)
    {
        var host = hostType is null ? null : compilation.GetTypeByMetadataName(hostType);
        if (Unfollowable(list, host) is { } unfollowable)
        {
            return (null, unfollowable);
        }

        var items = list.Items is null ? null : Walk(host!, list.Items);
        if (items is null || !Enumerable(items.Value.Type, compilation))
        {
            return (null, $"its list's ItemsSource '{list.Binding}' is not a path of '{host!.ToDisplayString()}'" + (items is null ? string.Empty : " to a list"));
        }

        var item = string.IsNullOrEmpty(list.ItemType) ? null : compilation.GetTypeByMetadataName(list.ItemType!);
        if (item is null)
        {
            return (null, "its list's item template declares no item type (x:DataType)");
        }

        var key = itemKey is { Path: { } path } ? Walk(item, path) : null;
        if (itemKey is { } declared && key is null)
        {
            return (null, $"its item key '{declared.Binding}' is not a path of '{item.ToDisplayString()}'");
        }

        if (Mismatch(loader, item, key?.Type, itemKey?.Binding) is { } mismatch)
        {
            return (null, mismatch);
        }

        var members = key is { } found ? Keyed(item, found, compilation) : [];

        var code = $"new global::Aioros.Pages.ViewList({list.Ordinal}, static model__ => model__ is {Name(host!)} host__ ? host__{items.Value.Code} : null)"
            + (members.Count == 0 ? string.Empty : " { " + string.Join(", ", members) + " }");
        return (code, null);
    }

    private static List<string> Keyed(ITypeSymbol item, (ITypeSymbol Type, string Code) key, Compilation compilation)
    {
        var members = new List<string> { $"Key = static item__ => item__ is {Name(item)} typed__ ? (object?)typed__{key.Code} : null" };
        if (Parser(key.Type, compilation) is { } parse)
        {
            members.Add("Parse = static text__ => " + parse);
        }

        return members;
    }

    private static string? Unfollowable(XamlList list, ITypeSymbol? host) =>
        list.Nested ? "it is placed in a list nested inside another list's item template"
        : list.ContextChanged ? "an element around its list sets the DataContext, so its items cannot be followed from the host's view model"
        : host is null ? "its host has no view model (x:DataType) to read its list's items from"
        : null;

    private static string? Mismatch(PageLoader loader, ITypeSymbol item, ITypeSymbol? key, string? binding)
    {
        if (!loader.Batch || SymbolEqualityComparer.Default.Equals(loader.Key, key ?? item))
        {
            return null;
        }

        return key is null
            ? $"its batch loader takes keys of '{loader.Key!.ToDisplayString()}', but without a:View.ItemKey its items are its keys, of '{item.ToDisplayString()}'"
            : $"its batch loader takes keys of '{loader.Key!.ToDisplayString()}', but its item key '{binding}' is of '{key.ToDisplayString()}'";
    }

    /// <summary>The type a member path reaches from a type, and the code that reads it after an instance (<c>.Board?.Cards</c>).</summary>
    private static (ITypeSymbol Type, string Code)? Walk(ITypeSymbol type, string path)
    {
        var code = new System.Text.StringBuilder();
        var current = type;
        var first = true;
        foreach (var name in path.Split('.'))
        {
            var member = Members(current).FirstOrDefault(candidate => candidate.Name == name);
            var reached = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => Observable(current, name),
            };
            if (reached is null)
            {
                return null;
            }

            code.Append(first || current.IsValueType ? "." : "?.").Append(name);
            current = reached;
            first = false;
        }

        return (current, code.ToString());
    }

    private static IEnumerable<ISymbol> Members(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers().Where(member => !member.IsStatic && member.DeclaredAccessibility == Accessibility.Public && member is IPropertySymbol { Parameters.Length: 0 } or IFieldSymbol))
            {
                yield return member;
            }
        }
    }

    /// <summary>
    /// The type of a property CommunityToolkit.Mvvm generates from an <c>[ObservableProperty]</c> field (<c>_issues</c>
    /// gives <c>Issues</c>), which another generator's output hides from this one.
    /// </summary>
    private static ITypeSymbol? Observable(ITypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(candidate => !candidate.IsStatic && Generated(candidate.Name) == name
                && candidate.GetAttributes().Any(data => data.AttributeClass?.ToDisplayString() == "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute"));
            if (field is not null)
            {
                return field.Type;
            }
        }

        return null;
    }

    private static string Generated(string field)
    {
        var name = field.StartsWith("m_", System.StringComparison.Ordinal) ? field.Substring(2) : field.TrimStart('_');
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private static bool Enumerable(ITypeSymbol type, Compilation compilation) =>
        type.SpecialType != SpecialType.System_String
        && compilation.GetTypeByMetadataName("System.Collections.IEnumerable") is { } enumerable
        && (SymbolEqualityComparer.Default.Equals(type, enumerable) || type.AllInterfaces.Contains(enumerable, SymbolEqualityComparer.Default));

    /// <summary>The code that reads a key from the text naming it in its region: a string itself, an enum, or a type with <c>IParsable</c>.</summary>
    private static string? Parser(ITypeSymbol type, Compilation compilation)
    {
        var plain = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        if (plain.SpecialType == SpecialType.System_String)
        {
            return "text__";
        }

        if (plain.TypeKind == TypeKind.Enum)
        {
            return $"global::System.Enum.Parse<{Name(plain)}>(text__)";
        }

        var parsable = compilation.GetTypeByMetadataName("System.IParsable`1");
        return parsable is not null && plain.AllInterfaces.Any(face => SymbolEqualityComparer.Default.Equals(face.OriginalDefinition, parsable) && SymbolEqualityComparer.Default.Equals(face.TypeArguments[0], plain))
            ? $"{Name(plain)}.Parse(text__, global::System.Globalization.CultureInfo.InvariantCulture)"
            : null;
    }

    private static string Name(ITypeSymbol type) => type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
