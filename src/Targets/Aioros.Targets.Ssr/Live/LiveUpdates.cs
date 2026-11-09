using System.Buffers;
using System.IO.Compression;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Ssr.Live;

public static class LiveUpdates
{
    private static readonly byte[] DataPrefix = "data: "u8.ToArray();

    public static ReadOnlyMemory<byte> Connected { get; } = ": connected\n\n"u8.ToArray();

    /// <summary>The event that carries the regions of the views a live refresh changed.</summary>
    public const string ViewsEvent = "views";

    /// <summary>The event that opens a page's stream with the connection's name, which the page's posts send to act on its models.</summary>
    public const string ConnectionEvent = "connection";

    /// <summary>
    /// Streams a live page to one connection: when the page's model changes, and when a data key one of its loaders read is
    /// invalidated (<see cref="DataVersions"/>, from a command or a background service), as Turbo 8 refreshes a page on a
    /// broadcast and SvelteKit's <c>invalidate(key)</c> reruns the loads that depend on it. An invalidation reruns only the
    /// loaders that read its keys, each in a new scope with the connection's own user and access rules (Phoenix LiveView
    /// renders per connection); views refreshed alone are sent as their regions (<see cref="ViewsEvent"/>), anything else
    /// as the page's body or, for a script that knows the plan, its dynamics. The connection's models are its own and its
    /// page's posts that name it act on them (<see cref="LiveStates"/>), as a LiveView's events reach its socket's process.
    /// </summary>
    public static async Task StreamAsync(HttpContext http, PageDefinition page, PageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(handler);
        var loaded = await PageModels.LoadAsync(http, page);
        if (loaded.Models.IsDefault || loaded.Models.Length <= page.Layouts.Length)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var ended = Ended(http);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ended.Token);
        using var connection = new LiveConnection(http, page, handler, loaded);
        var name = handler.Live.Open(http, page, connection);
        var following = connection.FollowAsync(stop.Token);
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        var output = Output(http);
        try
        {
            await SendAsync(output, Opening(name), ended.Token);
            await foreach (var _ in connection.Signals.Reader.ReadAllAsync(ended.Token))
            {
                await connection.AnswerPostsAsync();
                if (!await connection.RefreshAsync())
                {
                    return;
                }

                await SettleAsync(http);
                using var message = new PooledBuffer();
                if (await connection.MessageAsync(message))
                {
                    await SendAsync(output, message.WrittenMemory, ended.Token);
                }
            }
        }
        catch (OperationCanceledException) when (ended.IsCancellationRequested)
        {
            await stop.CancelAsync();
        }
        finally
        {
            handler.Live.Close(name);
            await stop.CancelAsync();
            await following;
            if (output != http.Response.Body)
            {
                await output.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Ends when the request is aborted or the app starts stopping, so an open stream never holds a restart or a deploy
    /// for the host's shutdown timeout (the installed app's <see cref="LiveKeys"/> stream does the same).
    /// </summary>
    public static CancellationTokenSource Ended(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var stopping = http.RequestServices.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;
        return CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, stopping);
    }

    /// <summary>The stream's opening: the connection comment and the event naming the connection, which the script sends with the page's posts (<see cref="LiveStates"/>).</summary>
    private static ReadOnlyMemory<byte> Opening(string name)
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Write(Connected.Span);
        Event(writer, System.Text.Encoding.ASCII.GetBytes(name), ConnectionEvent);
        return writer.WrittenMemory;
    }

    public static void Event(IBufferWriter<byte> writer, ReadOnlySpan<byte> data, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (name is not null)
        {
            writer.Write("event: "u8);
            var span = writer.GetSpan(System.Text.Encoding.UTF8.GetMaxByteCount(name.Length));
            writer.Advance(System.Text.Encoding.UTF8.GetBytes(name, span));
            writer.Write("\n"u8);
        }

        foreach (var line in data.Split((byte)'\n'))
        {
            writer.Write(DataPrefix);
            writer.Write(data[line]);
            writer.Write("\n"u8);
        }

        writer.Write("\n"u8);
    }

    private static Stream Output(HttpContext http)
    {
        if (!http.Request.Headers.AcceptEncoding.ToString().Contains("gzip", StringComparison.OrdinalIgnoreCase))
        {
            return http.Response.Body;
        }

        http.Response.Headers.ContentEncoding = "gzip";
        return new GZipStream(new EventBuffer(http.Response.Body), CompressionLevel.Fastest, leaveOpen: false);
    }

    private static async Task SendAsync(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await output.WriteAsync(bytes, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static async Task SettleAsync(HttpContext http)
    {
        if (http.RequestServices.GetService(typeof(IUiThread)) is IUiThread ui)
        {
            await ui.InvokeAsync(static () => { }, http.RequestAborted);
        }
    }
}
