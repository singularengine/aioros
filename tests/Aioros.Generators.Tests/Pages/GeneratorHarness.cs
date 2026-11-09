using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Aioros.Generators.Pages;
using Aioros.Pages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Aioros.Generators.Tests.Pages;

internal sealed class GeneratorHarness
{
    private const string Directory = "/app";

    private readonly List<AdditionalText> _files = [];
    private readonly List<(string Code, string Path)> _sources = [];
    private readonly List<string> _generated = [];
    private readonly List<byte[]> _libraries = [];
    private readonly Dictionary<string, string> _properties = new(StringComparer.Ordinal) { ["build_property.MSBuildProjectDirectory"] = Directory };

    public GeneratorHarness Property(string name, string value)
    {
        _properties["build_property." + name] = value;
        return this;
    }

    public GeneratorHarness Xaml(string path, string body, string? dataType = null, string extra = "")
    {
        var type = dataType is null ? string.Empty : $" x:DataType=\"vm:{dataType}\"";
        _files.Add(new InMemoryText($"{Directory}/{path}", $"""
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:a="https://aioros.dev" xmlns:vm="using:App.Models"{type}{extra}>
              {body}
            </UserControl>
            """));
        return this;
    }

    public GeneratorHarness Source(string code, string path = "")
    {
        _sources.Add((code, path.Length == 0 ? string.Empty : $"{Directory}/{path}"));
        return this;
    }

    /// <summary>Code another source generator adds, which the generators under test cannot see but the app compiles with.</summary>
    public GeneratorHarness Generated(string code)
    {
        _generated.Add(code);
        return this;
    }

    /// <summary>An assembly the app references, compiled from this code, whose types the generators see only as metadata.</summary>
    public GeneratorHarness Library(string name, string code, Libraries references = Libraries.All)
    {
        var library = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken)],
            references switch
            {
                Libraries.Framework => Framework(),
                Libraries.Abstractions => Framework().Append(MetadataReference.CreateFromFile(typeof(Aioros.Icons.IIconSet).Assembly.Location)),
                Libraries.FrameworkAndLibraries => Framework().Concat(_libraries.Select(image => MetadataReference.CreateFromImage(image))),
                _ => References(),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emitted = library.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        _libraries.Add(stream.ToArray());
        return this;
    }

    public GeneratorResult Run()
    {
        var compilation = CSharpCompilation.Create(
            "App",
            _sources.Select(source => CSharpSyntaxTree.ParseText(source.Code, path: source.Path, cancellationToken: TestContext.Current.CancellationToken)),
            References().Concat(_libraries.Select(image => MetadataReference.CreateFromImage(image))),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
            [new PageRegistryGenerator().AsSourceGenerator(), new Aioros.Generators.Functions.ServerFunctionGenerator().AsSourceGenerator(), new Aioros.Generators.Navigation.NavigationGenerator().AsSourceGenerator()],
            _files,
            optionsProvider: new Options(_properties));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics, TestContext.Current.CancellationToken);
        output = output.AddSyntaxTrees(_generated.Select(code => CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken)));
        return new GeneratorResult(output, diagnostics, _libraries);
    }

    public async Task<ImmutableArray<Diagnostic>> AnalyzeAsync()
    {
        var compilation = CSharpCompilation.Create(
            "App",
            _sources.Select(source => CSharpSyntaxTree.ParseText(source.Code, path: source.Path, cancellationToken: TestContext.Current.CancellationToken)),
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return await compilation
            .WithAnalyzers([new Aioros.Generators.SharedState.SharedStateAnalyzer()], new AnalyzerOptions([.. _files], new Options(_properties)))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The .NET framework alone, without Aioros's assemblies or Avalonia, for a library that references only the abstractions.</summary>
    private static IEnumerable<MetadataReference> Framework() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is var file && (file.StartsWith("System.", StringComparison.Ordinal) || file is "netstandard.dll" or "mscorlib.dll"))
            .Select(path => MetadataReference.CreateFromFile(path));

    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(PageDefinition).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(Aioros.Runtime.Functions.ServerFunctionGroup).Assembly.Location));

    private sealed class InMemoryText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class Options(Dictionary<string, string> properties) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(properties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private sealed class Values(Dictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => properties.TryGetValue(key, out value!);
    }
}

internal sealed class GeneratorResult(Compilation compilation, ImmutableArray<Diagnostic> diagnostics, IReadOnlyList<byte[]>? libraries = null)
{
    public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;

    public IEnumerable<string> Sources => compilation.SyntaxTrees.Select(tree => tree.ToString());

    public ImmutableArray<Diagnostic> WarningsAsErrors() =>
        [.. compilation.WithOptions(compilation.Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error))
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

    public Assembly Load()
    {
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        stream.Position = 0;
        var context = new AssemblyLoadContext("generated", isCollectible: true);
        foreach (var library in libraries ?? [])
        {
            context.LoadFromStream(new MemoryStream(library));
        }

        return context.LoadFromStream(stream);
    }

    public ImmutableArray<PageDefinition> Pages(out Assembly assembly)
    {
        assembly = Load();
        var registry = assembly.GetType("Aioros.Generated.AiorosPages")!;
        return (ImmutableArray<PageDefinition>)registry.GetProperty("All")!.GetValue(null)!;
    }

    public Type? ApplicationType() =>
        ((Func<Type>?)Load().GetType("Aioros.Generated.AiorosPages")!.GetProperty("ApplicationType")!.GetValue(null))?.Invoke();
}

internal sealed class Services(params object[] services) : IServiceProvider
{
    public object? GetService(Type serviceType) => services.FirstOrDefault(serviceType.IsInstanceOfType);
}

/// <summary>What a library compiled by the harness references: everything the tests load, the abstractions and the framework, or the framework alone.</summary>
public enum Libraries
{
    All,
    Abstractions,
    Framework,

    /// <summary>The .NET framework and the libraries added before, for a library that references another (one built on Avalonia).</summary>
    FrameworkAndLibraries,
}
