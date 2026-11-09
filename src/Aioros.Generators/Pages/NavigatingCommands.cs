using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aioros.Generators.Navigation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aioros.Generators.Pages;

/// <summary>
/// Tells, from the semantic model, whether every command of a view model navigates: a command that navigates is not
/// revalidated (SSR-55), so its view model loses nothing without <c>Receive</c>. Anything it cannot follow counts as not
/// navigating, so <c>AIOROS017</c> stays when unsure.
/// </summary>
internal sealed class NavigatingCommands(Compilation compilation, CancellationToken cancellationToken)
{
    private const string Navigator = "Aioros.Navigation.INavigator";

    private HashSet<ITypeSymbol>? _navigators;

    public bool AllNavigate(IEnumerable<ISymbol> commands) => commands.All(Navigates);

    private bool Navigates(ISymbol command) => command switch
    {
        IMethodSymbol method => Bodies(method).Any() && Bodies(method).All(body => Ends(body.Node, body.Model)),
        IPropertySymbol property => Executed(property).ToList() is { Count: > 0 } executed && executed.All(body => Ends(body.Node, body.Model)),
        _ => false,
    };

    private IEnumerable<(SyntaxNode Node, SemanticModel Model)> Bodies(IMethodSymbol method) =>
        method.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .Select(declaration => ((SyntaxNode?)declaration.ExpressionBody?.Expression ?? declaration.Body, declaration))
            .Where(found => found.Item1 is not null)
            .Select(found => (found.Item1!, compilation.GetSemanticModel(found.declaration.SyntaxTree)));

    /// <summary>
    /// What an <c>ICommand</c> property runs: the method or lambda given first to the command its initializer creates
    /// (<c>new RelayCommand(Save)</c>, <c>ReactiveCommand.CreateFromTask(() => …)</c>); nothing when it cannot be traced.
    /// </summary>
    private IEnumerable<(SyntaxNode Node, SemanticModel Model)> Executed(IPropertySymbol property)
    {
        foreach (var declaration in property.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(cancellationToken)).OfType<PropertyDeclarationSyntax>())
        {
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            var first = First(declaration.Initializer?.Value ?? declaration.ExpressionBody?.Expression);
            if (first is LambdaExpressionSyntax lambda)
            {
                yield return ((SyntaxNode?)lambda.ExpressionBody ?? lambda.Block!, model);
                continue;
            }

            var target = first is null ? null : model.GetSymbolInfo(first, cancellationToken) is var info && (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is IMethodSymbol method ? method : null;
            var bodies = target is not null && SymbolEqualityComparer.Default.Equals(target.ContainingType, property.ContainingType) ? Bodies(target).ToList() : [];
            foreach (var body in bodies.DefaultIfEmpty((declaration, model)))
            {
                yield return body;
            }
        }
    }

    private static ExpressionSyntax? First(ExpressionSyntax? creation)
    {
        var arguments = creation switch
        {
            BaseObjectCreationExpressionSyntax created => created.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };
        return arguments is { Count: > 0 } list ? list[0].Expression : null;
    }

    /// <summary>
    /// Whether a body ends by navigating: an expression that calls a navigation, or a block whose last statement does and
    /// whose other <c>return</c>s navigate or leave with field errors, which revalidates nothing: <c>if (HasErrors) return;</c>,
    /// or a <c>return;</c> ending a <c>catch</c> of <c>ValidationException</c> that first runs the view model's validation.
    /// </summary>
    private bool Ends(SyntaxNode body, SemanticModel model) => body switch
    {
        ExpressionSyntax expression => IsNavigation(expression, model),
        BlockSyntax block => EndsIn(block, model) && Returns(block).All(exit => exit.Expression is { } value ? IsNavigation(value, model) : LeavesWithErrors(exit, model) || LeavesRefused(exit, model)),
        _ => false,
    };

