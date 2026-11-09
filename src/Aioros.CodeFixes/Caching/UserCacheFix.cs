using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aioros.CodeFixes.Caching;

/// <summary>
/// Fixes <c>AIOROS024</c>: drops <c>Shared</c> from a server function's <c>[Cached]</c>, so its results are kept per signed-in
/// user, or removes the <c>[Cached]</c> of a loader whose data depends on the user.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UserCacheFix))]
[Shared]
public sealed class UserCacheFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["AIOROS024"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var attribute = root?.FindNode(context.Span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault()
            ?? root?.FindNode(context.Span).DescendantNodesAndSelf().OfType<AttributeSyntax>().FirstOrDefault(static attribute => Named(attribute, "Cached"));
        if (root is null || attribute is null)
        {
            return;
        }

        var shared = attribute.ArgumentList?.Arguments.FirstOrDefault(static argument => argument.NameEquals?.Name.Identifier.Text == "Shared");
        var fixedRoot = shared is not null
            ? root.ReplaceNode(attribute.ArgumentList!, attribute.ArgumentList!.WithArguments(attribute.ArgumentList.Arguments.Remove(shared)))
            : attribute.Parent is AttributeListSyntax { Attributes.Count: 1 } list
                ? root.RemoveNode(list, SyntaxRemoveOptions.KeepLeadingTrivia)
                : root.RemoveNode(attribute, SyntaxRemoveOptions.KeepNoTrivia);
        if (fixedRoot is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(shared is not null ? "Keep the results per user (drop Shared)" : "Remove [Cached]", _ => Task.FromResult(context.Document.WithSyntaxRoot(fixedRoot)), nameof(UserCacheFix)),
            context.Diagnostics);
    }

    private static bool Named(AttributeSyntax attribute, string name) =>
        attribute.Name.ToString() is var text && (text == name || text == name + "Attribute" || text.EndsWith("." + name, System.StringComparison.Ordinal) || text.EndsWith("." + name + "Attribute", System.StringComparison.Ordinal));
}
