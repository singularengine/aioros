using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aioros.CodeFixes.Services;

/// <summary>Registers a view model <c>AIOROS012</c> reports as a singleton as scoped instead: one instance per request.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ScopedViewModelFix))]
[Shared]
public sealed class ScopedViewModelFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["AIOROS012"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var invocation = root?.FindNode(context.Span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (root is null || invocation?.Expression is not MemberAccessExpressionSyntax { Name: var name } || !name.Identifier.Text.EndsWith("Singleton", System.StringComparison.Ordinal))
        {
            return;
        }

        var scoped = name.Identifier.Text.Substring(0, name.Identifier.Text.Length - "Singleton".Length) + "Scoped";
        context.RegisterCodeFix(
            CodeAction.Create($"Register it with {scoped}", _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceToken(name.Identifier, SyntaxFactory.Identifier(name.Identifier.LeadingTrivia, scoped, name.Identifier.TrailingTrivia)))), nameof(ScopedViewModelFix)),
            context.Diagnostics);
    }
}
