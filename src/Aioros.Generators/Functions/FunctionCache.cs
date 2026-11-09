using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Functions;

/// <summary>
/// Whether a server function's implementation marks it <c>[Cached(seconds)]</c>, for how long and whether one result
/// serves every user, and what keeps the generated caller from caching it: a result a loader could change
/// (<c>AIOROS023</c>, as for <c>[Memo]</c>), or a shared result whose implementation takes the request (<c>AIOROS024</c>).
/// </summary>
internal sealed class FunctionCache
{
    private const string CachedName = "Aioros.Functions.CachedAttribute";

    private FunctionCache(bool marked, int seconds, bool shared, Location? location, string? mutable, string? request)
    {
        Marked = marked;
        Seconds = seconds;
        Shared = shared;
        Location = location;
        Mutable = mutable;
        Request = request;
    }

    public bool Marked { get; }

    public int Seconds { get; }

    public bool Shared { get; }

    public Location? Location { get; }

    /// <summary>Why a loader could change the kept result, when it could.</summary>
    public string? Mutable { get; }

    /// <summary>The request type a shared function's implementation takes, which makes its result depend on the user.</summary>
    public string? Request { get; }

    /// <summary>Whether the generated caller goes through the data cache: marked, with a result, and not refused.</summary>
    public bool Caches => Marked && Request is null && Mutable is not "it returns no result";

    public static FunctionCache Of(IMethodSymbol method, ITypeSymbol? result, IEnumerable<INamedTypeSymbol> implementations)
    {
        var found = implementations
            .Select(implementation => (Implementation: implementation, Member: implementation.FindImplementationForInterfaceMember(method) as IMethodSymbol))
            .Where(candidate => candidate.Member is not null)
            .Select(candidate => (candidate.Implementation, Attribute: candidate.Member!.GetAttributes().FirstOrDefault(data => data.AttributeClass?.ToDisplayString() == CachedName)))
            .FirstOrDefault(candidate => candidate.Attribute is not null);
        if (found.Attribute is not { } attribute)
        {
            return new FunctionCache(false, 0, false, null, null, null);
        }

        var seconds = attribute.ConstructorArguments.FirstOrDefault().Value as int? ?? 0;
        var shared = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "Shared").Value.Value as bool? ?? false;
        return new FunctionCache(true, seconds, shared, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(), FunctionMemo.Mutable(result), shared ? RequestReads.Taken(found.Implementation) : null);
    }
}

/// <summary>The types through which a class reads the request, and so the user: what a cached result shared by everyone must not take.</summary>
internal static class RequestReads
{
    private static readonly HashSet<string> Types = new(System.StringComparer.Ordinal)
    {
        "Microsoft.AspNetCore.Http.IHttpContextAccessor",
        "Microsoft.AspNetCore.Http.HttpContext",
        "Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider",
        "System.Security.Claims.ClaimsPrincipal",
        "System.Security.Principal.IPrincipal",
        "System.IServiceProvider",
    };

    /// <summary>The first request type a public constructor of <paramref name="type"/> takes, or nothing.</summary>
    public static string? Taken(INamedTypeSymbol type) =>
        type.InstanceConstructors
            .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .SelectMany(constructor => constructor.Parameters)
            .Select(parameter => parameter.Type)
            .FirstOrDefault(Reads)?.ToDisplayString();

    private static bool Reads(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (Types.Contains(current.ToDisplayString()))
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(face => Types.Contains(face.ToDisplayString()));
    }
}
