using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Ssr.Data;
using Aioros.Targets.Ssr.Functions;
using Aioros.Tests.SignIn;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.SignIn;

public sealed class InstalledAppAuthorizationTests
{
    private const string Contract = "c1";

    [Fact]
    [Trait("AC", "SIGNIN-01")]
    public async Task CodesAreExchangedOnlyWithTheirVerifierAsync()
    {
        await using var server = await AuthorizationServer.StartAsync(accessTokenLifetime: TimeSpan.FromMinutes(5));
        var cookie = await server.CookieAsync();
        var verifier = Pkce.Verifier();

        var redirect = await AuthorizeAsync(server, cookie, Pkce.Challenge(verifier), "http://127.0.0.1:49152/callback");
        var query = QueryHelpers.ParseQuery(redirect.Query);
        var issued = await TokenAsync(server, ("grant_type", "authorization_code"), ("code", query["code"].ToString()), ("redirect_uri", "http://127.0.0.1:49152/callback"), ("client_id", server.Options.ClientId), ("code_verifier", verifier));
        var other = await AuthorizeAsync(server, cookie, Pkce.Challenge(verifier), "http://127.0.0.1:49152/callback");
        var wrong = await TokenAsync(server, ("grant_type", "authorization_code"), ("code", QueryHelpers.ParseQuery(other.Query)["code"].ToString()), ("redirect_uri", "http://127.0.0.1:49152/callback"), ("client_id", server.Options.ClientId), ("code_verifier", Pkce.Verifier()));

        Assert.Equal("http://127.0.0.1:49152/callback", redirect.GetLeftPart(UriPartial.Path));
        Assert.Equal("the-state", query["state"]);
        Assert.Equal(HttpStatusCode.OK, issued.Status);
        Assert.Equal("Bearer", issued.Json.GetProperty("token_type").GetString());
        Assert.InRange(issued.Json.GetProperty("expires_in").GetInt32(), 298, 300);
        Assert.False(string.IsNullOrEmpty(issued.Json.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(issued.Json.GetProperty("refresh_token").GetString()));
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant", false), (wrong.Status, wrong.Json.GetProperty("error").GetString(), wrong.Json.TryGetProperty("access_token", out _)));
    }

    [Fact]
    [Trait("AC", "SIGNIN-02")]
    public async Task OnlyLoopbackRedirectsWithAChallengeGetACodeAsync()
    {
        await using var server = await AuthorizationServer.StartAsync();
        var cookie = await server.CookieAsync();
        var challenge = Pkce.Challenge(Pkce.Verifier());

        var ports = new List<Uri>();
        foreach (var port in new[] { 50001, 61234 })
        {
            ports.Add(await AuthorizeAsync(server, cookie, challenge, $"http://127.0.0.1:{port}/callback"));
        }

        using var host = await server.BrowseAsync(AuthorizeRequest(server, challenge, "http://evil.example:50001/callback"), cookie);
        using var path = await server.BrowseAsync(AuthorizeRequest(server, challenge, "http://127.0.0.1:50001/elsewhere"), cookie);
        using var bare = await server.BrowseAsync(AuthorizeRequest(server, null, "http://127.0.0.1:50001/callback"), cookie);
        using var anonymous = await server.BrowseAsync(AuthorizeRequest(server, challenge, "http://127.0.0.1:50001/callback"), null);

        Assert.All(ports, redirect => Assert.True(QueryHelpers.ParseQuery(redirect.Query).ContainsKey("code")));
        Assert.Equal([50001, 61234], ports.Select(static redirect => redirect.Port));
        Assert.All([host, path], refused => Assert.NotEqual("127.0.0.1", refused.Headers.Location?.Host ?? string.Empty));
        Assert.All([host, path], refused => Assert.DoesNotContain("evil.example", refused.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal));
        Assert.False(QueryHelpers.ParseQuery(bare.Headers.Location?.Query ?? string.Empty).ContainsKey("code"));
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/login", anonymous.Headers.Location!.AbsolutePath);
        Assert.StartsWith("/connect/authorize?", QueryHelpers.ParseQuery(anonymous.Headers.Location.Query)["ReturnUrl"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "SIGNIN-03")]
    public async Task RefreshTokensRotateAndAReusedOneIsRefusedAsync()
    {
        await using var server = await AuthorizationServer.StartAsync();
        var first = await SignInAsync(server);

        var second = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", first.Refresh), ("client_id", server.Options.ClientId));
        var reused = await TokenAsync(server, ("grant_type", "refresh_token"), ("refresh_token", first.Refresh), ("client_id", server.Options.ClientId));

        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.NotEqual(first.Refresh, second.Json.GetProperty("refresh_token").GetString());
        Assert.NotEqual(first.Access, second.Json.GetProperty("access_token").GetString());
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (reused.Status, reused.Json.GetProperty("error").GetString()));
    }

    [Fact]
    [Trait("AC", "SIGNIN-04")]
    public async Task BearerTokensAreAcceptedOnDataAndFunctionsAlongsideTheCookieAsync()
    {
        await using var server = await AuthorizationServer.StartAsync(
            services => services.AddHttpContextAccessor().AddScoped<IWho, Who>(),
            app =>
            {
                app.MapPageData([Protected]);
                app.MapServerFunctions([WhoGroup]);
            });
        var tokens = await SignInAsync(server);
        var cookie = await server.CookieAsync();

        var data = await CallAsync(server, Data, "Bearer " + tokens.Access, null);
        var function = await CallAsync(server, Function, "Bearer " + tokens.Access, null);
        var anonymous = (await CallAsync(server, Data, null, null), await CallAsync(server, Function, null, null));
        var forged = (await CallAsync(server, Data, "Bearer forged", null), await CallAsync(server, Function, "Bearer forged", null));
        var cookies = (await CallAsync(server, Data, null, cookie), await CallAsync(server, Function, null, cookie));

        Assert.Equal((HttpStatusCode.OK, $"{AuthorizationServer.UserId}/{AuthorizationServer.UserName}"), (data.Status, Segment(data.Body)));
        Assert.Equal((HttpStatusCode.OK, $"\"{AuthorizationServer.UserId}/{AuthorizationServer.UserName}\""), (function.Status, function.Body));
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (anonymous.Item1.Status, anonymous.Item2.Status));
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (forged.Item1.Status, forged.Item2.Status));
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (cookies.Item1.Status, cookies.Item2.Status));
    }

    [Fact]
    [Trait("AC", "SIGNIN-06")]
    public async Task TheClientSignsInThroughTheBrowserAndStoresTheTokensAsync()
    {
        await using var server = await AuthorizationServer.StartAsync();
        var cookie = await server.CookieAsync();
        var store = new SessionTokenStore();
        Uri? opened = null;
        var browser = server.Browser(cookie);
        var client = new SignInClient(server.Options, server.Client(), store, TimeProvider.System);

        await client.SignInAsync(address => { opened = address; return browser(address); }, TestContext.Current.CancellationToken);

        var query = QueryHelpers.ParseQuery(opened!.Query);
        var stored = await store.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("/connect/authorize", opened.AbsolutePath);
        Assert.Equal(("code", server.Options.ClientId, "S256"), (query["response_type"].ToString(), query["client_id"].ToString(), query["code_challenge_method"].ToString()));
        Assert.Equal(43, query["code_challenge"].ToString().Length);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.Matches("^http://127\\.0\\.0\\.1:[0-9]+/callback$", query["redirect_uri"].ToString());
        Assert.Equal($"{Environment.MachineName} ({(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux")})", query["device_name"].ToString());
        Assert.False(query.ContainsKey("return_path"));
        Assert.NotNull(stored);
        Assert.False(string.IsNullOrEmpty(stored.AccessToken));
        Assert.False(string.IsNullOrEmpty(stored.RefreshToken));
        Assert.InRange(stored.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        Assert.Equal(stored.AccessToken, await client.AccessTokenAsync(TestContext.Current.CancellationToken));
    }

    internal static async Task<(string Access, string Refresh)> SignInAsync(AuthorizationServer server, string? user = null, string? device = null)
    {
        var cookie = await server.CookieAsync(user);
        var verifier = Pkce.Verifier();
        var redirect = await AuthorizeAsync(server, cookie, Pkce.Challenge(verifier), "http://127.0.0.1:50000/callback", device is null ? null : new() { ["device_name"] = device });
        var issued = await TokenAsync(server, ("grant_type", "authorization_code"), ("code", QueryHelpers.ParseQuery(redirect.Query)["code"].ToString()), ("redirect_uri", "http://127.0.0.1:50000/callback"), ("client_id", server.Options.ClientId), ("code_verifier", verifier));
        return (issued.Json.GetProperty("access_token").GetString()!, issued.Json.GetProperty("refresh_token").GetString()!);
    }

    internal static Uri AuthorizeRequest(AuthorizationServer server, string? challenge, string redirect, Dictionary<string, string?>? extra = null)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = server.Options.ClientId,
            ["redirect_uri"] = redirect,
            ["state"] = "the-state",
        };
        foreach (var (name, value) in extra ?? [])
        {
            parameters[name] = value;
        }

        if (challenge is not null)
        {
            parameters["code_challenge"] = challenge;
            parameters["code_challenge_method"] = "S256";
        }

        return new Uri(QueryHelpers.AddQueryString("/connect/authorize", parameters), UriKind.Relative);
    }

    internal static async Task<Uri> AuthorizeAsync(AuthorizationServer server, string cookie, string challenge, string redirect, Dictionary<string, string?>? extra = null)
    {
        using var response = await server.BrowseAsync(AuthorizeRequest(server, challenge, redirect, extra), cookie);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!;
    }

    internal static async Task<(HttpStatusCode Status, JsonElement Json)> TokenAsync(AuthorizationServer server, params (string Name, string Value)[] form)
    {
        using var client = server.Client();
        using var content = new FormUrlEncodedContent(form.Select(static pair => KeyValuePair.Create(pair.Name, pair.Value)));
        using var response = await client.PostAsync(new Uri("/connect/token", UriKind.Relative), content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (response.StatusCode, document.RootElement.Clone());
    }

    internal static readonly Func<HttpRequestMessage> Data = () => new HttpRequestMessage(HttpMethod.Get, new Uri("/_data/mine", UriKind.Relative)) { Headers = { { ServerFunctionGroup.ContractHeader, Contract } } };

    private static readonly Func<HttpRequestMessage> Function = () => new HttpRequestMessage(HttpMethod.Post, new Uri("/_sf/who/name", UriKind.Relative))
    {
        Content = new StringContent("{}", new MediaTypeHeaderValue("application/json")),
        Headers = { { ServerFunctionGroup.FunctionHeader, "1" }, { ServerFunctionGroup.ContractHeader, Contract } },
    };

    internal static async Task<(HttpStatusCode Status, string Body)> CallAsync(AuthorizationServer server, Func<HttpRequestMessage> create, string? authorization, string? cookie)
    {
        using var client = server.Client();
        using var request = create();
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string? Segment(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("segments")[0].GetProperty("data").GetString();
    }

    private static string UserOf(ClaimsPrincipal user) => $"{user.FindFirstValue(ClaimTypes.NameIdentifier)}/{user.Identity?.Name}";

    internal static readonly PageDefinition Protected = new("/mine", "Pages/mine/Page.axaml", static _ => null, [])
    {
        Access = [new PageAccess(null, null)],
        DataContract = Contract,
        Loader = new LoaderDefinition(static (_, request, _) => ValueTask.FromResult(new LoaderOutcome(UserOf(request.User))), static (_, _) => null)
        {
            Write = static (writer, data) => writer.WriteStringValue((string?)data),
            Read = static element => element.GetString(),
        },
    };

    private static readonly ServerFunctionGroup WhoGroup = new(
        typeof(IWho),
        "who",
        Contract,
        [new ServerFunctionRoute("name", static (service, _, _) => Task.FromResult<object?>(((IWho)service).Name())) { Access = [new ServerFunctionAccess(null, null)], Write = static (writer, value) => writer.WriteStringValue((string?)value) }],
        static _ => throw new InvalidOperationException("No client in this test."));

    public interface IWho
    {
        string Name();
    }

    private sealed class Who(IHttpContextAccessor http) : IWho
    {
        public string Name() => UserOf(http.HttpContext!.User);
    }
}
