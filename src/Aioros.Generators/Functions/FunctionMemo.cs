using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Functions;

/// <summary>
/// Whether a server function's implementation marks it <c>[Memo]</c>, and why its result could be changed by a loader it
/// is shared with: no result, an array, a mutable collection, or a type with a public setter or writable field.
/// </summary>
internal sealed class FunctionMemo
{
    private const string MemoName = "Aioros.Functions.MemoAttribute";

    private static readonly string[] ReadOnlySpaces = ["System.Collections.Immutable", "System.Collections.Frozen", "System.Collections.ObjectModel"];

    private FunctionMemo(bool marked, Location? location, string? problem)
    {
        Marked = marked;
        Location = location;
        Problem = problem;
    }

    public bool Marked { get; }

    public Location? Location { get; }

    public string? Problem { get; }

    public static FunctionMemo Of(IMethodSymbol method, ITypeSymbol? result, IEnumerable<INamedTypeSymbol> implementations)
    {
        var attribute = implementations
            .Select(implementation => implementation.FindImplementationForInterfaceMember(method) as IMethodSymbol)
            .Where(member => member is not null)
            .SelectMany(member => member!.GetAttributes())
            .FirstOrDefault(data => data.AttributeClass?.ToDisplayString() == MemoName);
        return attribute is null
            ? new FunctionMemo(false, null, null)
            : new FunctionMemo(true, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(), Mutable(result));
    }

    public static string? Mutable(ITypeSymbol? result)
    {
        if (result is null)
        {
            return "it returns no result";
        }

        var name = "its result '" + result.ToDisplayString() + "'";
        if (result is IArrayTypeSymbol)
        {
            return name + " is an array, which a loader could change";
        }

        if (result.SpecialType != SpecialType.None || result.TypeKind == TypeKind.Enum || ReadOnlySpaces.Contains(result.ContainingNamespace?.ToDisplayString()))
        {
            return null;
        }

        if (new[] { result }.Concat(result.AllInterfaces).Any(face => face.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>" or "System.Collections.IList" or "System.Collections.IDictionary"))
        {
            return name + " is a mutable collection, which a loader could change";
        }

        return Writable(result) is { } member ? name + " has the public " + member + ", which a loader could change" : null;
    }

    private static string? Writable(ITypeSymbol result)
    {
        for (var type = result; type is not null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            var member = type.GetMembers().Where(member => !member.IsStatic && member.DeclaredAccessibility == Accessibility.Public).Select(member => member switch
            {
                IPropertySymbol { SetMethod: { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false } } => "setter '" + member.Name + "'",
                IFieldSymbol { IsReadOnly: false, IsConst: false, IsImplicitlyDeclared: false } => "writable field '" + member.Name + "'",
                _ => null,
            }).FirstOrDefault(found => found is not null);
            if (member is not null)
            {
                return member;
            }
        }

        return null;
    }
}
