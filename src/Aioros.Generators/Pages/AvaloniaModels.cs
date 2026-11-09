using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Aioros.Generators.Pages;

/// <summary>
/// Finds the models and loaders that create an Avalonia object as they are constructed: an <c>AvaloniaObject</c> takes
/// the dispatcher of the thread that creates it (the first one Avalonia's UI dispatcher), and images, assets and
/// dispatchers need Avalonia's platform, so a page whose models create one has them created on Avalonia's thread. What a
/// construction runs is read from the app's code: field and property initializers, constructors, and the app's methods,
/// accessors and types they reach; a lambda's body runs later, when it is called, and is not followed.
/// </summary>
internal sealed class AvaloniaModels(Compilation compilation)
{
    private static readonly string[] PlatformNamespaces = ["Avalonia.Media.Imaging", "Avalonia.Platform", "Avalonia.Threading"];

    private readonly Dictionary<ISymbol, bool> _known = new(SymbolEqualityComparer.Default);

    public bool Use(INamedTypeSymbol? type) => type is not null && Constructs(type);

    public bool Loads(PageLoader? loader) =>
        loader is not null && (Constructs(loader.Type) || loader.Type.GetMembers().OfType<IMethodSymbol>().Any(method => method.Name.StartsWith("Load", StringComparison.Ordinal) && Runs(method)));

    private bool Constructs(INamedTypeSymbol type) =>
        Known(type, () => type.GetMembers().Any(member => member switch
        {
            IFieldSymbol field => field.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } value } && Touches(value)),
            IPropertySymbol property => property.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is PropertyDeclarationSyntax { Initializer.Value: { } value } && Touches(value)),
            IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor => Runs(constructor),
            _ => false,
        }));

    private bool Runs(IMethodSymbol method) =>
        Known(method, () => method.DeclaringSyntaxReferences.Any(reference => Touches(reference.GetSyntax())));

    private bool Known(ISymbol symbol, Func<bool> find)
    {
        if (_known.TryGetValue(symbol, out var known))
        {
            return known;
        }

        _known[symbol] = false;
        return _known[symbol] = find();
    }

    private bool Touches(SyntaxNode node)
    {
        var model = compilation.GetSemanticModel(node.SyntaxTree);
        return model.GetOperation(node) is { } operation && Touches(operation);
    }

    private bool Touches(IOperation operation)
    {
        if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
        {
            return false;
        }

        var found = operation switch
        {
            IObjectCreationOperation creation => IsAvalonia(creation.Type) || Reaches(creation.Constructor),
            IInvocationOperation invocation => IsAvalonia(invocation.TargetMethod.ContainingType) || IsAvalonia(invocation.TargetMethod.ReturnType) || Reaches(invocation.TargetMethod),
            IPropertyReferenceOperation property => InPlatform(property.Property.ContainingType) || Reaches(property.Property.GetMethod),
            IFieldReferenceOperation field => Reaches(field.Field.ContainingType),
            _ => false,
        };
        return found || operation.ChildOperations.Any(Touches);
    }

    private bool Reaches(IMethodSymbol? method) =>
        method is not null && InApp(method) && (Runs(method) || Constructs(method.ContainingType));

    private bool Reaches(INamedTypeSymbol? type) => type is not null && InApp(type) && Constructs(type);

    private bool InApp(ISymbol symbol) => SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);

    private static bool IsAvalonia(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "Avalonia.AvaloniaObject")
            {
                return true;
            }
        }

        return InPlatform(type);
    }

    private static bool InPlatform(ITypeSymbol? type) =>
        type?.ContainingNamespace?.ToDisplayString() is { } name && PlatformNamespaces.Any(platform => name == platform || name.StartsWith(platform + ".", StringComparison.Ordinal));
}
