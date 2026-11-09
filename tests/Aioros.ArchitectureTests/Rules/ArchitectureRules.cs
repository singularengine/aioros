using System.Collections.Immutable;
using Aioros.ArchitectureTests.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aioros.ArchitectureTests.Rules;

public static class ArchitectureRules
{
    private const int MaxTypeLines = 600;

    private static readonly ImmutableDictionary<ProjectRole, ImmutableHashSet<ProjectRole>> AllowedReferences =
        new Dictionary<ProjectRole, ImmutableHashSet<ProjectRole>>
        {
            [ProjectRole.Abstractions] = [],
            [ProjectRole.Routing] = [ProjectRole.Abstractions],
            [ProjectRole.Runtime] = [ProjectRole.Abstractions],
            [ProjectRole.Generator] = [],
            [ProjectRole.Adapter] = [ProjectRole.Abstractions, ProjectRole.Runtime, ProjectRole.Markup],
            [ProjectRole.MvvmAdapter] = [ProjectRole.Abstractions, ProjectRole.Generator],
            [ProjectRole.WebTarget] = [ProjectRole.Abstractions, ProjectRole.Runtime, ProjectRole.Routing],
            [ProjectRole.NativeTarget] = [ProjectRole.Abstractions, ProjectRole.Runtime, ProjectRole.Routing, ProjectRole.Adapter, ProjectRole.Platform],
            [ProjectRole.Platform] = [],
            [ProjectRole.Markup] = [],
            [ProjectRole.Feature] = [ProjectRole.Abstractions, ProjectRole.Runtime],
            [ProjectRole.Tool] = [ProjectRole.Abstractions, ProjectRole.Runtime, ProjectRole.Routing],
            [ProjectRole.Bundle] = [ProjectRole.Abstractions, ProjectRole.Runtime, ProjectRole.Routing, ProjectRole.Adapter, ProjectRole.WebTarget, ProjectRole.NativeTarget, ProjectRole.Feature, ProjectRole.Generator, ProjectRole.Platform, ProjectRole.Markup],
            [ProjectRole.Testing] = [ProjectRole.Abstractions, ProjectRole.Runtime, ProjectRole.Adapter, ProjectRole.WebTarget],
        }.ToImmutableDictionary();

    private static readonly ImmutableHashSet<ProjectRole> RequestPath =
        [ProjectRole.Abstractions, ProjectRole.Routing, ProjectRole.Runtime, ProjectRole.Feature, ProjectRole.WebTarget, ProjectRole.MvvmAdapter];

    public static ImmutableDictionary<string, Func<Workspace, IEnumerable<Violation>>> All { get; } =
        new Dictionary<string, Func<Workspace, IEnumerable<Violation>>>
        {
            ["every-project-is-mapped"] = EveryProjectIsMapped,
            ["references-follow-roles"] = ReferencesFollowRoles,
            ["avalonia-only-in-adapters"] = AvaloniaOnlyInAdapters,
            ["aspnetcore-only-in-web-targets"] = AspNetCoreOnlyInWebTargets,
            ["every-class-is-sealed"] = EveryClassIsSealed,
            ["no-locks"] = NoLocks,
            ["no-reflection-on-the-request-path"] = NoReflectionOnTheRequestPath,
            ["no-comments"] = NoComments,
            ["no-type-over-600-lines"] = NoTypeOver600Lines,
            ["no-avalonia-namespace-outside-the-adapter"] = NoAvaloniaNamespaceOutsideTheAdapter,
        }.ToImmutableDictionary();

    private static IEnumerable<Violation> EveryProjectIsMapped(Workspace workspace) =>
        workspace.Unmapped.Select(name => new Violation(name, "not in the project map"))
            .Concat(workspace.Missing.Select(name => new Violation(name, "in the project map but not on disk")));

    private static IEnumerable<Violation> ReferencesFollowRoles(Workspace workspace) =>
        from project in workspace.Projects
        from reference in project.ProjectReferences
        let role = workspace.RoleOf(reference)
        where !AllowedReferences[project.Role].Contains(role)
        select new Violation(project.Name, $"references {reference} ({role})");

