using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aioros.Routing.Files;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aioros.Generators.Pages;

[Generator]
public sealed class PageRegistryGenerator : IIncrementalGenerator
{
    /// <summary>The app's view error template, shown where an embedded view's loader failed (Next.js <c>error.js</c>).</summary>
    public const string ViewErrorFile = "Pages/ViewError.axaml";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            .Select((file, cancellationToken) => (Path: file.Path, Text: file.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .Collect();
        var directory = context.AnalyzerConfigOptionsProvider
            .Select((options, _) => options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var value) ? value : string.Empty);
        var embed = context.AnalyzerConfigOptionsProvider
            .Select((options, _) => options.GlobalOptions.TryGetValue("build_property.AiorosEmbedSources", out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var server = context.AnalyzerConfigOptionsProvider
            .Select((options, _) => (
                Server: options.GlobalOptions.TryGetValue("build_property.AiorosServer", out var value) && value.Length > 0 ? value : null,
                Scheme: options.GlobalOptions.TryGetValue("build_property.AiorosScheme", out var scheme) && scheme.Length > 0 ? scheme : null));
        var onlyReferences = AppProjects.OnlyReferencesAnApp(context);
        var input = files.Combine(directory).Combine(context.CompilationProvider).Combine(onlyReferences);
        context.RegisterSourceOutput(context.CompilationProvider.Combine(server).Combine(onlyReferences), (output, source) =>
        {
            if (!source.Right)
            {
                EmitApplication(output, source.Left.Left, source.Left.Right.Server, source.Left.Right.Scheme);
            }
        });
        context.RegisterSourceOutput(input, (output, source) =>
        {
            if (!source.Right)
            {
                Generate(output, source.Left.Left.Left, source.Left.Left.Right, source.Left.Right);
            }
        });
        context.RegisterSourceOutput(files.Combine(directory).Combine(embed), (output, source) =>
        {
            if (source.Right)
            {
                output.AddSource("AiorosSources.g.cs", SourceTexts.Emit(source.Left.Left.Select(file => (Relative(file.Path, source.Left.Right), file.Text))));
            }
        });
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<(string Path, string Text)> files, string directory, Compilation compilation)
    {
        var sources = files
            .GroupBy(file => Relative(file.Path, directory), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var map = FileRoutes.Map(sources.Keys);
        foreach (var conflict in map.Conflicts)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.RouteConflict, At(sources[conflict.Second].Path), conflict.Route, conflict.First, conflict.Second));
        }

        var used = Used(map);
        var placements = new ViewPlacements(output, sources, compilation);
        var shapes = new DataShapes(compilation);
        var loaders = PageLoaders.Find(output, compilation, shapes, path => Relative(path, directory), new HashSet<string>(used.Concat(placements.Views), StringComparer.Ordinal));
        var models = new Dictionary<string, (INamedTypeSymbol? Type, bool Live, PageLoader? Loader, string? Head)>(StringComparer.Ordinal);
        (INamedTypeSymbol? Type, bool Live, PageLoader? Loader, string? Head) Of(string source)
        {
            if (!models.TryGetValue(source, out var model))
            {
                var (type, live, head) = Model(output, sources[source], compilation);
                model = (type, live, loaders.TryGetValue(source, out var loader) ? loader : null, head);
                models[source] = model;
            }

            return model;
        }

        List<PlacedView> Placed(FileRoute route) =>
            [.. placements.Of(route.Layouts.Append(route.Source), loaders).Select(view => view.With(Of(view.Source)))];

        NoteLocators(output, compilation);

        var emitter = new RegistryEmitter(ApplicationType(compilation), directory, shapes, sources.ContainsKey(ViewErrorFile) ? ViewErrorFile : null, compilation.GetTypeByMetadataName("Aioros.Pages.PageEngines") is not null, compilation);
        var platforms = PlatformDeclarations.Collect(output, sources);
        foreach (var route in map.Routes)
        {
            var placed = Placed(route);
            emitter.Add(route, Of(route.Source), [.. route.Layouts.Select(layout => (layout, Of(layout)))], placed, platforms[route.Source], sources);
        }

        if (map.NativeOnly is { } nativeOnly)
        {
            emitter.SetNativeOnly(nativeOnly, Of(nativeOnly.Source), [.. nativeOnly.Layouts.Select(layout => (layout, Of(layout)))], Placed(nativeOnly));
        }

        if (map.NotFound is { } notFound)
        {
            emitter.SetNotFound(notFound, Of(notFound.Source), [.. notFound.Layouts.Select(layout => (layout, Of(layout)))], Placed(notFound));
        }

        if (map.Error is { } error)
        {
            emitter.SetError(error, Of(error.Source), [.. error.Layouts.Select(layout => (layout, Of(layout)))], Placed(error));
        }

        PageLoaders.Check(output, compilation, models.OrderBy(model => model.Key, StringComparer.Ordinal).Select(model => (At(sources[model.Key].Path), model.Key, model.Value.Type, model.Value.Loader)), loaders.Values);

        output.AddSource("AiorosPages.g.cs", emitter.Build());
        output.AddSource("AiorosLoaderData.g.cs", emitter.BuildData());
        output.AddSource("Routes.g.cs", RouteMethods.Emit(map.Routes.Select(route => route.Route)));
        EmitAccessors(output, compilation, files);
    }

