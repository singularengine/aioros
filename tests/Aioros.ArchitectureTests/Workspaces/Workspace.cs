using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Aioros.ArchitectureTests.Workspaces;

public sealed record Workspace(
    ImmutableArray<SourceProject> Projects,
    ImmutableArray<string> Unmapped,
    ImmutableArray<string> Missing)
{
    public ProjectRole RoleOf(string project) => Projects.Single(p => p.Name == project).Role;

    public static async Task<Workspace> LoadAsync(string root, ImmutableDictionary<string, ProjectRole> map)
    {
        var projects = ImmutableArray.CreateBuilder<SourceProject>();
        var unmapped = ImmutableArray.CreateBuilder<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Where(IsSource).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!map.TryGetValue(name, out var role))
            {
                unmapped.Add(name);
                continue;
            }

            projects.Add(await LoadProjectAsync(file, name, role));
        }

        var found = projects.Select(p => p.Name).Concat(unmapped).ToImmutableHashSet();
        var missing = map.Keys.Where(name => !found.Contains(name)).Order(StringComparer.Ordinal).ToImmutableArray();
        return new Workspace(projects.ToImmutable(), unmapped.ToImmutable(), missing);
    }

    private static async Task<SourceProject> LoadProjectAsync(string file, string name, ProjectRole role)
    {
        var xml = XDocument.Parse(await File.ReadAllTextAsync(file));
        ImmutableArray<string> Includes(string element) =>
            [.. xml.Descendants(element).Select(e => (string?)e.Attribute("Include")).OfType<string>()];

        var directory = Path.GetDirectoryName(file)!;
        var trees = ImmutableArray.CreateBuilder<Microsoft.CodeAnalysis.SyntaxTree>();
        foreach (var source in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(IsSource).Order(StringComparer.Ordinal))
        {
            trees.Add(CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(source), path: source));
        }

        return new SourceProject(
            name,
            role,
            [.. Includes("ProjectReference").Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))],
            Includes("PackageReference"),
            Includes("FrameworkReference"),
            trees.ToImmutable());
    }

    private static bool IsSource(string path) =>
        !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");
}
