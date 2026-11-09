using System.Text;
using System.Text.Json;
using Aioros.Development;
using Aioros.Events;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Development;

public static class DevelopmentChannel
{
    public const string Path = "/_aioros/dev";

    private static ReadOnlyMemory<byte> Reconnect { get; } = "retry: 250\n\n"u8.ToArray();

    public static async Task StreamAsync(HttpContext http, IEventBus bus)
    {
        ArgumentNullException.ThrowIfNull(http);
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        using var ended = Live.LiveUpdates.Ended(http);
        var events = bus.SubscribeAsync<object>(ended.Token);
        try
        {
            await http.Response.BodyWriter.WriteAsync(Reconnect, ended.Token);
            await foreach (var message in events)
            {
                if (Format(message) is not { } text)
                {
                    continue;
                }

                await http.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(text), ended.Token);
                await http.Response.BodyWriter.FlushAsync(ended.Token);
            }
        }
        catch (OperationCanceledException) when (ended.IsCancellationRequested)
        {
            return;
        }
    }

    public static async Task InvalidateAsync(IEventBus bus, PlanCache cache, CancellationToken cancellationToken)
    {
        await foreach (var message in bus.SubscribeAsync<object>(cancellationToken))
        {
            Apply(message, cache);
        }
    }

    public static void Apply(object message, PlanCache cache)
    {
        if (message is XamlReloaded or CodeReloaded)
        {
            cache.Clear();
        }
    }

    public static string? Format(object message) => message switch
    {
        XamlReloaded reloaded => $"event: reload\ndata: {reloaded.Source}\n\n",
        CodeReloaded => "event: reload\ndata: code\n\n",
        BuildReported build => $"event: build\ndata: {JsonSerializer.Serialize(build, BuildContext.Default.BuildReported)}\n\n",
        XamlReloadFailed failed => $"event: failure\ndata: {JsonSerializer.Serialize(new Failure(failed.Source, failed.Message), FailureContext.Default.Failure)}\n\n",
        _ => null,
    };
}

public sealed record Failure(string Source, string Message);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(Failure))]
public sealed partial class FailureContext : System.Text.Json.Serialization.JsonSerializerContext;
