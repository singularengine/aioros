using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Navigation;

[Generator]
public sealed class NavigationGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Unusable = new(
        "AIOROS016",
        "A navigation interface cannot be implemented",
        "'{0}' cannot be a navigation interface: {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat Full = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registered = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => NavigationDeclaration.IsCandidate(node),
            static (syntax, cancellationToken) => NavigationDeclaration.Read(syntax, cancellationToken))
            .Where(static declaration => declaration is not null).Collect();
        var runtime = context.CompilationProvider
            .Select(static (compilation, _) => compilation.GetTypeByMetadataName("Aioros.Runtime.Pages.NavigationInterface") is not null)
            .Combine(Pages.AppProjects.OnlyReferencesAnApp(context))
            .Select(static (input, _) => input.Left && !input.Right);
        context.RegisterSourceOutput(registered.Combine(runtime), static (output, input) => Generate(output, input.Left!, input.Right));
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<NavigationDeclaration?> declarations, bool runtime)
    {
        var entries = new List<string>();
        foreach (var registration in declarations
            .Select(declaration => declaration!)
            .GroupBy(declaration => declaration.Type, SymbolEqualityComparer.Default)
            .OrderBy(registration => registration.Key!.ToDisplayString(), StringComparer.Ordinal))
        {
            var type = (ITypeSymbol)registration.Key!;
            var problems = Problems(type).ToList();
            foreach (var problem in problems)
            {
                output.ReportDiagnostic(Diagnostic.Create(Unusable, registration.First().Location, type.ToDisplayString(), problem));
            }

            if (problems.Count > 0)
            {
                continue;
            }

            var name = ImplementationName(type);
            output.AddSource(type.ToDisplayString() + ".Navigation.g.cs", Implementation(type, name));
            entries.Add("new(typeof(" + type.ToDisplayString(Full) + "), static navigator => new " + Namespace(type) + name + "(navigator))");
        }

        if (runtime)
        {
            output.AddSource("AiorosNavigators.g.cs", Registry(entries));
        }
    }

    private static IEnumerable<string> Problems(ITypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface || type is not INamedTypeSymbol named)
        {
            yield return "it is not an interface; declare an interface whose methods take the path to navigate to";
            yield break;
        }

        if (named.IsGenericType)
        {
            yield return "it is a generic interface, whose implementation could not be named";
            yield break;
        }

        if (!Visible(named))
        {
            yield return "the generated implementation cannot see it; make it public or internal";
            yield break;
        }

        foreach (var member in Members(named))
        {
            if (member is not IMethodSymbol method)
            {
                yield return $"its member '{member.Name}' is not a method; a navigation interface has only methods that navigate to a path";
            }
            else if (method.IsGenericMethod)
            {
                yield return $"its method '{method.Name}' is generic, and a navigation method takes no type arguments";
            }
            else if (!TakesPath(method))
            {
                yield return $"its method '{method.Name}' must take the path as a string, optionally followed by a CancellationToken";
            }
            else if (Returns(method) is null)
            {
                yield return $"its method '{method.Name}' must return Task, ValueTask or void";
            }
        }
    }

    private static IEnumerable<ISymbol> Members(INamedTypeSymbol type) =>
        new[] { type }.Concat(type.AllInterfaces)
            .SelectMany(face => face.GetMembers())
            .Where(member => !member.IsStatic && member.IsAbstract && member is not IMethodSymbol { MethodKind: not MethodKind.Ordinary });

    private static bool Visible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TakesPath(IMethodSymbol method) =>
        method.Parameters.Length is 1 or 2
        && method.Parameters.All(parameter => parameter.RefKind == RefKind.None && !parameter.IsParams)
        && method.Parameters[0].Type.SpecialType == SpecialType.System_String
        && (method.Parameters.Length == 1 || IsToken(method.Parameters[1].Type));

    private static string? Returns(IMethodSymbol method) =>
        method.ReturnsVoid ? "void"
        : method.ReturnType is INamedTypeSymbol { Name: "Task" or "ValueTask", TypeArguments.Length: 0, ContainingNamespace: { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } } task ? task.Name
        : null;

    private static bool IsToken(ITypeSymbol type) => type.ToDisplayString() == "System.Threading.CancellationToken";

    private static string Implementation(ITypeSymbol type, string name)
    {
        var interfaceName = type.ToDisplayString(Full);
        var code = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            code.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).Append(";\n\n");
        }

        code.Append("/// <summary>Navigates for <see cref=\"").Append(type.ToDisplayString()).Append("\"/> through the page's navigator.</summary>\n");
        code.Append("internal sealed class ").Append(name).Append("(global::Aioros.Navigation.INavigator navigator) : ").Append(interfaceName).Append("\n{\n");
        foreach (var method in Members((INamedTypeSymbol)type).OfType<IMethodSymbol>())
        {
            var path = Escape(method.Parameters[0].Name);
            var token = method.Parameters.Length == 2 ? Escape(method.Parameters[1].Name) : "global::System.Threading.CancellationToken.None";
            var call = "navigator.NavigateAsync(new global::Aioros.Navigation.NavigationRequest(" + path + "), " + token + ")";
            code.Append("    ").Append(method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(Full)).Append(' ')
                .Append(method.ContainingType.ToDisplayString(Full)).Append('.').Append(method.Name).Append('(')
                .Append(string.Join(", ", method.Parameters.Select(parameter => parameter.Type.ToDisplayString(Full) + " " + Escape(parameter.Name))))
                .Append(") =>\n        ")
                .Append(Returns(method) switch
                {
                    "Task" => call + ".AsTask()",
                    "ValueTask" => call,
                    _ => "_ = " + call + ".AsTask()",
                })
                .Append(";\n");
        }

        code.Append("}\n");
        return code.ToString();
    }

    private static string ImplementationName(ITypeSymbol type)
    {
        var bare = type.Name.Length > 1 && type.Name[0] == 'I' && char.IsUpper(type.Name[1]) ? type.Name.Substring(1) : type.Name;
        var outer = new List<string>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            outer.Insert(0, current.Name);
        }

        return "Generated" + string.Concat(outer) + bare;
    }

    private static string Namespace(ITypeSymbol type) =>
        type.ContainingNamespace.IsGlobalNamespace ? "global::" : "global::" + type.ContainingNamespace.ToDisplayString() + ".";

    private static string Escape(string name) =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? name : "@" + name;

    private static string Registry(IEnumerable<string> entries) =>
        "// <auto-generated/>\n#nullable enable\nnamespace Aioros.Generated;\n\n"
        + "/// <summary>Every navigation interface the app registers, with its generated implementation.</summary>\n"
        + "public static class AiorosNavigators\n{\n"
        + "    public static global::System.Collections.Immutable.ImmutableArray<global::Aioros.Runtime.Pages.NavigationInterface> All { get; } =\n"
        + "        [" + string.Join(", ", entries) + "];\n}\n";
}
