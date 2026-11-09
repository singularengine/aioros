using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Aioros.ReactiveUI.Generators.NativeOnly;

/// <summary>
/// Reports, at build time, an <c>Interaction&lt;TInput, TOutput&gt;</c> of a view model that only a native view handles: on
/// the web only the view model runs, so nothing would answer it. A handler the class registers itself runs everywhere.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InteractionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor NativeOnly = new(
        "AIOROSRX001",
        "An interaction is handled only by a native view",
        "'{0}' of '{1}' is an Interaction that only a native view handles: on the web nothing answers it, so register its handler in the view model or replace it with a page or a server function",
        "Aioros.ReactiveUI",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [NativeOnly];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolStartAction(Start, SymbolKind.NamedType);
    }

    private static void Start(SymbolStartAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var interactions = type.GetMembers().Where(member => !member.IsImplicitlyDeclared && member.DeclaredAccessibility != Accessibility.Private && IsInteraction(ValueType(member))).ToImmutableArray();
        if (interactions.IsEmpty)
        {
            return;
        }

        var handled = new HandledMembers();
        context.RegisterOperationAction(operation => handled.Read((IInvocationOperation)operation.Operation), OperationKind.Invocation);
        context.RegisterSymbolEndAction(end =>
        {
            foreach (var member in interactions.Where(member => !handled.Contains(member)))
            {
                end.ReportDiagnostic(Diagnostic.Create(NativeOnly, member.Locations.FirstOrDefault(), member.Name, type.Name));
            }
        });
    }

    private static ITypeSymbol? ValueType(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => null,
    };

    private static bool IsInteraction(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && new[] { named }.Concat(named.AllInterfaces).Any(candidate =>
            candidate.MetadataName == "IInteraction`2" && candidate.ContainingNamespace.ToDisplayString().StartsWith("ReactiveUI", System.StringComparison.Ordinal));

    private sealed class HandledMembers
    {
        private ImmutableHashSet<ISymbol> _members = ImmutableHashSet.Create<ISymbol>(SymbolEqualityComparer.Default);

        public bool Contains(ISymbol member) => Volatile.Read(ref _members).Contains(member);

        public void Read(IInvocationOperation invocation)
        {
            if (invocation.TargetMethod.Name == "RegisterHandler" && invocation.Instance is IMemberReferenceOperation { Member: { } member })
            {
                ImmutableInterlocked.Update(ref _members, static (members, member) => members.Add(member), member.OriginalDefinition);
            }
        }
    }
}
