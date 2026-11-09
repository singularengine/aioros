using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aioros.Generators.Pages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Aioros.Generators.SharedState;

/// <summary>
/// Reports state in page and layout view models that a multi-user server would share between users: a view model
/// registered as a singleton, static mutable state, and process-wide messengers or locators.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedStateAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor SingletonViewModel = new(
        "AIOROS012",
        "A page or layout view model is registered as a singleton",
        "'{0}' is the view model of '{1}' and is registered as a singleton: one instance would serve every user and request on a server and keep their state between them; register it as scoped or transient, and keep shared data in a singleton service it takes",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StaticState = new(
        "AIOROS013",
        "A view model holds static mutable state",
        "{0}: '{1}' is {2}, so every user shares it on a server; keep the state on the instance, or in a service registered for the scope that owns it",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProcessWide = new(
        "AIOROS014",
        "A view model uses a process-wide messenger or locator",
        "{0}: the view model uses '{1}', one instance for the whole process, so on a server every user's view models share it; {2}",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private const string Messaging = "CommunityToolkit.Mvvm.Messaging.";
    private const string InjectMessenger = "take an IMessenger registered as scoped (services.AddScoped<IMessenger, WeakReferenceMessenger>()) through the constructor";
    private const string InjectServices = "take the services through the constructor, from the page's scope";

    private static readonly ImmutableHashSet<string> Singletons =
        ImmutableHashSet.Create(StringComparer.Ordinal, "AddSingleton", "TryAddSingleton", "AddKeyedSingleton", "TryAddKeyedSingleton");

    private static readonly ImmutableDictionary<string, string> Shared = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Messaging + "WeakReferenceMessenger.Default"] = InjectMessenger,
        [Messaging + "StrongReferenceMessenger.Default"] = InjectMessenger,
        ["Splat.Locator.Current"] = InjectServices,
        ["Splat.AppLocator.Current"] = InjectServices,
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SingletonViewModel, StaticState, ProcessWide);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var models = ViewModels(context.Compilation, context.Options, context.CancellationToken);
        if (models.Count == 0)
        {
            return;
        }

        var owners = Owners(models);
        context.RegisterOperationAction(registration => Registration(registration, models), OperationKind.Invocation);
        context.RegisterSymbolAction(member => Static(member, owners), SymbolKind.Field, SymbolKind.Property);
        context.RegisterOperationAction(use => Use(use, owners), OperationKind.PropertyReference);
    }

    private static Dictionary<INamedTypeSymbol, string> ViewModels(Compilation compilation, AnalyzerOptions options, System.Threading.CancellationToken cancellationToken)
    {
        var directory = options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var value) ? value : string.Empty;
        var files = options.AdditionalFiles
            .Where(file => file.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            .Select(file => (file.Path, Text: file.GetText(cancellationToken)?.ToString() ?? string.Empty));
        var models = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        foreach (var (source, dataType) in PageRegistryGenerator.ViewModels(files, directory))
        {
            if (compilation.GetTypeByMetadataName(dataType) is { } type && !models.ContainsKey(type))
            {
                models[type] = source;
            }
        }

        return models;
    }

    private static Dictionary<INamedTypeSymbol, INamedTypeSymbol> Owners(Dictionary<INamedTypeSymbol, string> models)
    {
        var owners = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var model in models.Keys)
        {
            for (var type = model; type is not null && type.Locations.Any(location => location.IsInSource); type = type.BaseType)
            {
                if (!owners.ContainsKey(type))
                {
                    owners[type] = model;
                }
            }
        }

        return owners;
    }

    private static void Registration(OperationAnalysisContext context, Dictionary<INamedTypeSymbol, string> models)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!Singletons.Contains(method.Name) || method.Parameters.Length == 0 || method.Parameters[0].Type.Name != "IServiceCollection")
        {
            return;
        }

        var named = method.TypeArguments
            .Concat(invocation.Arguments.Select(argument => argument.Value).OfType<ITypeOfOperation>().Select(type => type.TypeOperand))
            .OfType<INamedTypeSymbol>();
        foreach (var type in named)
        {
            if (models.TryGetValue(type.OriginalDefinition, out var source))
            {
                context.ReportDiagnostic(Diagnostic.Create(SingletonViewModel, invocation.Syntax.GetLocation(), type.ToDisplayString(), source));
                return;
            }
        }
    }

    private static void Static(SymbolAnalysisContext context, Dictionary<INamedTypeSymbol, INamedTypeSymbol> owners)
    {
        var member = context.Symbol;
        if (!member.IsStatic || member.IsImplicitlyDeclared || !owners.TryGetValue(member.ContainingType, out var model))
        {
            return;
        }

        var reason = member switch
        {
            IFieldSymbol { IsConst: true } => null,
            IFieldSymbol { IsReadOnly: false } => "a static field that is not readonly",
            IFieldSymbol field when Mutable(field.Type) => "a static readonly mutable collection",
            IPropertySymbol { SetMethod: not null } => "a static property with a setter",
            _ => null,
        };
        if (reason is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(StaticState, member.Locations[0], model.ToDisplayString(), member.ToDisplayString(), reason));
        }
    }

    private static void Use(OperationAnalysisContext context, Dictionary<INamedTypeSymbol, INamedTypeSymbol> owners)
    {
        var property = ((IPropertyReferenceOperation)context.Operation).Property;
        if (!property.IsStatic || !Shared.TryGetValue(property.ContainingType.ToDisplayString() + "." + property.Name, out var advice))
        {
            return;
        }

        for (var type = context.ContainingSymbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (owners.TryGetValue(type, out var model))
            {
                context.ReportDiagnostic(Diagnostic.Create(ProcessWide, context.Operation.Syntax.GetLocation(), model.ToDisplayString(), property.ContainingType.ToDisplayString() + "." + property.Name, advice));
                return;
            }
        }
    }

    private static bool Mutable(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named || type.TypeKind is TypeKind.Array)
        {
            return false;
        }

        var space = named.ContainingNamespace?.ToDisplayString();
        if (space is "System.Collections.Immutable" or "System.Collections.Frozen" || named.Name.StartsWith("ReadOnly", StringComparison.Ordinal))
        {
            return false;
        }

        return Collection(named) || named.AllInterfaces.Any(Collection);
    }

    private static bool Collection(INamedTypeSymbol type) =>
        (type.ContainingNamespace?.ToDisplayString(), type.Name, type.Arity) switch
        {
            ("System.Collections.Generic", "ICollection", 1) => true,
            ("System.Collections.Concurrent", "IProducerConsumerCollection", 1) => true,
            ("System.Collections", "IList" or "IDictionary", 0) => true,
            _ => false,
        };
}
