using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

/// <summary>Tells the project that is an Aioros app from one that only references an app, as a test project does.</summary>
internal static class AppProjects
{
    private const string Registry = "Aioros.Generated.AiorosPages";

    private const string PagesFolder = "Pages/";

    /// <summary>Whether the project references an assembly that is an app and has no page of its own, so the app's registries are not generated again in it.</summary>
    public static IncrementalValueProvider<bool> OnlyReferencesAnApp(IncrementalGeneratorInitializationContext context)
    {
        var pages = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, _) => file.Path)
            .Collect();
        var directory = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var value) ? value : string.Empty);
        return pages.Combine(directory).Combine(context.CompilationProvider).Select(static (input, _) =>
            !input.Left.Left.Any(path => PageRegistryGenerator.Relative(path, input.Left.Right).StartsWith(PagesFolder, StringComparison.Ordinal))
            && input.Right.GetTypesByMetadataName(Registry).Length > 0);
    }
}
