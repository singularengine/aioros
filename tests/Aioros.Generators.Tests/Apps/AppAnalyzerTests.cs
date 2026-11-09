using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Aioros.Generators.Tests.Apps;

public sealed class AppAnalyzerTests
{
    private const string App = """
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class Orders
        {
            public int Blocking(Task<int> total, Task saved, ManualResetEventSlim ready)
            {
                saved.Wait();
                Thread.Sleep(10);
                ready.Wait();
                return total.Result + total.GetAwaiter().GetResult();
            }

            public async Task<int> AwaitedAsync(Task<int> total, Task saved)
            {
                await saved;
                return await total;
            }
        }
        """;

    [Fact]
    [Trait("AC", "PRODUCTION-09")]
    public async Task AppsAreWarnedAboutBlockingCallsAsync()
    {
        var compilation = CSharpCompilation.Create(
            "App",
            [CSharpSyntaxTree.ParseText(App, cancellationToken: TestContext.Current.CancellationToken)],
            [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = Directory.GetFiles(Metadata("BannedApiAnalyzers"), "*.dll")
            .SelectMany(path => new AnalyzerFileReference(path, new Loader()).GetAnalyzers(LanguageNames.CSharp))
            .ToImmutableArray();
        var banned = new BannedList(await File.ReadAllTextAsync(Metadata("AppBannedSymbols"), TestContext.Current.CancellationToken));

        var diagnostics = await compilation
            .WithAnalyzers(analyzers, new AnalyzerOptions([banned]))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        var reported = diagnostics.Where(diagnostic => diagnostic.Id == "RS0030").ToList();
        Assert.Equal(5, reported.Count);
        Assert.All(reported, diagnostic => Assert.Contains("on the desktop it freezes the window. Use await.", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        Assert.All(reported, diagnostic => Assert.True(diagnostic.Location.GetLineSpan().StartLinePosition.Line < 13));
    }

    private static string Metadata(string key) =>
        typeof(AppAnalyzerTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value!;

    private sealed class BannedList(string text) : AdditionalText
    {
        public override string Path => "/app/BannedSymbols.Aioros.txt";

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class Loader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