    private static IEnumerable<Violation> AvaloniaOnlyInAdapters(Workspace workspace) =>
        from project in workspace.Projects
        where project.Role is not (ProjectRole.Adapter or ProjectRole.NativeTarget or ProjectRole.Platform or ProjectRole.Markup)
        from violation in Packages(project, "Avalonia").Concat(Namespaces(project, "Avalonia"))
        select violation;

    private static IEnumerable<Violation> AspNetCoreOnlyInWebTargets(Workspace workspace) =>
        from project in workspace.Projects
        where project.Role is not (ProjectRole.WebTarget or ProjectRole.Tool or ProjectRole.Bundle or ProjectRole.Testing)
        from violation in project.FrameworkReferences
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .Select(name => new Violation(project.Name, $"framework {name}"))
            .Concat(Packages(project, "Microsoft.AspNetCore"))
            .Concat(Namespaces(project, "Microsoft.AspNetCore"))
        select violation;

    private static IEnumerable<Violation> EveryClassIsSealed(Workspace workspace) =>
        from project in workspace.Projects
        from tree in project.Files
        from type in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
        where type is ClassDeclarationSyntax || (type is RecordDeclarationSyntax record && !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword))
        where !type.Modifiers.Any(SyntaxKind.SealedKeyword) && !type.Modifiers.Any(SyntaxKind.StaticKeyword)
        select new Violation(project.Name, $"{type.Identifier.Text} is not sealed");

    private static IEnumerable<Violation> NoLocks(Workspace workspace) =>
        from project in workspace.Projects
        from tree in project.Files
        where tree.GetRoot().DescendantNodes().OfType<LockStatementSyntax>().Any()
        select new Violation(project.Name, $"lock in {Path.GetFileName(tree.FilePath)}");

    private static IEnumerable<Violation> NoReflectionOnTheRequestPath(Workspace workspace) =>
        from project in workspace.Projects
        where RequestPath.Contains(project.Role)
        from violation in Namespaces(project, "System.Reflection")
        select violation;

    private static IEnumerable<Violation> NoComments(Workspace workspace) =>
        from project in workspace.Projects
        from tree in project.Files
        from trivia in tree.GetRoot().DescendantTrivia()
        where trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        select new Violation(project.Name, $"comment in {Path.GetFileName(tree.FilePath)}");

    private static IEnumerable<Violation> NoTypeOver600Lines(Workspace workspace) =>
        from project in workspace.Projects
        from tree in project.Files
        from type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
        let span = type.GetLocation().GetLineSpan()
        where span.EndLinePosition.Line - span.StartLinePosition.Line + 1 > MaxTypeLines
        select new Violation(project.Name, $"{type.Identifier.Text} is longer than {MaxTypeLines} lines");

    private static IEnumerable<Violation> NoAvaloniaNamespaceOutsideTheAdapter(Workspace workspace) =>
        from project in workspace.Projects
        where project.Role is not ProjectRole.Adapter
        from tree in project.Files
        from declared in tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
        let name = declared.Name.ToString()
        where name.Split('.').Skip(1).Contains("Avalonia", StringComparer.Ordinal)
        select new Violation(project.Name, $"namespace {name} shadows Avalonia in {Path.GetFileName(tree.FilePath)}");

    private static IEnumerable<Violation> Packages(SourceProject project, string prefix) =>
        project.PackageReferences
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => new Violation(project.Name, $"package {name}"));

    private static IEnumerable<Violation> Namespaces(SourceProject project, string root) =>
        from tree in project.Files
        let names = tree.GetRoot().DescendantNodes().OfType<NameSyntax>()
            .Where(name => name.Parent is not NameSyntax)
            .Select(name => name.ToString().Replace("global::", string.Empty, StringComparison.Ordinal))
        from used in names.Where(name => name == root || name.StartsWith(root + ".", StringComparison.Ordinal)).Distinct()
        select new Violation(project.Name, $"{used} in {Path.GetFileName(tree.FilePath)}");
}
