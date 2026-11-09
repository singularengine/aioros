using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

/// <summary>What a loader method's result carries: its data, a <c>LoaderResult&lt;TData&gt;</c>, or a <c>LoaderOutcome</c> without data.</summary>
internal enum LoaderShape
{
    Data,
    Result,
    Outcome,
}

/// <summary>
/// A loader found by convention (a public <c>Load</c> or <c>LoadAsync</c> in a <c>.Loader.cs</c> file) with the code that
/// binds its parameters by name from the request, as ASP.NET Core's Request Delegate Generator emits it for Minimal APIs.
/// </summary>
internal sealed class LoaderCall(IMethodSymbol method, ITypeSymbol? data, LoaderShape shape, bool awaited, IReadOnlyList<string> statements, IReadOnlyList<string> arguments)
{
    public IMethodSymbol Method { get; } = method;

    public ITypeSymbol? Data { get; } = data;

    public LoaderShape Shape { get; } = shape;

    public bool Awaited { get; } = awaited;

    public IReadOnlyList<string> Statements { get; } = statements;

    public IReadOnlyList<string> Arguments { get; } = arguments;

    public bool TakesUser => Method.Parameters.Any(parameter => parameter.Type.ToDisplayString() == LoaderCalls.User);
}

internal static class LoaderCalls
{
    public const string User = "System.Security.Claims.ClaimsPrincipal";

    private static readonly SymbolDisplayFormat Full = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly char[] Slash = ['/'];

