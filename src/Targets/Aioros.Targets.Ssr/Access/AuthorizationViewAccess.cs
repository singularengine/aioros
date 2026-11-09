using System.Collections.Immutable;
using System.Security.Claims;
using Aioros.Pages;
using Aioros.Runtime.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Access;

/// <summary>
/// Decides whether a visitor may see an embedded view with ASP.NET Core's authorization, as a page's access is decided:
/// the view's <c>[Authorize]</c> rules combined into one policy. Without the authorization services, the defaults of
/// <see cref="ViewAccess"/> apply.
/// </summary>
public sealed class AuthorizationViewAccess : IViewAccess
{
    public async ValueTask<bool> AllowsAsync(IServiceProvider services, ClaimsPrincipal user, ImmutableArray<PageAccess> rules, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(user);
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (services.GetService<IAuthorizationPolicyProvider>() is not { } policies || services.GetService<IAuthorizationService>() is not { } authorization)
        {
            return ViewAccess.Holds(user, rules);
        }

        var policy = await AuthorizationPolicy.CombineAsync(policies, rules.Select(rule => new AuthorizeAttribute { Policy = rule.Policy, Roles = rule.Roles }));
        return policy is null || (await authorization.AuthorizeAsync(user, policy)).Succeeded;
    }
}
