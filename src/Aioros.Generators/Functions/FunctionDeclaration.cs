using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Aioros.Generators.Functions;

internal sealed class FunctionDeclaration(ITypeSymbol service, INamedTypeSymbol? implementation, string? group, string? problem, Location location, bool marked)
{
    private const string Registration = "Microsoft.AspNetCore.Builder.AiorosFunctionRegistration";
    private const string Method = "AddServerFunction";

    public ITypeSymbol Service { get; } = service;

    public INamedTypeSymbol? Implementation { get; } = implementation;

    public string? Group { get; } = group;

    public string? Problem { get; } = problem;

    public Location Location { get; } = location;

    public bool Marked { get; } = marked;

    public static bool IsCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax invocation
        && (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax member } ? member : invocation.Expression as GenericNameSyntax) is { } name
        && name.Identifier.ValueText == Method
        && name.TypeArgumentList.Arguments.Count == 2;

    public static FunctionDeclaration? Read(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: Method, TypeArguments.Length: 2 } method)
        {
            return method.ContainingType?.ToDisplayString() == Registration
                ? Declare(invocation, method.TypeArguments[0], method.TypeArguments[1], (model.GetOperation(invocation, cancellationToken) as IInvocationOperation)?.Arguments
                    .FirstOrDefault(candidate => candidate.Parameter?.Name == "group" && candidate.ArgumentKind == ArgumentKind.Explicit)?.Value.ConstantValue)
                : null;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax name } access
            || model.GetTypeInfo(access.Expression, cancellationToken).Type is not { TypeKind: TypeKind.Error }
            || model.Compilation.GetTypeByMetadataName(Registration) is null)
        {
            return null;
        }

        var types = name.TypeArgumentList.Arguments.Select(argument => model.GetTypeInfo(argument, cancellationToken).Type).ToList();
        var group = invocation.ArgumentList.Arguments.FirstOrDefault(argument => argument.NameColon is null || argument.NameColon.Name.Identifier.ValueText == "group");
        return types[0] is { } service && types[1] is { } implementation
            ? Declare(invocation, service, implementation, group is null ? null : model.GetConstantValue(group.Expression, cancellationToken))
            : null;
    }

    private static FunctionDeclaration Declare(InvocationExpressionSyntax invocation, ITypeSymbol service, ITypeSymbol implementation, Optional<object?>? constant)
    {
        var group = constant is { HasValue: true } ? constant.Value.Value as string : null;
        var problem = constant is { HasValue: false } ? "its group is not a constant string, so its route could change without the build knowing"
            : group is { Length: 0 } ? "its group is empty"
            : null;
        return new FunctionDeclaration(service, implementation as INamedTypeSymbol, problem is null ? group : null, problem, invocation.GetLocation(), marked: false);
    }

    public static FunctionDeclaration Mark(GeneratorAttributeSyntaxContext context) =>
        new(
            (INamedTypeSymbol)context.TargetSymbol,
            null,
            context.Attributes[0].ConstructorArguments.Length > 0 ? context.Attributes[0].ConstructorArguments[0].Value as string : null,
            null,
            context.TargetSymbol.Locations[0],
            marked: true);
}
