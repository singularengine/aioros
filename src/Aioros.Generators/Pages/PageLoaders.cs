using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

internal sealed class PageLoader(INamedTypeSymbol type, ITypeSymbol? data, ITypeSymbol? key = null, int? seconds = null, LoaderCall? call = null)
{
    /// <summary>The loader's typed <c>Load</c> or <c>LoadAsync</c> and its binding, when it implements no loader interface.</summary>
    public LoaderCall? Call { get; } = call;

    /// <summary>The lifetime its <c>[Cached(seconds)]</c> declares, when it does and does not read the user.</summary>
    public int? Seconds { get; } = seconds;

    public INamedTypeSymbol Type { get; } = type;

    public ITypeSymbol? Data { get; } = data;

    /// <summary>The key type of a batch loader (<c>IBatchLoader&lt;TKey, TData&gt;</c>), which loads every item of a list at once.</summary>
    public ITypeSymbol? Key { get; } = key;

    public bool Batch => Key is not null;
}

internal static class PageLoaders
{
    private const string Suffix = ".Loader.cs";

    private static readonly HashSet<string> Deferred = new(StringComparer.Ordinal) { "System.Linq.IQueryable", "System.Linq.IQueryable`1", "System.Collections.Generic.IAsyncEnumerable`1" };

    public static Dictionary<string, PageLoader> Find(SourceProductionContext output, Compilation compilation, DataShapes shapes, Func<string, string> relative, ICollection<string> files)
    {
        var found = new Dictionary<string, PageLoader>(StringComparer.Ordinal);
        if (compilation.GetTypeByMetadataName("Aioros.Pages.ILoader`1") is not { } contract)
        {
            return found;
        }

        foreach (var (type, declared, key, location, methods) in Candidates(compilation, contract))
        {
            var path = relative(location!.SourceTree!.FilePath);
            var file = path.EndsWith(Suffix, StringComparison.Ordinal) ? path.Substring(0, path.Length - Suffix.Length) + ".axaml" : null;
            var problem = file is null || !files.Contains(file)
                ? $"its file '{path}' is not named after a page, layout or view beside it ('Page.Loader.cs' next to 'Page.axaml', 'Layout.Loader.cs' next to 'Layout.axaml', 'Recent.Loader.cs' next to a view 'Recent.axaml' with an x:DataType)"
                : found.TryGetValue(file, out var first) ? $"'{file}' already has the loader '{first.Type.ToDisplayString()}'" : null;
            if (problem is not null)
            {
                output.ReportDiagnostic(Diagnostic.Create(Diagnostics.MisplacedLoader, location, type.ToDisplayString(), problem));
            }
            else if (Called(output, type, methods, file!, location) is var (bound, call) && bound)
            {
                var data = call?.Data ?? declared;
                found[file!] = new PageLoader(type, data, key, Lifetime(output, compilation, type, location, call), call);
                CheckData(output, shapes, type, data, location);
            }
        }

        return found;
    }

