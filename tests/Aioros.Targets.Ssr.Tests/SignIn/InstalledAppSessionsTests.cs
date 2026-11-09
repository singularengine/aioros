using System.Net;
using System.Security.Claims;
using Aioros.Targets.Ssr.Data;
using Aioros.Targets.Ssr.SignIn;
using Aioros.Tests.SignIn;
using Microsoft.Extensions.DependencyInjection;
using static Aioros.Targets.Ssr.Tests.SignIn.InstalledAppAuthorizationTests;

namespace Aioros.Targets.Ssr.Tests.SignIn;

public sealed class InstalledAppSessionsTests
{
    private static readonly ClaimsPrincipal Ana = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, AuthorizationServer.UserId)], "test"));

    [Fact]
    [Trait("AC", "SIGNIN-13")]
    public async Task ARevokedRefreshTokenEndsItsSessionAsync()
    {
        await using var server = await AuthorizationServer.StartAsync(map: static app => app.MapPageData([Protected]));
        var tokens = await SignInAsync(server);

        var before = await CallAsync(server, InstalledAppAuthorizationTests.Data, "Bearer " + tokens.Access, null);
        var revoked = await RevokeAsync(server, tokens.Refresh);
        var refresh = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", tokens.Refresh), ("client_id", server.Options.ClientId));
        var after = await CallAsync(server, InstalledAppAuthorizationTests.Data, "Bearer " + tokens.Access, null);
        var unknown = await RevokeAsync(server, "not-a-token");

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(HttpStatusCode.OK, revoked);
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (refresh.Status, refresh.Json.GetProperty("error").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, after.Status);
        Assert.Equal(HttpStatusCode.OK, unknown);
    }

    [Fact]
    [Trait("AC", "SIGNIN-15")]
    public async Task TheUsersSessionsAreListedAndRevokedOneByOneAsync()
    {
        await using var server = await AuthorizationServer.StartAsync(map: static app => app.MapPageData([Protected]));
        var laptop = await SignInAsync(server, device: "ana-laptop (Linux)");
        var desk = await SignInAsync(server, device: "ana-desk (Windows)");
        var other = await SignInAsync(server, user: AuthorizationServer.OtherUserName, device: "bo-laptop (macOS)");
        _ = await SignInAsync(server, user: AuthorizationServer.OtherUserName, device: "x\u0007" + new string('y', 80));
        var bo = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, AuthorizationServer.OtherUserId)], "test"));

        var listed = await SessionsAsync(server, sessions => sessions.ListAsync(Ana, TestContext.Current.CancellationToken));
        var others = await SessionsAsync(server, sessions => sessions.ListAsync(bo, TestContext.Current.CancellationToken));
        var foreign = await SessionsAsync(server, sessions => sessions.RevokeAsync(Ana, others[0].Id, TestContext.Current.CancellationToken));
        var unknown = await SessionsAsync(server, sessions => sessions.RevokeAsync(Ana, "unknown", TestContext.Current.CancellationToken));
        var revoked = await SessionsAsync(server, sessions => sessions.RevokeAsync(Ana, listed[1].Id, TestContext.Current.CancellationToken));
        var left = await SessionsAsync(server, sessions => sessions.ListAsync(Ana, TestContext.Current.CancellationToken));
        var laptopRefresh = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", laptop.Refresh), ("client_id", server.Options.ClientId));
        var laptopData = await CallAsync(server, InstalledAppAuthorizationTests.Data, "Bearer " + laptop.Access, null);
        var deskRefresh = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", desk.Refresh), ("client_id", server.Options.ClientId));
        var otherRefresh = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", other.Refresh), ("client_id", server.Options.ClientId));

        Assert.Equal(["ana-desk (Windows)", "ana-laptop (Linux)"], listed.Select(static session => session.Device));
        Assert.All(listed, session => Assert.InRange(session.Created, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow));
        Assert.All(listed, session => Assert.True(session.LastUsed >= session.Created));
        Assert.True(listed[0].Created >= listed[1].Created);
        Assert.Equal(["x" + new string('y', 63), "bo-laptop (macOS)"], others.Select(static session => session.Device));
        Assert.False(foreign);
        Assert.False(unknown);
        Assert.True(revoked);
        Assert.Equal(["ana-desk (Windows)"], left.Select(static session => session.Device));
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (laptopRefresh.Status, laptopRefresh.Json.GetProperty("error").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, laptopData.Status);
        Assert.Equal(HttpStatusCode.OK, deskRefresh.Status);
        Assert.Equal(HttpStatusCode.OK, otherRefresh.Status);
    }

    [Fact]
    [Trait("AC", "SIGNIN-16")]
    public async Task SigningOutOnTheWebRevokesInstalledAppsOnlyWhenAskedAsync()
    {
        await using var keeping = await AuthorizationServer.StartAsync();
        await using var revoking = await AuthorizationServer.StartAsync(configure: static options => options with { RevokeOnWebSignOut = true });

        var kept = await SignOutOnTheWebAsync(keeping);
        var ended = await SignOutOnTheWebAsync(revoking);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), kept);
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.OK), ended);
    }

    private static async Task<(HttpStatusCode Mine, HttpStatusCode Other)> SignOutOnTheWebAsync(AuthorizationServer server)
    {
        var mine = await SignInAsync(server);
        var other = await SignInAsync(server, user: AuthorizationServer.OtherUserName);
        using (var signedOut = await server.BrowseAsync(new Uri("/test/signout", UriKind.Relative), await server.CookieAsync()))
        {
            Assert.Equal(HttpStatusCode.OK, signedOut.StatusCode);
        }

        var refreshed = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", mine.Refresh), ("client_id", server.Options.ClientId));
        var others = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", other.Refresh), ("client_id", server.Options.ClientId));
        return (refreshed.Status, others.Status);
    }

    private static async Task<HttpStatusCode> RevokeAsync(AuthorizationServer server, string token)
    {
        using var client = server.Client();
        using var content = new FormUrlEncodedContent([KeyValuePair.Create("token", token), KeyValuePair.Create("token_type_hint", "refresh_token"), KeyValuePair.Create("client_id", server.Options.ClientId)]);
        using var response = await client.PostAsync(new Uri("/connect/revoke", UriKind.Relative), content, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<T> SessionsAsync<T>(AuthorizationServer server, Func<InstalledAppSessions, Task<T>> use)
    {
        await using var scope = server.App.Services.CreateAsyncScope();
        return await use(scope.ServiceProvider.GetRequiredService<InstalledAppSessions>());
    }
}
