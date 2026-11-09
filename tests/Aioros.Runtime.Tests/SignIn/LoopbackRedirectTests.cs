using System.Net;
using Aioros.Runtime.SignIn;

namespace Aioros.Runtime.Tests.SignIn;

public sealed class LoopbackRedirectTests
{
    [Fact]
    [Trait("AC", "SIGNIN-05")]
    public async Task TheRedirectListensOnALoopbackPortAndChecksTheStateAsync()
    {
        var token = TestContext.Current.CancellationToken;
        using var browser = new HttpClient();
        using var redirect = LoopbackRedirect.Listen();
        using var second = LoopbackRedirect.Listen();

        var received = redirect.ReceiveAsync("s1", token);
        using var other = await browser.GetAsync(new Uri(redirect.RedirectUri, "/favicon.ico"), token);
        using var answer = await browser.GetAsync(new Uri(redirect.RedirectUri + "?code=the%20code&state=s1&return_path=%2Forders%2F42"), token);
        var code = await received;
        var forged = redirect.ReceiveAsync("s1", token);
        using var forgedAnswer = await browser.GetAsync(new Uri(redirect.RedirectUri + "?code=c&state=other"), token);
        var mismatch = await Record.ExceptionAsync(() => forged);
        var refused = redirect.ReceiveAsync("s2", token);
        using var refusedAnswer = await browser.GetAsync(new Uri(redirect.RedirectUri + "?error=access_denied&state=s2"), token);
        var denied = await Record.ExceptionAsync(() => refused);

        Assert.Equal(("http", "127.0.0.1", "/callback"), (redirect.RedirectUri.Scheme, redirect.RedirectUri.Host, redirect.RedirectUri.AbsolutePath));
        Assert.True(redirect.RedirectUri.Port > 0);
        Assert.NotEqual(redirect.RedirectUri.Port, second.RedirectUri.Port);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(("the code", "/orders/42"), (code["code"], code["return_path"]));
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Contains("You can close this window", await answer.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        Assert.IsType<SignInRequiredException>(mismatch);
        Assert.Equal(HttpStatusCode.BadRequest, forgedAnswer.StatusCode);
        Assert.Contains("not started by this app", await forgedAnswer.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        Assert.Contains("access_denied", Assert.IsType<SignInRequiredException>(denied).Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, refusedAnswer.StatusCode);
    }
}
