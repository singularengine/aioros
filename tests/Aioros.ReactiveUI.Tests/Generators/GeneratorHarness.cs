using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Aioros.ReactiveUI.Activation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aioros.ReactiveUI.Tests.Generators;

internal static class GeneratorHarness
{
    public static (Compilation Output, ImmutableArray<SyntaxTree> Generated) Generate(string source, bool reactive = true)
    {
        var compilation = CSharpCompilation.Create(
            "App" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), cancellationToken: TestContext.Current.CancellationToken)],
            References(reactive),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(new global::Aioros.ReactiveUI.Generators.Activation.ReactiveViewModelGenerator())
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);
        return (output, driver.GetRunResult().GeneratedTrees);
    }

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var (output, _) = Generate(source);
        var analyzers = output.WithAnalyzers([new global::Aioros.ReactiveUI.Generators.NativeOnly.InteractionAnalyzer()]);
        return await analyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    public static Assembly Load(Compilation output)
    {
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        stream.Position = 0;
        return new AssemblyLoadContext("generated", isCollectible: true).LoadFromStream(stream);
    }

    private static IEnumerable<MetadataReference> References(bool reactive)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => reactive || !Path.GetFileName(path).StartsWith("ReactiveUI", StringComparison.Ordinal));
        var own = reactive
            ? new[] { typeof(global::ReactiveUI.IScreen), typeof(global::ReactiveUI.Binding.Interaction<,>), typeof(ReactiveViewModels), typeof(Aioros.Pages.IModelLifecycle), typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection) }
                .Select(type => type.Assembly.Location)
                .Concat(AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetName().Name!.StartsWith("ReactiveUI", StringComparison.Ordinal)).Select(assembly => assembly.Location))
            : [typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location];
        return platform.Concat(own).Where(path => path.Length > 0).Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
    }
}
