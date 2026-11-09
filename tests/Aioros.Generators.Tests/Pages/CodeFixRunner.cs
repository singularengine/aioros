using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Aioros.Generators.Tests.Pages;

/// <summary>Applies a code fix to the file a diagnostic is in, as the IDE does, and gives the file it leaves.</summary>
internal static class CodeFixRunner
{
    public static async Task<string> ApplyAsync(CodeFixProvider fix, Diagnostic diagnostic)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("App", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Source.cs", await diagnostic.Location.SourceTree!.GetTextAsync(cancellationToken));
        var actions = new List<CodeAction>();
        await fix.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken));
        var operations = await Assert.Single(actions).GetOperationsAsync(cancellationToken);
        var changed = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync(cancellationToken)).ToString();
    }
}
