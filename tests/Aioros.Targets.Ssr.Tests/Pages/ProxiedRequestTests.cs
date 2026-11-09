using System.Net;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class ProxiedRequestTests
{
    [Fact]
    [Trait("AC", "SSR-184")]
    public async Task AProxysForwardedSchemeKeepsTheThemeCookieSecureAsync()
    {
        await using var trusted = await StartAsync(trustsLoopback: true);
        await using var untrusted = await StartAsync(trustsLoopback: false);

        var fromTrusted = await SwitchAsync(trusted, forwarded: true);
        var fromUntrusted = await SwitchAsync(untrusted, forwarded: true);
        var plain = await SwitchAsync(trusted, forwarded: false);

        Assert.Equal(("https", true), fromTrusted);
        Assert.Equal(("http", true), fromUntrusted);
        Assert.Equal(("http", false), plain);
    }

    private static async Task<(string Scheme, bool Secure)> SwitchAsync(WebApplication app, bool forwarded)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/theme/dark", UriKind.Relative));
        if (forwarded)
        {
            request.Headers.Add("X-Forwarded-Proto", "https");
        }

        using var answer = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (answer.Headers.GetValues("X-Scheme").Single(), answer.Headers.GetValues("Set-Cookie").Single().Split("; ").Contains("secure"));
    }

    private static async Task<WebApplication> StartAsync(bool trustsLoopback)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddProxiedRequests();
        if (!trustsLoopback)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(static options =>
            {
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Add(IPAddress.Parse("10.9.9.9"));
            });
        }

        var app = builder.Build();
        app.MapGet("/theme/{name}", static (HttpContext http, string name) =>
        {
            http.Response.Headers["X-Scheme"] = http.Request.Scheme;
            return ThemeSwitch.Switch(http, name);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
