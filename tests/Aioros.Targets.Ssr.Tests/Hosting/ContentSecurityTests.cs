using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Hosting;

public sealed class ContentSecurityTests
{
    [Fact]
    [Trait("AC", "SSR-273")]
    public async Task HtmlDocumentsCarryAContentSecurityPolicyAsync()
    {
        var production = await PoliciesAsync(new SsrOptions());
        var replaced = await PoliciesAsync(new SsrOptions { ContentSecurityPolicy = "default-src 'none'" });
        var off = await PoliciesAsync(new SsrOptions { ContentSecurityPolicy = "" });
        var development = await PoliciesAsync(new SsrOptions { Development = true });

        Assert.Equal((ContentSecurity.DefaultPolicy, null, "frame-ancestors 'none'"), production);
        Assert.Equal(("default-src 'none'", null, "frame-ancestors 'none'"), replaced);
        Assert.Equal((null, null, "frame-ancestors 'none'"), off);
        Assert.Equal((null, null, "frame-ancestors 'none'"), development);
    }

    private static async Task<(string? Page, string? Data, string? Own)> PoliciesAsync(SsrOptions options)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(options).AddContentSecurity();
        await using var app = builder.Build();
        app.MapGet("/page", () => Results.Content("<!doctype html>", "text/html"));
        app.MapGet("/data", () => Results.Json(new { }));
        app.MapGet("/own", (HttpContext http) =>
        {
            http.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            return Results.Content("<!doctype html>", "text/html");
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();
        async Task<string?> PolicyAsync(string path)
        {
            using var answer = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            return answer.Headers.TryGetValues("Content-Security-Policy", out var values) ? values.Single() : null;
        }

        return (await PolicyAsync("/page"), await PolicyAsync("/data"), await PolicyAsync("/own"));
    }
}
