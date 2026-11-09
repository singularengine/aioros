using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Access;

/// <summary>
/// Checks <c>[Authorize]</c> rules inside an endpoint that answers an app rather than a browser, so a refusal is a status
/// code and never a redirect to the sign-in page: <c>401</c> anonymous, <c>403</c> when the policy combined from the rules
/// fails, nothing when they hold or there are none.
/// </summary>
public static class AccessRules
{
    public static async Task<int?> RefusedAsync(HttpContext http, IReadOnlyCollection<(string? Policy, string? Roles)> rules)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Count == 0)
        {
            return null;
        }

        if (http.User.Identity?.IsAuthenticated != true)
        {
            return StatusCodes.Status401Unauthorized;
        }

        var policy = await AuthorizationPolicy.CombineAsync(
            http.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>(),
            rules.Select(rule => new AuthorizeAttribute { Policy = rule.Policy, Roles = rule.Roles }));
        var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
        return policy is null || (await authorization.AuthorizeAsync(http.User, policy)).Succeeded ? null : StatusCodes.Status403Forbidden;
    }
}
