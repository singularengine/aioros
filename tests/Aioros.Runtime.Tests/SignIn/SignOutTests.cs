using System.Net;
using Aioros.Runtime.SignIn;

namespace Aioros.Runtime.Tests.SignIn;

public sealed class SignOutTests
{
    private static readonly Uri Address = new("https://app.example.test/");

    [Fact]
    [Trait("AC", "SIGNIN-14")]
    public async Task SigningOutRevokesTheRefreshTokenAndClearsTheStoreAsync()
    {
        var revoked = await SignOutAsync(HttpStatusCode.OK, stored: true);
        var refused = await SignOutAsync(HttpStatusCode.InternalServerError, stored: true);
        var unreachable = await SignOutAsync(null, stored: true);
        var empty = await SignOutAsync(HttpStatusCode.OK, stored: false);

        Assert.Equal((true, true, true), (revoked.Answer, revoked.Cleared, revoked.Signed));
        Assert.Equal(["POST /connect/revoke token=r1&token_type_hint=refresh_token&client_id=aioros-installed-app"], revoked.Posts);
        Assert.Equal((false, true, true), (refused.Answer, refused.Cleared, refused.Signed));
        Assert.Single(refused.Posts);
        Assert.Equal((false, true, true), (unreachable.Answer, unreachable.Cleared, unreachable.Signed));
        Assert.Equal((false, true, true), (empty.Answer, empty.Cleared, empty.Signed));
        Assert.Empty(empty.Posts);
    }

    private static async Task<(bool Answer, bool Cleared, bool Signed, List<string> Posts)> SignOutAsync(HttpStatusCode? status, bool stored)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new SessionTokenStore();
        if (stored)
        {
            await store.WriteAsync(new TokenSet("a1", "r1", DateTimeOffset.UtcNow.AddMinutes(10)), token);
        }

        var server = new Server(status);
        var client = new SignInClient(new InstalledAppSignInOptions(), new HttpClient(server) { BaseAddress = Address }, store, TimeProvider.System);
        var answer = await client.SignOutAsync(token);
        return (answer, await store.ReadAsync(token) is null, await client.AccessTokenAsync(token) is null, server.Posts);
    }

    private sealed class Server(HttpStatusCode? status) : HttpMessageHandler
    {
        public List<string> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Posts.Add($"{request.Method} {request.RequestUri!.AbsolutePath} {await request.Content!.ReadAsStringAsync(cancellationToken)}");
            return status is { } answer ? new HttpResponseMessage(answer) : throw new HttpRequestException("The server cannot be reached.");
        }
    }
}
