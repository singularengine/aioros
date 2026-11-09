using System.Net;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Ssr.Data;
using Aioros.Tests.SignIn;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using static Aioros.Targets.Ssr.Tests.SignIn.InstalledAppAuthorizationTests;

namespace Aioros.Targets.Ssr.Tests.SignIn;

public sealed class TokenKeysTests
{
    [Fact]
    [Trait("AC", "SIGNIN-27")]
    public async Task TheServerNeedsNoRsaKeyAndIssuesOnlyDataProtectionTokensAsync()
    {
        await using var server = await AuthorizationServer.StartAsync();
        var options = server.App.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var cookie = await server.CookieAsync();
        var verifier = Pkce.Verifier();
        var redirect = await AuthorizeAsync(server, cookie, Pkce.Challenge(verifier), "http://127.0.0.1:50000/callback");
        var code = QueryHelpers.ParseQuery(redirect.Query)["code"].ToString();
        var issued = await TokenAsync(server, ("grant_type", "authorization_code"), ("code", code), ("redirect_uri", "http://127.0.0.1:50000/callback"), ("client_id", server.Options.ClientId), ("code_verifier", verifier));
        var jwt = new JsonWebTokenHandler();

        var signing = Assert.Single(options.SigningCredentials);
        var encryption = Assert.Single(options.EncryptionCredentials);
        Assert.Equal((typeof(ECDsaSecurityKey), SecurityAlgorithms.EcdsaSha256), (signing.Key.GetType(), signing.Algorithm));
        Assert.Equal((typeof(SymmetricSecurityKey), SecurityAlgorithms.Aes256KW), (encryption.Key.GetType(), encryption.Alg));
        Assert.Equal(HttpStatusCode.OK, issued.Status);
        Assert.All([code, issued.Json.GetProperty("access_token").GetString(), issued.Json.GetProperty("refresh_token").GetString()], token => Assert.False(jwt.CanReadToken(token)));
    }

    [Fact]
    [Trait("AC", "SIGNIN-28")]
    public async Task TokensOutliveARestartWithTheSameKeyRingAsync()
    {
        var database = $"Data Source=restart-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var ring = new EphemeralDataProtectionProvider();
        var first = await AuthorizationServer.StartAsync(services => services.AddSingleton<IDataProtectionProvider>(ring), database: database);
        var tokens = await SignInAsync(first);
        await using var restarted = await AuthorizationServer.StartAsync(services => services.AddSingleton<IDataProtectionProvider>(ring), static app => app.MapPageData([Protected]), database: database);
        await using var other = await AuthorizationServer.StartAsync(static services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()), static app => app.MapPageData([Protected]), database: database);
        await first.DisposeAsync();

        var refused = await CallAsync(other, InstalledAppAuthorizationTests.Data, "Bearer " + tokens.Access, null);
        var foreign = await TokenAsync(other, ("grant_type", "refresh_token"), ("refresh_token", tokens.Refresh), ("client_id", other.Options.ClientId));
        var accepted = await CallAsync(restarted, InstalledAppAuthorizationTests.Data, "Bearer " + tokens.Access, null);
        var refreshed = await TokenAsync(restarted, ("grant_type", "refresh_token"), ("refresh_token", tokens.Refresh), ("client_id", restarted.Options.ClientId));

        Assert.Equal(HttpStatusCode.Unauthorized, refused.Status);
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (foreign.Status, foreign.Json.GetProperty("error").GetString()));
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(HttpStatusCode.OK, refreshed.Status);
        Assert.NotEqual(tokens.Refresh, refreshed.Json.GetProperty("refresh_token").GetString());
    }
}