    private bool EndsIn(StatementSyntax statement, SemanticModel model) => statement switch
    {
        BlockSyntax block => block.Statements.LastOrDefault() is { } last && EndsIn(last, model),
        ExpressionStatementSyntax expression => IsNavigation(expression.Expression, model),
        ReturnStatementSyntax { Expression: { } value } => IsNavigation(value, model),
        IfStatementSyntax { Else: { } otherwise } branch => EndsIn(branch.Statement, model) && EndsIn(otherwise.Statement, model),
        _ => false,
    };

    private static IEnumerable<ReturnStatementSyntax> Returns(BlockSyntax block) =>
        block.DescendantNodes(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)).OfType<ReturnStatementSyntax>();

    private bool LeavesWithErrors(ReturnStatementSyntax exit, SemanticModel model)
    {
        var owner = exit.Parent is BlockSyntax { Statements.Count: 1 } block ? block.Parent : exit.Parent;
        return owner is IfStatementSyntax { Else: null } guard
            && model.GetSymbolInfo(guard.Condition, cancellationToken).Symbol is IPropertySymbol { Name: "HasErrors" } property
            && property.ContainingType.AllInterfaces.Concat([property.ContainingType]).Any(face => face.ToDisplayString() == "System.ComponentModel.INotifyDataErrorInfo");
    }

    /// <summary>
    /// A <c>return;</c> that ends a <c>catch</c> of <c>System.ComponentModel.DataAnnotations.ValidationException</c> (a
    /// server's refusal) after calling a method of the view model, which notifies data errors, to record them.
    /// </summary>
    private bool LeavesRefused(ReturnStatementSyntax exit, SemanticModel model)
    {
        if (exit.Parent is not BlockSyntax { Parent: CatchClauseSyntax { Declaration.Type: var caught } } block || block.Statements.Last() != exit)
        {
            return false;
        }

        if (!Derives(model.GetTypeInfo(caught, cancellationToken).Type, "System.ComponentModel.DataAnnotations.ValidationException")
            || model.GetEnclosingSymbol(exit.SpanStart, cancellationToken)?.ContainingType is not { } owner || !NotifiesErrors(owner))
        {
            return false;
        }

        return block.Statements.TakeWhile(statement => statement != exit)
            .SelectMany(statement => statement.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            .Any(call => call.Expression is IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax }
                && model.GetSymbolInfo(call, cancellationToken).Symbol is IMethodSymbol { IsStatic: false } method
                && Inherits(owner, method.ContainingType) && NotifiesErrors(method.ContainingType));
    }

    private static bool Derives(ITypeSymbol? type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == name)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Inherits(ITypeSymbol type, ITypeSymbol ancestor)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NotifiesErrors(ITypeSymbol type) =>
        type.AllInterfaces.Any(face => face.ToDisplayString() == "System.ComponentModel.INotifyDataErrorInfo");

    private bool IsNavigation(ExpressionSyntax expression, SemanticModel model)
    {
        var call = Unwrapped(expression);
        if (call is not InvocationExpressionSyntax invocation || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var owner = method.ReducedFrom?.Parameters.FirstOrDefault()?.Type ?? method.ContainingType;
        return IsNavigator(owner);
    }

    private static ExpressionSyntax Unwrapped(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case AwaitExpressionSyntax awaited:
                    expression = awaited.Expression;
                    break;
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;
                case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ConfigureAwait", Expression: var awaitedCall } }:
                    expression = awaitedCall;
                    break;
                default:
                    return expression;
            }
        }
    }

    private bool IsNavigator(ITypeSymbol type)
    {
        _navigators ??= Registered();
        return _navigators.Contains(type) || type.ToDisplayString() == Navigator || type.AllInterfaces.Any(face => face.ToDisplayString() == Navigator);
    }

    /// <summary>The app's navigation interfaces, registered with <c>AddNavigation&lt;T&gt;()</c>, as the navigation generator finds them.</summary>
    private HashSet<ITypeSymbol> Registered()
    {
        var found = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot(cancellationToken).DescendantNodes().Where(NavigationDeclaration.IsCandidate).Cast<InvocationExpressionSyntax>())
            {
                if (NavigationDeclaration.Read(invocation, model, cancellationToken) is { } declaration)
                {
                    found.Add(declaration.Type);
                }
            }
        }

        return found;
    }
}
