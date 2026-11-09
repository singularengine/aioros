using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Aioros.Generators.Pages;

/// <summary>
/// Finds the commands a page's <c>Command</c> bindings name that run on any thread: their bodies, read from the app's
/// code with the lambdas they create (a command's lambda is its body), create or touch no Avalonia object, dispatcher or
/// platform service, and call no code of an assembly that references Avalonia unless its type carries
/// <c>[AnyThread]</c> (<c>Aioros.Markup</c>), as androidx's lint reads a library's <c>@AnyThread</c>. A path read through
/// such a library type needs the attribute too. What cannot be followed is not listed, so the engine runs it.
/// </summary>
internal sealed class AnywhereCommands(Compilation compilation)
{
    private const string AnyThread = "Aioros.Markup.AnyThreadAttribute";

    private static readonly string[] PlatformNamespaces = ["Avalonia.Media.Imaging", "Avalonia.Platform", "Avalonia.Threading"];

    private readonly Dictionary<ISymbol, bool> _known = new(SymbolEqualityComparer.Default);

    /// <summary>The planned paths, in order, of the commands that run on any thread among the <c>Command</c> bindings of a route's page, its layouts and the views placed in them.</summary>
    public List<string> Of(Aioros.Routing.Files.FileRoute route, IReadOnlyList<PlacedView> views, IReadOnlyDictionary<string, (string Path, string Text)> sources)
    {
        var files = route.Layouts.Append(route.Source).Concat(views.Select(view => view.Source)).Where(sources.ContainsKey).Select(source => XamlPage.Parse(sources[source].Text)).ToList();
        return [.. files.SelectMany(file => file.CommandBindings()).Select(binding => Runs(binding.DataType, binding.Path))
            .Concat(files.SelectMany(file => file.PageCommandBindings()).Select(binding => Runs(binding.DataType, binding.Path) is { } path ? "^" + binding.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + path : null))
            .OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)];
    }

    /// <summary>The path a command binding is planned with (a method's ends with <c>()</c>) when its command runs on any thread.</summary>
    public string? Runs(string dataType, string path)
    {
        if (compilation.GetTypeByMetadataName(dataType) is not ITypeSymbol type)
        {
            return null;
        }

        var parts = path.Split('.');
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (Property(type, parts[index]) is not { } property || !Reads(property))
            {
                return null;
            }

            type = property.Type;
        }

        var last = parts[parts.Length - 1];
        if (Property(type, last) is { } command)
        {
            return Executes(command) ? path : null;
        }

        return Members(type).OfType<IMethodSymbol>().FirstOrDefault(method => method.Name == last && !method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length <= 1) is { } target
            && Calls(target)
            ? path + "()"
            : Generated(type, last) is { } generated && Calls(generated) ? path : null;
    }

    /// <summary>The method a toolkit's generator turns into this command property: CommunityToolkit's <c>[RelayCommand]</c> and ReactiveUI's <c>[ReactiveCommand]</c>, <c>Save</c> or <c>SaveAsync</c> for <c>SaveCommand</c>.</summary>
    private static IMethodSymbol? Generated(ITypeSymbol type, string name) =>
        name.EndsWith("Command", StringComparison.Ordinal) && name.Substring(0, name.Length - "Command".Length) is var method
            ? Members(type).OfType<IMethodSymbol>().FirstOrDefault(candidate => (candidate.Name == method || candidate.Name == method + "Async")
                && candidate.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is "CommunityToolkit.Mvvm.Input.RelayCommandAttribute" or "ReactiveUI.SourceGenerators.ReactiveCommandAttribute"))
            : null;

    private static IPropertySymbol? Property(ITypeSymbol type, string name) =>
        Members(type).OfType<IPropertySymbol>().FirstOrDefault(property => property.Name == name && !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public);

    private static IEnumerable<ISymbol> Members(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                yield return member;
            }
        }
    }

    /// <summary>Whether reading a property on the way to a command touches nothing of Avalonia: the app's getter is read, another assembly's type is judged as a whole.</summary>
    private bool Reads(IPropertySymbol property) =>
        InApp(property) ? property.GetMethod is not { } getter || Calls(getter) : Free(property.ContainingType);

    /// <summary>Whether what a command property runs touches nothing of Avalonia: its initializer, its expression body and what the type's constructors assign to it.</summary>
    private bool Executes(IPropertySymbol property)
    {
        if (!InApp(property))
        {
            return Free(property.ContainingType);
        }

        var bodies = property.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<PropertyDeclarationSyntax>()
            .Select(declaration => (SyntaxNode?)declaration.Initializer?.Value ?? declaration.ExpressionBody?.Expression)
            .OfType<SyntaxNode>()
            .Concat(Assigned(property))
            .ToList();
        return bodies.Count > 0 && !bodies.Any(Touches);
    }

    private IEnumerable<SyntaxNode> Assigned(IPropertySymbol property) =>
        property.ContainingType.Constructors.SelectMany(constructor => constructor.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()))
            .SelectMany(constructor => constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            .Where(assignment => compilation.GetSemanticModel(assignment.SyntaxTree).GetSymbolInfo(assignment.Left).Symbol is { } target && SymbolEqualityComparer.Default.Equals(target, property))
            .Select(assignment => (SyntaxNode)assignment.Right);

    private bool Calls(IMethodSymbol method) =>
        InApp(method)
            ? Accessor(method) || Known(method, () => method.IsAbstract
                ? Implementations(method) is { Count: > 0 } implementations && implementations.All(Calls)
                : Declared(method) ? !method.DeclaringSyntaxReferences.Any(reference => Touches(reference.GetSyntax())) : Initializes(method.ContainingType))
            : Free(method.ContainingType);

    /// <summary>Whether a method has a declaration of its own to read; a primary, record or implicit constructor has its type's instead.</summary>
    private static bool Declared(IMethodSymbol method) =>
        method.MethodKind != MethodKind.Constructor || method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is ConstructorDeclarationSyntax);

    /// <summary>Whether a type's field and property initializers, which a constructor without a body of its own runs, touch nothing of Avalonia.</summary>
    private bool Initializes(INamedTypeSymbol type) =>
        !type.GetMembers().Any(member => member switch
        {
            IFieldSymbol field => field.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } value } && Touches(value)),
            IPropertySymbol property => property.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is PropertyDeclarationSyntax { Initializer.Value: { } value } && Touches(value)),
            _ => false,
        });

    private List<INamedTypeSymbol>? _types;

    /// <summary>The app's implementations of an interface member or overrides of an abstract one, which a call through it may run.</summary>
    private List<IMethodSymbol> Implementations(IMethodSymbol method) =>
        [.. (_types ??= [.. Types(compilation.Assembly.GlobalNamespace)])
            .Select(type => method.ContainingType.TypeKind == TypeKind.Interface
                ? type.FindImplementationForInterfaceMember(method) as IMethodSymbol
                : type.GetMembers(method.Name).OfType<IMethodSymbol>().FirstOrDefault(candidate => Overrides(candidate, method)))
            .OfType<IMethodSymbol>()
            .Where(implementation => !implementation.IsAbstract)
            .Distinct<IMethodSymbol>(SymbolEqualityComparer.Default)];

    private static bool Overrides(IMethodSymbol candidate, IMethodSymbol method)
    {
        for (var current = candidate.OverriddenMethod; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, method.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space) =>
        space.GetTypeMembers().SelectMany(Nested).Concat(space.GetNamespaceMembers().SelectMany(Types));

    private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type) => new[] { type }.Concat(type.GetTypeMembers().SelectMany(Nested));

    /// <summary>
    /// An accessor without a body of its own (an auto property's, a record's positional property's, or a partial
    /// property's that another generator implements): it reads or writes its field and raises its change.
    /// </summary>
    private static bool Accessor(IMethodSymbol method) =>
        method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet
        && !method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is AccessorDeclarationSyntax { Body: not null } or AccessorDeclarationSyntax { ExpressionBody: not null } or ArrowExpressionClauseSyntax);

    private bool Known(ISymbol symbol, Func<bool> find)
    {
        if (_known.TryGetValue(symbol, out var known))
        {
            return known;
        }

        _known[symbol] = true;
        return _known[symbol] = find();
    }

    private bool Touches(SyntaxNode node) =>
        compilation.GetSemanticModel(node.SyntaxTree).GetOperation(node) is not { } operation || Touches(operation);

    private bool Touches(IOperation operation)
    {
        var found = operation switch
        {
            IObjectCreationOperation creation => !Free(creation.Type) || (creation.Constructor is { } constructor && !Calls(constructor)),
            IInvocationOperation invocation => !Calls(invocation.TargetMethod) || !Free(invocation.TargetMethod.ReturnType),
            IPropertyReferenceOperation property => property.Property.GetMethod is { } getter ? !Calls(getter) : !Free(property.Property.ContainingType),
            IFieldReferenceOperation field => !InApp(field.Field) && !Free(field.Field.ContainingType),
            IEventReferenceOperation reference => !InApp(reference.Event) && !Free(reference.Event.ContainingType),
            IMethodReferenceOperation reference => !Calls(reference.Method),
            IAwaitOperation awaited => Waits(awaited.Operation),
            _ => false,
        };
        return found || operation.ChildOperations.Any(Touches);
    }

    /// <summary>
    /// Whether an awaited task is one the command does not start, so the command waits on another post (a
    /// <c>TaskCompletionSource</c>'s task, a task held in a field or property): only the engine answers a post while its
    /// command waits on the visitor ("Posts that wait on the user"); a task a call returns is the command's own work.
    /// </summary>
    private static bool Waits(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;
                case IInvocationOperation { TargetMethod.Name: "ConfigureAwait", Instance: { } instance }:
                    operation = instance;
                    continue;
                case IFieldReferenceOperation or IPropertyReferenceOperation:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Whether a type of another assembly cannot reach Avalonia's thread: one of Avalonia's values (not an
    /// <c>AvaloniaObject</c>, outside its platform namespaces), a type of an assembly that references no Avalonia, or one
    /// marked <c>[AnyThread]</c>, it or a type holding it; the app's own types are judged by their code instead.
    /// </summary>
    private bool Free(ITypeSymbol? type)
    {
        if (type is null || type is ITypeParameterSymbol || InApp(type))
        {
            return true;
        }

        if (type is IArrayTypeSymbol array)
        {
            return Free(array.ElementType);
        }

        if (type is INamedTypeSymbol { IsGenericType: true } generic && !generic.TypeArguments.All(Free))
        {
            return false;
        }

        if (IsAvaloniaAssembly(type.ContainingAssembly))
        {
            return !IsAvaloniaObject(type) && !InPlatform(type);
        }

        return !ReferencesAvalonia(type.ContainingAssembly) || Marked(type);
    }

    private static bool Marked(ITypeSymbol type)
    {
        for (var current = (ITypeSymbol?)type.OriginalDefinition; current is not null; current = current.ContainingType)
        {
            if (current.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == AnyThread))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAvaloniaAssembly(IAssemblySymbol? assembly) =>
        assembly?.Name is { } name && (name == "Avalonia" || name.StartsWith("Avalonia.", StringComparison.Ordinal));

    private static bool ReferencesAvalonia(IAssemblySymbol? assembly) =>
        assembly is not null && assembly.Modules.Any(module => module.ReferencedAssemblySymbols.Any(IsAvaloniaAssembly));

    private bool InApp(ISymbol symbol) => SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);

    private static bool IsAvaloniaObject(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "Avalonia.AvaloniaObject")
            {
                return true;
            }
        }

        return false;
    }

    private static bool InPlatform(ITypeSymbol type) =>
        type.ContainingNamespace?.ToDisplayString() is { } name && PlatformNamespaces.Any(platform => name == platform || name.StartsWith(platform + ".", StringComparison.Ordinal));
}
