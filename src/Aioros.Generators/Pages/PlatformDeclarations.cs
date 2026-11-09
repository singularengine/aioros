using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aioros.Generators.Pages;

/// <summary>
/// The platforms a page declares on its root (<c>a:Page.Platforms="Desktop"</c>), read like <c>a:Page.Title</c>, and the
/// files that use Avalonia's own <c>OnPlatform</c> where the web renders them.
/// </summary>
internal static class PlatformDeclarations
{
    private const string Type = "global::Aioros.Pages.PagePlatforms.";

    private static readonly string[] Names = ["Web", "Desktop", "Mobile"];

    /// <summary>
    /// The <c>Platforms</c> value of each file's page definition, or nothing for every platform, after reporting unknown
    /// names and Avalonia's <c>OnPlatform</c> where the web renders it.
    /// </summary>
    public static Dictionary<string, string?> Collect(SourceProductionContext output, Dictionary<string, (string Path, string Text)> sources)
    {
        var platforms = sources.ToDictionary(file => file.Key, file => Read(output, file.Value.Path, XamlPage.Parse(file.Value.Text)), StringComparer.Ordinal);
        ReportServerValues(output, sources, platforms);
        return platforms;
    }

    /// <summary>The page definition's <c>Platforms</c> value, or nothing for every platform; an unknown name is <c>AIOROS031</c>.</summary>
    private static string? Read(SourceProductionContext output, string path, XamlPage page)
    {
        if (page.Platforms is not { } written)
        {
            return null;
        }

        var flags = new List<string>();
        foreach (var part in written.Split(',').Select(part => part.Trim()).Where(part => part.Length > 0))
        {
            var name = Array.Find(Names, known => string.Equals(known, part, StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownPlatform, At(path), path, part));
            }
            else if (!flags.Contains(name))
            {
                flags.Add(name);
            }
        }

        return flags.Count == 0 ? null : Type + string.Join(" | " + Type, flags);
    }

    /// <summary>Reports <c>AIOROS030</c> at each file that uses Avalonia's <c>OnPlatform</c>, unless it is a page the web never renders.</summary>
    private static void ReportServerValues(SourceProductionContext output, Dictionary<string, (string Path, string Text)> sources, Dictionary<string, string?> platforms)
    {
        foreach (var source in sources.OrderBy(source => source.Key, StringComparer.Ordinal))
        {
            var nativeOnly = platforms[source.Key] is { } declared && !declared.Contains(Type + "Web");
            if (!nativeOnly && XamlPage.Parse(source.Value.Text).UsesAvaloniaOnPlatform)
            {
                output.ReportDiagnostic(Diagnostic.Create(Diagnostics.ServerPlatform, At(source.Value.Path), source.Value.Path));
            }
        }
    }

    private static Location At(string path) => Location.Create(path, default, new LinePositionSpan(default, default));
}
