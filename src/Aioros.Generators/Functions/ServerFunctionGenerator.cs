using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aioros.Generators.Pages;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Functions;

[Generator]
public sealed class ServerFunctionGenerator : IIncrementalGenerator
{
    private const string Attribute = "Aioros.Functions.ServerFunctionsAttribute";

    private static readonly DiagnosticDescriptor NotATask = new(
        "AIOROS003",
        "A server function must return Task or Task<T>",
        "'{0}.{1}' must return Task or Task<T> to be a server function",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Unusable = new(
        "AIOROS009",
        "A server function registration cannot be used",
        "'{0}' cannot be server functions: {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Unsendable = new(
        "AIOROS015",
        "A server function's data cannot be sent",
        "'{0}' takes or returns '{1}', which cannot be sent between the installed app and the server: {2}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnwritableKey = new(
        "AIOROS019",
        "A data key cannot be written from the server function's arguments",
        "The key '{0}' of '{1}' cannot be written: {2}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MutableMemo = new(
        "AIOROS023",
        "A memoized or cached server function's result could be changed",
        "'[{0}]' on '{1}' shares its result with {2}, but {3}",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>Reports a <c>[Memo]</c> or <c>[Cached]</c> result a loader could change and a shared cached result that could depend on the user.</summary>
    private static void ReportShared(SourceProductionContext output, INamedTypeSymbol type, FunctionEmitter emitter, Location fallback)
    {
        foreach (var (method, location, problem) in emitter.MemoProblems())
        {
            output.ReportDiagnostic(Diagnostic.Create(MutableMemo, location ?? method.Locations.FirstOrDefault() ?? fallback, "Memo", type.ToDisplayString() + "." + method.Name, "every loader of a load that asks", problem));
        }

        foreach (var (method, cache) in emitter.Caches())
        {
            var at = cache.Location ?? method.Locations.FirstOrDefault() ?? fallback;
            if (cache.Mutable is { } mutable)
            {
                output.ReportDiagnostic(Diagnostic.Create(MutableMemo, at, "Cached", type.ToDisplayString() + "." + method.Name, "every load that asks until it is evicted", mutable));
            }

            if (cache.Request is { } request)
            {
                output.ReportDiagnostic(Diagnostic.Create(Pages.Diagnostics.CachedUserData, at, $"'[Cached(Shared = true)]' on '{type.ToDisplayString()}.{method.Name}' would give every user the same result, but its implementation takes '{request}', through which it can read the user; drop 'Shared' to keep its results per signed-in user"));
            }
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var marked = context.SyntaxProvider.ForAttributeWithMetadataName(
            Attribute,
            static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax,
            static (attributed, _) => FunctionDeclaration.Mark(attributed)).Collect();
        var registered = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => FunctionDeclaration.IsCandidate(node),
            static (syntax, cancellationToken) => FunctionDeclaration.Read(syntax, cancellationToken))
            .Where(static declaration => declaration is not null).Collect();
        context.RegisterSourceOutput(registered.Combine(marked).Combine(context.CompilationProvider).Combine(Pages.AppProjects.OnlyReferencesAnApp(context)), static (output, input) =>
            Generate(output, [.. input.Left.Left.Left.Select(declaration => declaration!), .. input.Left.Left.Right], input.Left.Right, input.Right));
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<FunctionDeclaration> declarations, Compilation compilation, bool onlyReferencesAnApp)
    {
        var runtime = !onlyReferencesAnApp && compilation.GetTypeByMetadataName("Aioros.Runtime.Functions.ServerFunctionGroup") is not null;
        var shapes = new DataShapes(compilation);
        var roots = new List<DataShape>();
        var groups = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var registrations = new List<string>();
        foreach (var service in declarations
            .Where(declaration => Report(output, declaration, declaration.Problem))
            .GroupBy(declaration => declaration.Service, SymbolEqualityComparer.Default)
            .OrderBy(service => service.Key!.ToDisplayString(), StringComparer.Ordinal))
        {
            var all = service.ToList();
            var type = (INamedTypeSymbol)service.Key!;
            if (!Usable(output, type, all) || Group(output, type, all) is not { } group)
            {
                continue;
            }

            if (groups.TryGetValue(group, out var owner))
            {
                Report(output, all[0], $"the group '{group}' is already used by '{owner.ToDisplayString()}'; give one of them another group");
                continue;
            }

            groups[group] = type;
            var methods = type.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary).ToList();
            var emitter = new FunctionEmitter(type, group, methods, [.. all.Select(declaration => declaration.Implementation!).Where(implementation => implementation is not null).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)], shapes);
            var problems = emitter.Problems().ToList();
            foreach (var (method, data, problem) in problems)
            {
                output.ReportDiagnostic(Diagnostic.Create(Unsendable, method.Locations.FirstOrDefault() ?? all[0].Location, type.ToDisplayString() + "." + method.Name, data.ToDisplayString(), problem));
            }

            var keys = emitter.KeyProblems().ToList();
            foreach (var (method, location, key, problem) in keys)
            {
                output.ReportDiagnostic(Diagnostic.Create(UnwritableKey, location ?? method.Locations.FirstOrDefault() ?? all[0].Location, key, type.ToDisplayString() + "." + method.Name, problem));
            }

            ReportShared(output, type, emitter, all[0].Location);

            if (problems.Count > 0 || keys.Count > 0)
            {
                continue;
            }

            output.AddSource(type.ToDisplayString() + ".Functions.g.cs", emitter.Source());
            roots.AddRange(emitter.Roots());
            registrations.Add(emitter.Registration);
        }

        if (registrations.Count > 0)
        {
            output.AddSource("AiorosFunctionData.g.cs", DataCodecs.EmitFunctions(roots));
        }

        if (runtime)
        {
            output.AddSource("AiorosFunctions.g.cs", Registry(registrations));
        }
    }

