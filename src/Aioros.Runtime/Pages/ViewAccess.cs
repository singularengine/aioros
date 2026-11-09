using System.Collections.Immutable;
using System.Security.Claims;
using Aioros.Pages;

namespace Aioros.Runtime.Pages;

/// <summary>
/// Decides whether a visitor may see an embedded view, from the <c>[Authorize]</c> rules of its loader. The website
/// registers one that runs ASP.NET Core's authorization; without one, a rule holds for a signed-in user in one of its
/// roles, and a rule naming a policy never holds.
/// </summary>
public interface IViewAccess
{
    ValueTask<bool> AllowsAsync(IServiceProvider services, ClaimsPrincipal user, ImmutableArray<PageAccess> rules, CancellationToken cancellationToken);
}

/// <summary>The rules a view's access follows when the app registers no <see cref="IViewAccess"/>.</summary>
public static class ViewAccess
{
    public static ValueTask<bool> AllowsAsync(IServiceProvider services, ClaimsPrincipal user, ImmutableArray<PageAccess> rules, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(user);
        if (rules.IsDefaultOrEmpty)
        {
            return new ValueTask<bool>(true);
        }

        return services.GetService(typeof(IViewAccess)) is IViewAccess access
            ? access.AllowsAsync(services, user, rules, cancellationToken)
            : new ValueTask<bool>(Holds(user, rules));
    }

    /// <summary>Whether the rules hold without an authorization service: signed in, in one of each rule's roles, and no policy.</summary>
    public static bool Holds(ClaimsPrincipal user, ImmutableArray<PageAccess> rules)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Identity?.IsAuthenticated == true && rules.All(rule => rule.Policy is null && InRole(user, rule.Roles));
    }

    private static bool InRole(ClaimsPrincipal user, string? roles) =>
        roles is null || roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(user.IsInRole);
}