    public static IMethodSymbol[] Methods(INamedTypeSymbol type) =>
        [.. type.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && method.Name is "Load" or "LoadAsync")];

    public static (LoaderCall? Call, string? Problem, Location? At) Bind(INamedTypeSymbol type, IMethodSymbol[] methods, string file)
    {
        var method = methods[0];
        var at = method.Locations.FirstOrDefault(location => location.IsInSource);
        if (methods.Length > 1)
        {
            return (null, "it has more than one public 'Load' or 'LoadAsync' method; keep one", type.Locations.FirstOrDefault(location => location.IsInSource));
        }

        if (method.IsGenericMethod)
        {
            return (null, $"its method '{method.Name}' is generic; a loader method takes no type arguments", at);
        }

        var (result, awaited) = Unwrapped(method.ReturnType);
        if (result is null)
        {
            return (null, $"its method '{method.Name}' returns '{method.ReturnType.ToDisplayString()}', which carries no result; return the data, a LoaderResult<TData> or a LoaderOutcome, directly or in a Task or ValueTask", at);
        }

        var (data, shape) = result.ToDisplayString() == "Aioros.Pages.LoaderOutcome" ? (null, LoaderShape.Outcome)
            : result is INamedTypeSymbol { IsGenericType: true } wrapped && wrapped.OriginalDefinition.ToDisplayString() == "Aioros.Pages.LoaderResult<TData>" ? (wrapped.TypeArguments[0], LoaderShape.Result)
            : (result, LoaderShape.Data);
        var notFound = awaited
            ? "return global::Aioros.Pages.LoaderResult.NotFound();"
            : "return new global::System.Threading.Tasks.ValueTask<global::Aioros.Pages.LoaderOutcome>(global::Aioros.Pages.LoaderResult.NotFound());";
        var folder = Folder(file);
        var routes = RouteNames(folder);
        var statements = new List<string>();
        var arguments = new List<string>();
        foreach (var parameter in method.Parameters)
        {
            var (argument, problem) = Argument(parameter, parameter.Ordinal, routes, folder, notFound, statements);
            if (problem is not null)
            {
                return (null, problem, parameter.Locations.FirstOrDefault(location => location.IsInSource) ?? at);
            }

            arguments.Add(argument!);
        }

        return (new LoaderCall(method, data, shape, awaited, statements, arguments), null, null);
    }

    /// <summary>The result a loader method's return type carries, unwrapped from a <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c>.</summary>
    public static (ITypeSymbol? Result, bool Awaited) Unwrapped(ITypeSymbol type)
    {
        var name = type.OriginalDefinition.ToDisplayString();
        if (type is INamedTypeSymbol { TypeArguments.Length: 1 } task && name is "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>")
        {
            return (task.TypeArguments[0], true);
        }

        return type.SpecialType == SpecialType.System_Void || name is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" ? (null, false) : (type, false);
    }

    private static (string? Argument, string? Problem) Argument(IParameterSymbol parameter, int index, Dictionary<string, string> routes, string folder, string notFound, List<string> statements)
    {
        if (parameter.RefKind != RefKind.None || parameter.IsParams)
        {
            return (null, $"its parameter '{parameter.Name}' is passed by reference or as params; a loader takes its values by value");
        }

        if (Given(parameter.Type) is { } given)
        {
            return (given, null);
        }

        return Problem(parameter, routes, folder) is { } problem ? (null, problem) : (Converted(parameter, index, routes, notFound, statements), null);
    }

    private static string? Problem(IParameterSymbol parameter, Dictionary<string, string> routes, string folder)
    {
        var type = parameter.Type;
        if (Parser(Underlying(type)) is null)
        {
            return $"its parameter '{parameter.Name}' is a '{type.ToDisplayString()}', which cannot be converted from text; take a string, an enum, a type with IParsable<T> (numbers, Guid, dates) or one of them nullable, the PageRequest, the ClaimsPrincipal or the CancellationToken";
        }

        var optional = parameter.HasExplicitDefaultValue || !SymbolEqualityComparer.Default.Equals(Underlying(type), type) || (!type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated);
        return optional || routes.ContainsKey(parameter.Name)
            ? null
            : $"its parameter '{parameter.Name}' is no route value of '{folder}' and has no default; query values are optional, so make it nullable ('{type.ToDisplayString()}? {parameter.Name}') or give it a default";
    }

    /// <summary>
    /// Emits the statements that read the parameter's value, from the route when its folder declares it (not found when it
    /// does not convert) and from the query otherwise (its default when absent or not converted), and returns the argument.
    /// </summary>
    private static string Converted(IParameterSymbol parameter, int index, Dictionary<string, string> routes, string notFound, List<string> statements)
    {
        var type = parameter.Type;
        var underlying = Underlying(type);
        var parse = Parser(underlying)!;
        var route = routes.TryGetValue(parameter.Name, out var routeName);
        var number = index.ToString(CultureInfo.InvariantCulture);
        var text = "t" + number;
        statements.Add($"var {text} = request.{(route ? "RouteValues" : "Query")}.TryGetValue({Literal(routeName ?? parameter.Name)}, out var s{number}) ? s{number} : null;");
        var fallback = Default(parameter, !type.IsValueType || !SymbolEqualityComparer.Default.Equals(underlying, type));
        if (parse.Length == 0)
        {
            return route ? Required(text, notFound, statements) : fallback == "null" ? text : $"{text} ?? {fallback}";
        }

        var call = $"global::Aioros.Runtime.Pages.LoaderArguments.{parse}<{underlying.ToDisplayString(Full)}>({text}, out var p{number})";
        statements.Add(route ? $"if (!{call}) {{ {notFound} }}" : $"{type.ToDisplayString(Full)} a{number} = {call} ? p{number} : {fallback};");
        return (route ? "p" : "a") + number;
    }

    private static ITypeSymbol Underlying(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

    /// <summary>What the runtime gives a parameter of this type without converting text: the token, the request or the user.</summary>
    private static string? Given(ITypeSymbol type) => type.ToDisplayString() switch
    {
        "System.Threading.CancellationToken" => "cancellationToken",
        "Aioros.Pages.PageRequest" => "request",
        User => "request.User",
        _ => null,
    };

    /// <summary>The <c>LoaderArguments</c> method that converts text to the type, empty for a string, none when it cannot.</summary>
    private static string? Parser(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_String ? string.Empty
        : type.TypeKind == TypeKind.Enum ? "TryEnum"
        : Parsable(type) ? "TryParse"
        : null;

    private static string Required(string text, string notFound, List<string> statements)
    {
        statements.Add($"if ({text} is null) {{ {notFound} }}");
        return text;
    }

    private static string Default(IParameterSymbol parameter, bool nullable)
    {
        var type = parameter.Type.ToDisplayString(Full);
        if (!parameter.HasExplicitDefaultValue || parameter.ExplicitDefaultValue is null)
        {
            return nullable ? "null" : $"default({type})";
        }

        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(parameter.ExplicitDefaultValue, quoteStrings: true, useHexadecimalNumbers: false);
        return parameter.ExplicitDefaultValue is string ? literal : $"({type})({literal})";
    }

    private static bool Parsable(ITypeSymbol type) =>
        type.AllInterfaces.Any(face => face.OriginalDefinition.ToDisplayString() == "System.IParsable<TSelf>" && SymbolEqualityComparer.Default.Equals(face.TypeArguments[0], type));

    private static string Folder(string file)
    {
        var slash = file.LastIndexOf('/');
        return slash < 0 ? string.Empty : file.Substring(0, slash);
    }

    /// <summary>The route values the folders of a file declare (<c>[id]</c>, <c>[id:int]</c>, <c>[...slug]</c>), by name ignoring case.</summary>
    private static Dictionary<string, string> RouteNames(string folder)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in folder.Split(Slash, StringSplitOptions.RemoveEmptyEntries).Where(segment => segment.Length > 2 && segment[0] == '[' && segment[segment.Length - 1] == ']'))
        {
            var name = segment.Substring(1, segment.Length - 2);
            name = name.StartsWith("...", StringComparison.Ordinal) ? name.Substring(3) : name;
            var colon = name.IndexOf(':');
            name = colon < 0 ? name : name.Substring(0, colon);
            names[name] = name;
        }

        return names;
    }

    private static string Literal(string text) => "@\"" + text.Replace("\"", "\"\"") + "\"";
}
