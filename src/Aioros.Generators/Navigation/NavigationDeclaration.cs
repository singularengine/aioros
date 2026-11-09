using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aioros.Generators.Navigation;

internal sealed class NavigationDeclaration(ITypeSymbol type, Location location)
{
    public const string HostRegistration = "Microsoft.AspNetCore.Builder.AiorosNavigationRegistration";
    private const string ServiceRegistration = "Aioros.Runtime.Pages.NavigationInterfaceRegistration";
    private const string Method = "AddNavigation";

    public ITypeSymbol Type { get; } = type;

    public Location Location { get; } = location;

    public static bool IsCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax invocation
        && (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax member } ? member : invocation.Expression as GenericNameSyntax) is { } name
        && name.Identifier.ValueText == Method
        && name.TypeArgumentList.Arguments.Count == 1;

    public static NavigationDeclaration? Read(GeneratorSyntaxContext context, CancellationToken cancellationToken) =>
        Read((InvocationExpressionSyntax)context.Node, context.SemanticModel, cancellationToken);

    public static NavigationDeclaration? Read(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        var info = model.GetSymbolInfo(invocation, cancellationToken);
        if ((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is IMethodSymbol { Name: Method, TypeArguments.Length: 1 } method)
        {
            var owner = method.ContainingType?.ToDisplayString();
            return owner is HostRegistration or ServiceRegistration ? new NavigationDeclaration(method.TypeArguments[0], invocation.GetLocation()) : null;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax name } access
            || model.GetTypeInfo(access.Expression, cancellationToken).Type is not { TypeKind: TypeKind.Error }
            || model.Compilation.GetTypeByMetadataName(HostRegistration) is null
            || invocation.ArgumentList.Arguments.Count != 0)
        {
            return null;
        }

        return model.GetTypeInfo(name.TypeArgumentList.Arguments.Single(), cancellationToken).Type is { TypeKind: not TypeKind.Error } type
            ? new NavigationDeclaration(type, invocation.GetLocation())
            : null;
    }
}
