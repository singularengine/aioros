using System.Collections.Immutable;

namespace Aioros.ArchitectureTests.Workspaces;

public static class ProjectMaps
{
    public static ImmutableDictionary<string, ProjectRole> Production { get; } = new Dictionary<string, ProjectRole>
    {
        ["Aioros.Abstractions"] = ProjectRole.Abstractions,
        ["Aioros.Routing"] = ProjectRole.Routing,
        ["Aioros.Runtime"] = ProjectRole.Runtime,
        ["Aioros.Generators"] = ProjectRole.Generator,
        ["Aioros.CodeFixes"] = ProjectRole.Generator,
        ["Aioros.Avalonia"] = ProjectRole.Adapter,
        ["Aioros.ReactiveUI"] = ProjectRole.MvvmAdapter,
        ["Aioros.ReactiveUI.Generators"] = ProjectRole.Generator,
        ["Aioros.Targets.Ssr"] = ProjectRole.WebTarget,
        ["Aioros.Targets.Desktop"] = ProjectRole.NativeTarget,
        ["Aioros.Platform"] = ProjectRole.Platform,
        ["Aioros.Markup"] = ProjectRole.Markup,
        ["Aioros.Features.Live"] = ProjectRole.Feature,
        ["Aioros.Features.HotReload"] = ProjectRole.Feature,
        ["Aioros.Cli"] = ProjectRole.Tool,
        ["Aioros.Testing"] = ProjectRole.Testing,
        ["Aioros"] = ProjectRole.Bundle,
    }.ToImmutableDictionary();

    public static ImmutableDictionary<string, ProjectRole> Compliant { get; } = new Dictionary<string, ProjectRole>
    {
        ["Good.Abstractions"] = ProjectRole.Abstractions,
        ["Good.Feature"] = ProjectRole.Feature,
    }.ToImmutableDictionary();

    public static ImmutableDictionary<string, ProjectRole> Violating { get; } = new Dictionary<string, ProjectRole>
    {
        ["Bad.Feature"] = ProjectRole.Feature,
        ["Bad.Target"] = ProjectRole.WebTarget,
        ["Ghost.Project"] = ProjectRole.Feature,
    }.ToImmutableDictionary();
}
