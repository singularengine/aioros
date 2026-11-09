using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.ReactiveUI.Generators.Activation;

/// <summary>
/// What the adapter does with one of the app's classes, read from its shape: how its activator and its router are read,
/// whether it is routable, and how deep it derives, so that a derived class is matched before its base.
/// </summary>
internal sealed class ViewModelShape(string name, string? activator, string? router, bool routable, int depth)
{
    public const string Activator = "ReactiveUI.ViewModelActivator";
    public const string Activatable = "ReactiveUI.IActivatableViewModel";
    public const string RoutingState = "ReactiveUI.RoutingState";
    public const string Screen = "ReactiveUI.IScreen";
    public const string Routable = "ReactiveUI.IRoutableViewModel";

    private static readonly SymbolDisplayFormat Full = SymbolDisplayFormat.FullyQualifiedFormat;

    public string Name { get; } = name;

    public string? ActivatorAccess { get; } = activator;

    public string? RouterAccess { get; } = router;

    public bool IsRoutable { get; } = routable;

    public int Depth { get; } = depth;

    public static ViewModelShape? Read(INamedTypeSymbol type, Compilation compilation)
    {
        if (type.TypeKind != TypeKind.Class || type.IsGenericType || type.IsStatic || !Visible(type))
        {
            return null;
        }

        var activator = Access(type, "Activator", compilation.GetTypeByMetadataName(Activator), compilation.GetTypeByMetadataName(Activatable));
        var router = Access(type, "Router", compilation.GetTypeByMetadataName(RoutingState), compilation.GetTypeByMetadataName(Screen));
        var routable = compilation.GetTypeByMetadataName(Routable) is { } face && type.AllInterfaces.Contains(face, SymbolEqualityComparer.Default);
        return activator is null && router is null && !routable
            ? null
            : new ViewModelShape(type.ToDisplayString(Full), activator, router, routable, DepthOf(type));
    }

    private static string? Access(INamedTypeSymbol type, string member, INamedTypeSymbol? valueType, INamedTypeSymbol? face)
    {
        if (valueType is null)
        {
            return null;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetMembers(member).OfType<IPropertySymbol>()
                .FirstOrDefault(candidate => !candidate.IsStatic && candidate.GetMethod is not null && Derives(candidate.Type, valueType));
            if (property is not null && property.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)
            {
                return "." + member;
            }
        }

        return face is not null && type.AllInterfaces.Contains(face, SymbolEqualityComparer.Default)
            ? "(" + face.ToDisplayString(Full) + ")"
            : null;
    }

    private static bool Derives(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Visible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType || current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    private static int DepthOf(INamedTypeSymbol type)
    {
        var depth = 0;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }
}
