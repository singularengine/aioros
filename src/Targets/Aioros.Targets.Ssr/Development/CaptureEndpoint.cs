using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Aioros.Pages;
using Aioros.Routing.Tables;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Themes;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Development;

public static class CaptureEndpoint
{
    public const string Path = "/_aioros/capture";

    public static async Task HandleAsync(HttpContext http, IReadOnlyList<PageDefinition> pages, IPageCapture capture)
    {
        var query = http.Request.Query;
        var path = query["path"].ToString() is { Length: > 0 } requested ? requested : "/";
        var table = pages.Aggregate(RouteTable.Empty, (routes, page) => routes.Add(page.Route));
        var match = table.Match(path);
        if (!match.IsMatch)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var state = query["state"].ToString() is { Length: > 0 } named ? named : CaptureOptions.Normal;
        if (!CaptureOptions.States.Contains(state))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var page = pages.First(candidate => candidate.Route == match.Template);
        var request = new PageRequest(path, match.Values.All, ImmutableDictionary<string, string>.Empty);
        var models = (await PageModels.LoadAsync(http.RequestServices, page, request, capture, http.RequestAborted)).Models;
        var theme = query["theme"] == ThemeSwitch.Dark ? ThemeSwitch.Dark : ThemeSwitch.Light;
        var captured = await capture.CaptureAsync(page, models, new CaptureOptions(theme, Size(query["width"], 1280), Size(query["height"], 900), state), http.RequestAborted);
        if (query["format"] == "json")
        {
            http.Response.ContentType = "application/json";
            await WriteJsonAsync(http.Response.Body, captured, http.RequestAborted);
            return;
        }

        http.Response.ContentType = "image/png";
        await http.Response.Body.WriteAsync(captured.Png, http.RequestAborted);
    }

    private static async Task WriteJsonAsync(Stream body, PageCapture captured, CancellationToken cancellationToken)
    {
        await using var json = new Utf8JsonWriter(body);
        json.WriteStartObject();
        json.WriteBase64String("png", captured.Png);
        json.WriteStartArray("texts");
        foreach (var text in captured.Texts)
        {
            json.WriteStartObject();
            json.WriteString("text", text.Text);
            json.WriteNumber("x", Math.Round(text.X, 2));
            json.WriteNumber("y", Math.Round(text.Y, 2));
            json.WriteNumber("width", Math.Round(text.Width, 2));
            json.WriteNumber("height", Math.Round(text.Height, 2));
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
        await json.FlushAsync(cancellationToken);
    }

    private static int Size(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) ? Math.Clamp(size, 100, 4000) : fallback;
}