    /// <summary>Binds a loader found by convention, reporting <c>AIOROS025</c> when it cannot be called; an interface loader needs no binding.</summary>
    private static (bool Bound, LoaderCall? Call) Called(SourceProductionContext output, INamedTypeSymbol type, IMethodSymbol[]? methods, string file, Location? location)
    {
        if (methods is null)
        {
            return (true, null);
        }

        var (call, problem, at) = LoaderCalls.Bind(type, methods, file);
        if (problem is not null)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnboundLoader, at ?? location, type.ToDisplayString(), problem));
        }

        return (call is not null, call);
    }

    private static void CheckData(SourceProductionContext output, DataShapes shapes, INamedTypeSymbol type, ITypeSymbol? data, Location? location)
    {
        if (data is not null && Lazy(data) is { } lazy)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.DeferredData, location, type.ToDisplayString(), data.ToDisplayString(), lazy));
        }
        else if (data is not null && shapes.Problem(data) is { } unreadable)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnreadableData, location, type.ToDisplayString(), data.ToDisplayString(), unreadable));
        }
    }

    /// <summary>
    /// The project's loaders: classes implementing a loader interface, and classes in a <c>.Loader.cs</c> file with a public
    /// <c>Load</c> or <c>LoadAsync</c> (their methods given), whose data the binding reads from the method's result.
    /// </summary>
    private static IEnumerable<(INamedTypeSymbol Type, ITypeSymbol? Data, ITypeSymbol? Key, Location? Location, IMethodSymbol[]? Methods)> Candidates(Compilation compilation, INamedTypeSymbol contract)
    {
        var dataless = compilation.GetTypeByMetadataName("Aioros.Pages.ILoader");
        var batch = compilation.GetTypeByMetadataName("Aioros.Pages.IBatchLoader`2");
        return Types(compilation.Assembly.GlobalNamespace)
            .Where(type => type.TypeKind == TypeKind.Class && (type.IsStatic || !type.IsAbstract) && !type.IsGenericType)
            .Select(type => (Type: type, Many: type.AllInterfaces.FirstOrDefault(face => batch is not null && SymbolEqualityComparer.Default.Equals(face.OriginalDefinition, batch))))
            .Select(candidate => (candidate.Type, Data: candidate.Many?.TypeArguments[1] ?? candidate.Type.AllInterfaces.FirstOrDefault(face => SymbolEqualityComparer.Default.Equals(face.OriginalDefinition, contract))?.TypeArguments[0], Key: candidate.Many?.TypeArguments[0], Location: candidate.Type.Locations.FirstOrDefault(location => location.IsInSource)))
            .Where(candidate => candidate.Location is not null)
            .Select(candidate => (candidate.Type, candidate.Data, candidate.Key, candidate.Location, Interface: candidate.Data is not null || candidate.Type.AllInterfaces.Any(face => SymbolEqualityComparer.Default.Equals(face, dataless))))
            .Select(candidate => (candidate.Type, candidate.Data, candidate.Key, candidate.Location, Methods: candidate.Interface ? null : Convention(candidate.Type, candidate.Location!), candidate.Interface))
            .Where(candidate => candidate.Interface || candidate.Methods is not null)
            .Select(candidate => (candidate.Type, candidate.Data, candidate.Key, candidate.Location, candidate.Methods))
            .OrderBy(candidate => candidate.Location!.SourceTree!.FilePath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Location!.SourceSpan.Start);
    }

    private static IMethodSymbol[]? Convention(INamedTypeSymbol type, Location location) =>
        location.SourceTree!.FilePath.EndsWith(Suffix, StringComparison.Ordinal) && LoaderCalls.Methods(type) is { Length: > 0 } methods ? methods : null;

    /// <summary>
    /// The seconds a loader's <c>[Cached]</c> declares; none without it, and none, with <c>AIOROS024</c>, for a loader that
    /// reads <c>PageRequest.User</c> or takes the request in its constructor, since its page would depend on the user.
    /// </summary>
    private static int? Lifetime(SourceProductionContext output, Compilation compilation, INamedTypeSymbol type, Location? location, LoaderCall? call)
    {
        var cached = type.GetAttributes().FirstOrDefault(data => data.AttributeClass?.ToDisplayString() == "Aioros.Functions.CachedAttribute");
        if (cached?.ConstructorArguments.FirstOrDefault().Value is not int seconds)
        {
            return null;
        }

        var reads = Functions.RequestReads.Taken(type) is { } taken ? "takes '" + taken + "'"
            : call is { TakesUser: true } ? "takes the user ('" + LoaderCalls.User + "')"
            : Reads(compilation, type, output.CancellationToken) is { } read ? "reads 'PageRequest." + read + "'" : null;
        if (reads is null)
        {
            return seconds;
        }

        output.ReportDiagnostic(Diagnostic.Create(Diagnostics.CachedUserData, cached.ApplicationSyntaxReference?.GetSyntax(output.CancellationToken).GetLocation() ?? location, $"The loader '{type.ToDisplayString()}' is marked '[Cached]' but {reads}, so its data depends on the user and its page is never cached; remove '[Cached]'"));
        return null;
    }

    /// <summary>What of the visitor's a loader reads from its request (<c>User</c> or <c>Cookies</c>), or nothing.</summary>
    private static string? Reads(Compilation compilation, INamedTypeSymbol type, System.Threading.CancellationToken cancellationToken) =>
        type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(cancellationToken)).SelectMany(syntax =>
        {
            var model = compilation.GetSemanticModel(syntax.SyntaxTree);
            return syntax.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>()
                .Where(name => name.Identifier.ValueText is "User" or "Cookies")
                .Where(name => model.GetSymbolInfo(name, cancellationToken).Symbol is IPropertySymbol { ContainingType: { } owner } && owner.ToDisplayString() == "Aioros.Pages.PageRequest")
                .Select(name => name.Identifier.ValueText);
        }).FirstOrDefault();

    public static HashSet<ITypeSymbol> DataTypes(IEnumerable<PageLoader> loaders) =>
        new(loaders.Where(loader => loader.Data is not null).Select(loader => loader.Data!), SymbolEqualityComparer.Default);

    /// <summary>Checks each page, layout and view against its loader and the data the project's loaders return.</summary>
    public static void Check(SourceProductionContext output, Compilation compilation, IEnumerable<(Location At, string File, INamedTypeSymbol? Model, PageLoader? Loader)> files, IEnumerable<PageLoader> loaders)
    {
        var data = DataTypes(loaders);
        var commands = new NavigatingCommands(compilation, output.CancellationToken);
        foreach (var (at, file, model, loader) in files)
        {
            Check(output, at, file, model, loader, data, commands);
        }
    }

    private static void Check(SourceProductionContext output, Location at, string file, INamedTypeSymbol? model, PageLoader? loader, HashSet<ITypeSymbol> data, NavigatingCommands commands)
    {
        var parameters = Constructor(model)?.Parameters ?? [];
        foreach (var parameter in parameters.Where(parameter => data.Contains(parameter.Type) && !SymbolEqualityComparer.Default.Equals(parameter.Type, loader?.Data)))
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.DataWithoutLoader, at, file, model!.ToDisplayString(), parameter.Type.ToDisplayString()));
        }

        if (loader?.Data is not { } loaded)
        {
            return;
        }

        if (!parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, loaded)))
        {
            var reason = model is null ? "the file declares no view model (x:DataType) to take" : $"the view model '{model.ToDisplayString()}' does not take in its public constructor";
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.LoaderDataNotTaken, at, file, loader.Type.ToDisplayString(), loaded.ToDisplayString(), reason));
            return;
        }

        var (receive, problem) = Receiver(model, loaded);
        if (problem is not null)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.AmbiguousReceive, at, file, model!.ToDisplayString(), loaded.ToDisplayString(), problem));
        }
        else if (receive is null && Members(model!).Any(IsCommand) && Members(model!).Any(IsState) && !commands.AllNavigate(Members(model!).Where(IsCommand)))
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.LostAfterCommand, model!.Locations.FirstOrDefault(location => location.IsInSource) ?? at, file, model.ToDisplayString(), loaded.ToDisplayString()));
        }
    }

    private static IEnumerable<ISymbol> Members(INamedTypeSymbol model) => Hierarchy(model).SelectMany(type => type.GetMembers());

    private static bool IsCommand(ISymbol member) => member switch
    {
        IPropertySymbol property => property.DeclaredAccessibility == Accessibility.Public && IsCommandType(property.Type),
        IMethodSymbol method => Marked(method, "CommunityToolkit.Mvvm.Input.RelayCommandAttribute"),
        _ => false,
    };

    private static bool IsState(ISymbol member) =>
        member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, SetMethod.DeclaredAccessibility: Accessibility.Public, IsStatic: false }
        || Marked(member, "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute");

    private static bool Marked(ISymbol member, string attribute) => member.GetAttributes().Any(data => data.AttributeClass?.ToDisplayString() == attribute);

    private static bool IsCommandType(ITypeSymbol type) =>
        new[] { type }.Concat(type.AllInterfaces).Any(candidate => candidate.Name == "ICommand" && candidate.ContainingNamespace?.ToDisplayString() == "System.Windows.Input");

    public static (IMethodSymbol? Method, string? Problem) Receiver(INamedTypeSymbol? model, ITypeSymbol data)
    {
        if (model is null)
        {
            return (null, null);
        }

        var candidates = Hierarchy(model)
            .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
            .Where(method => !method.IsOverride && ReceiveName(method) && method.Parameters.Length > 0 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, data))
            .Where(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, model) || method.DeclaredAccessibility == Accessibility.Public || method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
            .ToList();
        if (candidates.Select(Unfit).FirstOrDefault(reason => reason is not null) is { } unfit)
        {
            return (null, unfit);
        }

        return candidates.Count switch
        {
            0 => (null, null),
            1 => (candidates[0], null),
            _ => (null, "it has two methods that take it (" + string.Join(" and ", candidates.Select(method => "'" + method.ToDisplayString() + "'")) + "); keep one"),
        };
    }

    private static bool ReceiveName(IMethodSymbol method) =>
        method.MethodKind == MethodKind.ExplicitInterfaceImplementation
            ? method.ExplicitInterfaceImplementations.Any(implemented => implemented.Name == "Receive")
            : method.MethodKind == MethodKind.Ordinary && method.Name == "Receive";

    private static string? Unfit(IMethodSymbol method)
    {
        var reason = method.MethodKind == MethodKind.ExplicitInterfaceImplementation ? null
            : method.DeclaredAccessibility != Accessibility.Public ? "is not public"
            : method.IsStatic ? "is static"
            : null;
        reason ??= !method.ReturnsVoid ? "returns '" + method.ReturnType.ToDisplayString() + "' instead of void"
            : method.Parameters.Length != 1 ? "takes more parameters than the data"
            : method.IsGenericMethod ? "is generic"
            : null;
        return reason is null ? null : "its method '" + method.ToDisplayString() + "' " + reason + "; a view model takes new data through a public 'void Receive(" + method.Parameters[0].Type.ToDisplayString() + ")'";
    }

    private static IEnumerable<INamedTypeSymbol> Hierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static string? Lazy(ITypeSymbol data)
    {
        if (IsDeferred(data))
        {
            return string.Empty;
        }

        var member = data.GetMembers()
            .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public && !candidate.IsStatic)
            .Select(candidate => candidate switch
            {
                IPropertySymbol property => (Kind: "property", candidate.Name, Type: property.Type),
                IFieldSymbol field => (Kind: "field", candidate.Name, Type: field.Type),
                _ => (Kind: string.Empty, candidate.Name, Type: (ITypeSymbol?)null),
            })
            .FirstOrDefault(candidate => candidate.Type is not null && IsDeferred(candidate.Type));
        return member.Type is null ? null : $", whose {member.Kind} '{member.Name}' is '{member.Type.ToDisplayString()}'";
    }

    private static bool IsDeferred(ITypeSymbol type) =>
        new[] { type }.Concat(type.AllInterfaces).Any(candidate => Deferred.Contains(candidate.OriginalDefinition.ContainingNamespace?.ToDisplayString() + "." + candidate.OriginalDefinition.MetadataName));

    public static IMethodSymbol? Constructor(INamedTypeSymbol? type) =>
        type is null || type.IsAbstract
            ? null
            : type.InstanceConstructors
                .Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(candidate => candidate.Parameters.Length)
                .FirstOrDefault();

    public static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space) =>
        space.GetTypeMembers().SelectMany(Nested).Concat(space.GetNamespaceMembers().SelectMany(Types));

    private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type) => new[] { type }.Concat(type.GetTypeMembers().SelectMany(Nested));
}
