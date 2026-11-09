using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;

namespace Aioros.Runtime.Tests.SignIn;

public sealed class SignedInHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("AC", "SIGNIN-07")]
    public async Task TokensAreAttachedRefreshedAndRetriedOnceAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock(Start);
        var server = new Server();
        var store = new SessionTokenStore();
        await store.WriteAsync(new TokenSet("a1", "r1", Start.AddMinutes(10)), token);
        var client = new SignInClient(new InstalledAppSignInOptions(), new HttpClient(server) { BaseAddress = Server.Address }, store, clock);
        using var http = new HttpClient(new SignedInHandler(client) { InnerHandler = server }) { BaseAddress = Server.Address };

        using var attached = await http.GetAsync(new Uri("/_data/a", UriKind.Relative), token);
        clock.Now = Start.AddMinutes(9.5);
        using var early = await http.GetAsync(new Uri("/_data/b", UriKind.Relative), token);
        server.Refused.Add("a2");
        using var retried = await http.GetAsync(new Uri("/_data/c", UriKind.Relative), token);
        var stored = await store.ReadAsync(token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (attached.StatusCode, early.StatusCode, retried.StatusCode));
        Assert.Equal(["/_data/a a1", "/_data/b a2", "/_data/c a2", "/_data/c a3"], server.Calls);
        Assert.Equal(["refresh_token r1", "refresh_token r2"], server.Grants);
        Assert.Equal(new TokenSet("a3", "r3", Start.AddMinutes(9.5).AddSeconds(900)), stored);

        server.Calls.Clear();
        server.Grants.Clear();
        clock.Now = Start.AddMinutes(30);
        server.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrent = Enumerable.Range(0, 5).Select(index => http.GetAsync(new Uri($"/_data/{index}", UriKind.Relative), token)).ToList();
        server.Gate.SetResult();
        foreach (var response in await Task.WhenAll(concurrent))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        Assert.Equal(["refresh_token r3"], server.Grants);
        Assert.All(server.Calls, call => Assert.EndsWith(" a4", call, StringComparison.Ordinal));

        server.Refused.Add("a4");
        server.Refused.Add("none");
        server.RefuseGrants = true;
        using var remaining = await http.GetAsync(new Uri("/_data/d", UriKind.Relative), token);
        var data = await Record.ExceptionAsync(() => new RemoteLoaderResults(http).LoadAsync(Page, new PageRequest("/d", ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty), token).AsTask());
        var function = await Record.ExceptionAsync(() => ServerFunctionCalls.CallAsync(http, "g", "f", "c1", 0, static (writer, _) => writer.WriteStartObject(), false, token));

        Assert.Equal(HttpStatusCode.Unauthorized, remaining.StatusCode);
        Assert.IsType<SignInRequiredException>(data);
        Assert.IsType<SignInRequiredException>(function);
        Assert.Null(await store.ReadAsync(token));
    }

    private static readonly PageDefinition Page = new("/d", "Pages/d/Page.axaml", static _ => null, []) { DataContract = "c1" };

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Server : HttpMessageHandler
    {
        public static readonly Uri Address = new("https://app.example.test/");
        private int _issued = 1;

        public ConcurrentQueue<string> Calls { get; } = new();

        public ConcurrentQueue<string> Grants { get; } = new();

        public HashSet<string> Refused { get; } = new(StringComparer.Ordinal);

        public bool RefuseGrants { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/connect/token")
            {
                return await GrantAsync(request, cancellationToken);
            }

            var bearer = request.Headers.Authorization?.Parameter ?? "none";
            Calls.Enqueue(request.RequestUri.AbsolutePath + " " + bearer);
            return new HttpResponseMessage(Refused.Contains(bearer) ? HttpStatusCode.Unauthorized : HttpStatusCode.OK) { Content = new StringContent("{}") };
        }

        private async Task<HttpResponseMessage> GrantAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&').Select(static pair => pair.Split('=')).ToDictionary(static pair => pair[0], static pair => Uri.UnescapeDataString(pair[1]));
            Grants.Enqueue(form["grant_type"] + " " + form["refresh_token"]);
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            if (RefuseGrants)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") };
            }

            _issued++;
            var body = JsonSerializer.Serialize(new Dictionary<string, object> { ["access_token"] = $"a{_issued}", ["refresh_token"] = $"r{_issued}", ["expires_in"] = 900, ["token_type"] = "Bearer" });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
