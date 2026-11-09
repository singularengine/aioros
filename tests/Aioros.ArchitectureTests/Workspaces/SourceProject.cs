using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Aioros.ArchitectureTests.Workspaces;

public sealed record SourceProject(
    string Name,
    ProjectRole Role,
    ImmutableArray<string> ProjectReferences,
    ImmutableArray<string> PackageReferences,
    ImmutableArray<string> FrameworkReferences,
    ImmutableArray<SyntaxTree> Files);
