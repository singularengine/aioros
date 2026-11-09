using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aioros.ArchitectureTests.Acceptance;

public sealed partial record AcceptanceWorkspace(ImmutableArray<string> Criteria, ImmutableArray<CoveringTest> Tests)
{
    public static async Task<AcceptanceWorkspace> LoadAsync(string root)
    {
        var criteria = ImmutableArray.CreateBuilder<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "docs", "acceptance"), "*.md").Order(StringComparer.Ordinal))
        {
            foreach (var line in await File.ReadAllLinesAsync(file))
            {
                var match = CriterionHeading().Match(line);
                if (match.Success)
                {
                    criteria.Add(match.Groups[1].Value);
                }
            }
        }

        var tests = ImmutableArray.CreateBuilder<CoveringTest>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories).Where(path => IsTestSource(Path.GetRelativePath(root, path))).Order(StringComparer.Ordinal))
        {
            var tree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(file));
            tests.AddRange((await tree.GetRootAsync()).DescendantNodes().OfType<MethodDeclarationSyntax>().Where(IsTest).Select(ToCoveringTest));
        }

        return new AcceptanceWorkspace(criteria.ToImmutable(), tests.ToImmutable());
    }

    private static bool IsTestSource(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        return !parts.Any(part => part is "bin" or "obj" or "Fixtures" or "Aioros.ArchitectureTests");
    }

    private static bool IsTest(MethodDeclarationSyntax method) =>
        Attributes(method).Any(a => a.Name.ToString() is "Fact" or "Theory");

    private static CoveringTest ToCoveringTest(MethodDeclarationSyntax method) =>
        new(method.Identifier.Text, [.. Attributes(method).Where(IsCriterionTrait).Select(a => Text(a.ArgumentList!.Arguments[1]))]);

    private static bool IsCriterionTrait(AttributeSyntax attribute) =>
        attribute.Name.ToString() == "Trait"
        && attribute.ArgumentList is { Arguments.Count: 2 } arguments
        && Text(arguments.Arguments[0]) == "AC";

    private static IEnumerable<AttributeSyntax> Attributes(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(list => list.Attributes);

    private static string Text(AttributeArgumentSyntax argument) =>
        argument.Expression is LiteralExpressionSyntax literal ? literal.Token.ValueText : argument.Expression.ToString();

    [GeneratedRegex(@"^###\s+([A-Z]+-\d{2,})\b")]
    private static partial Regex CriterionHeading();
}
