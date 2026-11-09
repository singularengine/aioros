using System.IO.Compression;
using LumenBlazor.Data;
using Microsoft.AspNetCore.ResponseCompression;

namespace LumenBlazor;

public static class Hosting
{
    /// Production-style response compression (Next.js compresses by default too), Brotli and gzip at the same level as
    /// Aioros. https://learn.microsoft.com/aspnet/core/performance/response-compression
    public static IServiceCollection AddLumenCompression(this IServiceCollection services) => services
        .AddResponseCompression(o =>
        {
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
        })
        .Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal)
        .Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);

    public static void MapLumenEndpoints(this WebApplication app)
    {
        app.MapGet("/theme/{name}", (HttpContext http, string name) =>
        {
            var current = http.Request.Cookies["theme"] == "dark" ? "dark" : "light";
            var next = name == "toggle" ? (current == "dark" ? "light" : "dark") : name == "dark" ? "dark" : "light";
            http.Response.Cookies.Append("theme", next, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
            var back = http.Request.Headers.Referer.ToString();
            return Results.Redirect(Uri.TryCreate(back, UriKind.Absolute, out var u) ? u.PathAndQuery : "/");
        });
        app.MapGet("/_stats", () => Results.Text(
            $"{{\"allocatedBytes\":{GC.GetTotalAllocatedBytes(true)},\"workingSet\":{Environment.WorkingSet}}}", "application/json"));
        app.MapFixtures();
    }

    /// Fixtures for the Next.js version, so all three apps show the same data.
    private static void MapFixtures(this WebApplication app) => app.MapGet("/_fixtures.json", (Store s) => Results.Json(new
    {
        statuses = Store.Statuses.Values, priorities = Store.Priorities, people = Store.People,
        issues = s.Issues.Select(i => new { i.Id, i.Title, i.Description, i.Status, i.Priority, assignee = i.Assignee.Name, labels = i.Labels.Select(l => l.Name), i.Team, updated = i.Updated }),
        notifications = s.Notifications().Select(n => new { n.Icon, n.Color, who = n.Who.Name, n.Text, issue = n.Issue.Id, at = n.At, n.Unread }),
        labels = Store.Labels, icons = Icons.Svg,
    }));
}