    private static bool Usable(SourceProductionContext output, INamedTypeSymbol type, List<FunctionDeclaration> declarations)
    {
        var generic = type.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(method => method.MethodKind == MethodKind.Ordinary && method.IsGenericMethod);
        var reason = type.TypeKind != TypeKind.Interface ? "it is not an interface; register the interface its implementation is called through"
            : type.IsGenericType ? "it is a generic interface, whose routes could not be named"
            : generic is not null ? $"its method '{generic.Name}' is generic, and a client cannot send a type argument"
            : null;
        if (reason is not null)
        {
            declarations.ForEach(declaration => Report(output, declaration, reason));
            return false;
        }

        var invalid = type.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary && !IsTask(method.ReturnType)).ToList();
        invalid.ForEach(method => output.ReportDiagnostic(Diagnostic.Create(NotATask, method.Locations.FirstOrDefault(), type.Name, method.Name)));
        return invalid.Count == 0;
    }

    private static string? Group(SourceProductionContext output, INamedTypeSymbol type, List<FunctionDeclaration> declarations)
    {
        var given = declarations.Where(declaration => !declaration.Marked && declaration.Group is not null).ToList();
        var conflict = given.FirstOrDefault(declaration => declaration.Group != given[0].Group);
        if (conflict is not null)
        {
            Report(output, conflict, $"it is registered with the groups '{given[0].Group}' and '{conflict.Group}'; keep one");
        }

        return given.FirstOrDefault()?.Group
            ?? declarations.FirstOrDefault(declaration => declaration.Marked)?.Group
            ?? DefaultGroup(type);
    }

    private static string DefaultGroup(INamedTypeSymbol type)
    {
        var name = type.Name.Length > 1 && type.Name[0] == 'I' && char.IsUpper(type.Name[1]) ? type.Name.Substring(1) : type.Name;
        name = name.Length > "Functions".Length && name.EndsWith("Functions", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Functions".Length) : name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static bool Report(SourceProductionContext output, FunctionDeclaration declaration, string? problem)
    {
        if (problem is not null)
        {
            output.ReportDiagnostic(Diagnostic.Create(Unusable, declaration.Location, declaration.Service.ToDisplayString(), problem));
        }

        return problem is null;
    }

    private static bool IsTask(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Task", ContainingNamespace: { Name: "Tasks" } } named && named.TypeArguments.Length <= 1;

    private static string Registry(IEnumerable<string> groups) =>
        "// <auto-generated/>\n#nullable enable\nnamespace Aioros.Generated;\n\n"
        + "/// <summary>Every interface the app registers as server functions.</summary>\n"
        + "public static class AiorosFunctions\n{\n"
        + "    public static global::System.Collections.Immutable.ImmutableArray<global::Aioros.Runtime.Functions.ServerFunctionGroup> All { get; } =\n"
        + "        [" + string.Join(", ", groups.OrderBy(name => name, StringComparer.Ordinal)) + "];\n}\n";
}