    /// <summary>
    /// Notes every project class that builds controls from data at run time (an Avalonia <c>IDataTemplate</c>, such as a
    /// <c>ViewLocator</c>): the views it creates cannot be found from the XAML, so they are not composed with their page.
    /// </summary>
    private static void NoteLocators(SourceProductionContext output, Compilation compilation)
    {
        foreach (var type in PageLoaders.Types(compilation.Assembly.GlobalNamespace).Where(type => type.TypeKind == TypeKind.Class && !type.IsAbstract
            && type.AllInterfaces.Any(face => face.Name == "IDataTemplate" && face.ContainingNamespace?.ToDisplayString() == "Avalonia.Controls.Templates")))
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.ChosenAtRunTime, type.Locations.FirstOrDefault(location => location.IsInSource),
                $"'{type.ToDisplayString()}' chooses views at run time: the views it creates are not composed with their page, and their loaders do not run with the page's"));
        }
    }

    private static void EmitAccessors(SourceProductionContext output, Compilation compilation, ImmutableArray<(string Path, string Text)> files)
    {
        if (compilation.GetTypeByMetadataName(Plans.PlanAccessors.AccessorType) is not null)
        {
            var pages = files.Select(file => XamlPage.Parse(file.Text)).ToList();
            output.AddSource("AiorosAccessors.g.cs", Plans.PlanAccessors.Emit(compilation, pages.SelectMany(page => page.DataTypes()), pages.SelectMany(page => page.BindingPaths())));
        }

        if (Plans.PlanCode.EmitsTypes(compilation))
        {
            output.AddSource("AiorosTypes.g.cs", Plans.PlanCode.Types(compilation, files.SelectMany(file => XamlPage.Parse(file.Text).DataTypes())));
        }

        if (Plans.PlanCode.EmitsCommands(compilation))
        {
            output.AddSource("AiorosCommands.g.cs", Plans.PlanCode.Commands(compilation, files.SelectMany(file => XamlPage.Parse(file.Text).DataTypes())));
        }

        if (Plans.PlanCode.EmitsConverters(compilation))
        {
            output.AddSource("AiorosConverters.g.cs", Plans.PlanCode.Converters(compilation));
        }

        if (Plans.SelectorCode.Emits(compilation))
        {
            output.AddSource("AiorosSelectors.g.cs", Plans.SelectorCode.Emit(compilation));
        }

        if (Plans.SlotWriterCode.Emits(compilation))
        {
            output.AddSource("AiorosSlotWriters.g.cs", Plans.SlotWriterCode.Emit(compilation));
        }

        if (Plans.IconSetCode.Emits(compilation))
        {
            output.AddSource("AiorosIcons.g.cs", Plans.IconSetCode.Emit(compilation));
        }
    }

    private static void EmitApplication(SourceProductionContext output, Compilation compilation, string? server, string? scheme)
    {
        var create = compilation.GetTypeByMetadataName("Aioros.Hosting.AiorosHost")?.GetMembers("CreateBuilder").OfType<IMethodSymbol>().FirstOrDefault();
        if (create is not null)
        {
            var app = compilation.GetTypeByMetadataName("Aioros.Hosting.AiorosApp");
            var generated = new[]
            {
                ("DeferredAccessors", "AiorosAccessors.Deferred", compilation.GetTypeByMetadataName(Plans.PlanAccessors.AccessorType) is not null && compilation.GetTypeByMetadataName(Plans.PlanAccessors.DeferredType) is not null),
                ("Types", "AiorosTypes.All", Plans.PlanCode.EmitsTypes(compilation)),
                ("Commands", "AiorosCommands.All", Plans.PlanCode.EmitsCommands(compilation)),
                ("Converters", "AiorosConverters.Code", Plans.PlanCode.EmitsConverters(compilation)),
                ("Selectors", "AiorosSelectors.All", Plans.SelectorCode.Emits(compilation)),
                ("SlotWriters", "AiorosSlotWriters.All", Plans.SlotWriterCode.Emits(compilation)),
                ("Icons", "AiorosIcons.All", Plans.IconSetCode.Emits(compilation)),
                ("NativeOnly", "AiorosPages.NativeOnly", true),
            }.Where(entry => entry.Item3 && app?.GetMembers(entry.Item1).Any() == true)
                .Aggregate(string.Empty, (code, entry) => code + "\n                        " + entry.Item1 + " = global::Aioros.Generated." + entry.Item2 + ",");
            if (scheme is not null && app?.GetMembers("Scheme").Any() == true)
            {
                generated += "\n                        Scheme = " + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(scheme, true) + ",";
            }

            output.AddSource("AiorosApplication.g.cs", RegistryEmitter.EntryPoint(create.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), server, generated));
        }
    }

    /// <summary>The pages and layouts of the app, by their path relative to the project, with the view model each names in <c>x:DataType</c>.</summary>
    internal static IEnumerable<(string Source, string DataType)> ViewModels(IEnumerable<(string Path, string Text)> files, string directory)
    {
        var sources = files
            .GroupBy(file => Relative(file.Path, directory), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var source in Used(FileRoutes.Map(sources.Keys)).Distinct(StringComparer.Ordinal))
        {
            var page = XamlPage.Parse(sources[source].Text);
            if (page.Error is null && page.DataType is { } dataType)
            {
                yield return (source, dataType);
            }
        }
    }

    private static List<string> Used(FileRouteMap map) =>
        map.Routes.SelectMany(route => route.Layouts.Append(route.Source))
            .Concat(new[] { map.NotFound, map.Error, map.NativeOnly }.Where(special => special is not null).SelectMany(special => special!.Layouts.Append(special.Source)))
            .ToList();

    private static (INamedTypeSymbol? Type, bool Live, string? Head) Model(SourceProductionContext output, (string Path, string Text) file, Compilation compilation)
    {
        var page = XamlPage.Parse(file.Text);
        if (page.Error is not null)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownModel, At(file.Path), file.Path, page.Error));
            return (null, page.Live, page.Head);
        }

        if (page.DataType is null)
        {
            return (null, page.Live, page.Head);
        }

        var type = compilation.GetTypeByMetadataName(page.DataType);
        if (type is null)
        {
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownModel, At(file.Path), file.Path, $"the view model '{page.DataType}' does not exist"));
        }

        return (type, page.Live, page.Head);
    }

    private static INamedTypeSymbol? ApplicationType(Compilation compilation)
    {
        var application = compilation.GetTypeByMetadataName("Avalonia.Application");
        return application is null ? null : Types(compilation.Assembly.GlobalNamespace).FirstOrDefault(type => Derives(type, application));
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space) =>
        space.GetTypeMembers().Concat(space.GetNamespaceMembers().SelectMany(Types));

    private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    internal static string Relative(string path, string directory)
    {
        var normalized = path.Replace('\\', '/');
        var root = directory.Replace('\\', '/').TrimEnd('/') + "/";
        if (directory.Length > 0 && normalized.StartsWith(root, StringComparison.Ordinal))
        {
            return normalized.Substring(root.Length);
        }

        var pages = normalized.LastIndexOf("/Pages/", StringComparison.Ordinal);
        return pages < 0 ? normalized : normalized.Substring(pages + 1);
    }

    private static Location At(string path) => Location.Create(path, default, new LinePositionSpan(default, default));
}
