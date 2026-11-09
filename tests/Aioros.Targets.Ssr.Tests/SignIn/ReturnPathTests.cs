using System.Net;
using Aioros.Routing.Tables;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Ssr.SignIn;
using Aioros.Tests.SignIn;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using static Aioros.Targets.Ssr.Tests.SignIn.InstalledAppAuthorizationTests;

namespace Aioros.Targets.Ssr.Tests.SignIn;

public sealed class ReturnPathTests
{
    private const string Loopback = "http://127.0.0.1:50000/callback";

    private static readonly RouteTable Routes = RouteTable.Empty.Add("/orders/{id:int}").Add("/download");

    private static bool IsPage(string path) => Routes.Match(path).IsMatch;

    [Fact]
    [Trait("AC", "INSTALL-15")]
    public async Task TheDownloadLinkKeepsTheReturnPathInAShortLivedCookieAsync()
    {
        await using var server = await StartAsync();

        var kept = await SetCookieAsync(server, "/download?return=%2Forders%2F42%3Fq%3Dlate");
        var refused = new List<string?>();
        foreach (var path in new[] { "//evil.example", "https://evil.example/", "/admin", "/orders/abc" })
        {
            refused.Add(await SetCookieAsync(server, "/download?return=" + Uri.EscapeDataString(path)));
        }

        var elsewhere = await SetCookieAsync(server, "/orders/42?return=%2Forders%2F42");

        Assert.NotNull(kept);
        var parts = kept.Split("; ");
        Assert.Equal(".Aioros.Return=" + Uri.EscapeDataString("/orders/42?q=late"), parts[0]);
        Assert.Contains("max-age=3600", parts);
        Assert.Contains("path=/connect/authorize", parts);
        Assert.Contains("samesite=lax", parts);
        Assert.Contains("httponly", parts);
        Assert.All(refused, Assert.Null);
        Assert.Null(elsewhere);
    }

    [Fact]
    [Trait("AC", "INSTALL-21")]
    public async Task TheReturnPathCookieIsSecureOnlyOverHttpsAsync()
    {
        await using var server = await StartAsync();
        using var secure = server.App.GetTestServer().CreateClient();
        secure.BaseAddress = new Uri("https://localhost/");

        using var answer = await secure.GetAsync(new Uri("/download?return=%2Forders%2F42", UriKind.Relative), TestContext.Current.CancellationToken);
        var plain = await SetCookieAsync(server, "/download?return=%2Forders%2F42");

        Assert.Contains("secure", answer.Headers.GetValues("Set-Cookie").Single().Split("; "));
        Assert.DoesNotContain("secure", plain!.Split("; "));
    }

    [Fact]
    [Trait("AC", "INSTALL-22")]
    public async Task TheReturnPathCookieIsSecureBehindATlsProxyAsync()
    {
        await using var server = await StartAsync();
        using var client = server.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/download?return=%2Forders%2F42", UriKind.Relative));
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var answer = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Contains("secure", answer.Headers.GetValues("Set-Cookie").Single().Split("; "));
    }

    [Fact]
    [Trait("AC", "INSTALL-16")]
    public async Task TheAuthorizationResponseCarriesTheReturnPathWhenAskedAsync()
    {
        await using var server = await StartAsync();
        var cookie = await server.CookieAsync();
        var challenge = Pkce.Challenge(Pkce.Verifier());
        var holding = cookie + "; .Aioros.Return=" + Uri.EscapeDataString("/orders/42");
        var ask = new Dictionary<string, string?> { ["return_path"] = "request" };

        using var asked = await server.BrowseAsync(AuthorizeRequest(server, challenge, Loopback, ask), holding);
        using var notAsked = await server.BrowseAsync(AuthorizeRequest(server, challenge, Loopback), holding);
        using var unknown = await server.BrowseAsync(AuthorizeRequest(server, challenge, Loopback, ask), cookie + "; .Aioros.Return=" + Uri.EscapeDataString("/admin"));

        var answer = QueryHelpers.ParseQuery(asked.Headers.Location!.Query);
        Assert.Equal(HttpStatusCode.Redirect, asked.StatusCode);
        Assert.True(answer.ContainsKey("code"));
        Assert.Equal("/orders/42", answer["return_path"]);
        Assert.Contains(asked.Headers.GetValues("Set-Cookie"), static set => set.StartsWith(".Aioros.Return=;", StringComparison.Ordinal) && set.Contains("expires=Thu, 01 Jan 1970", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Redirect, notAsked.StatusCode);
        Assert.False(QueryHelpers.ParseQuery(notAsked.Headers.Location!.Query).ContainsKey("return_path"));
        Assert.False(notAsked.Headers.TryGetValues("Set-Cookie", out _));
        Assert.True(QueryHelpers.ParseQuery(unknown.Headers.Location!.Query).ContainsKey("code"));
        Assert.False(QueryHelpers.ParseQuery(unknown.Headers.Location.Query).ContainsKey("return_path"));
    }

    private static Task<AuthorizationServer> StartAsync() => AuthorizationServer.StartAsync(
        map: static app =>
        {
            app.UseInstalledAppReturnPath("/download", IsPage);
            app.MapGet("/download", static () => "Download");
            app.MapGet("/orders/{id:int}", static (int id) => "Order " + id);
        },
        pages: IsPage);

    private static async Task<string?> SetCookieAsync(AuthorizationServer server, string path)
    {
        using var client = server.Client();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Single() : null;
    }
}
