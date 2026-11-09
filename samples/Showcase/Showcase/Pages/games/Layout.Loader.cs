using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Showcase.Games;

namespace Showcase.Pages.Games;

/// <summary>The store's layout loader. Its <c>[Authorize]</c> protects every page under <c>/games</c>.</summary>
[Authorize]
public static class StoreShellLoader
{
    public static StoreUser Load(ClaimsPrincipal user) => new(user.Identity?.Name ?? "guest");
}
