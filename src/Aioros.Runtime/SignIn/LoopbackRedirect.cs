using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aioros.Runtime.SignIn;

/// <summary>
/// The redirect of a native app's sign-in (RFC 8252, section 7.3): a listener on <c>127.0.0.1</c> with a port the operating
/// system chose, which waits for the browser to come back with the authorization code.
/// </summary>
public sealed class LoopbackRedirect : IDisposable
{
    private const int MaxHead = 16 * 1024;
    private readonly TcpListener _listener;
    private readonly string _path;

    private LoopbackRedirect(TcpListener listener, string path)
    {
        _listener = listener;
        _path = path;
        RedirectUri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}{path}");
    }

    public Uri RedirectUri { get; }

    public static LoopbackRedirect Listen(string path = "/callback")
    {
        ArgumentNullException.ThrowIfNull(path);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new LoopbackRedirect(listener, path);
    }

    /// <summary>
    /// Waits for the browser's redirect and returns its authorization code. Requests to other paths are answered
    /// <c>404</c> and ignored; a <c>state</c> other than <paramref name="state"/>, or an <c>error</c>, fails the sign-in.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> ReceiveAsync(string state, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            var target = await TargetAsync(stream, cancellationToken);
            var split = target?.IndexOf('?', StringComparison.Ordinal) ?? -1;
            var path = split < 0 ? target : target![..split];
            if (path != _path)
            {
                await AnswerAsync(stream, "404 Not Found", "Not found", cancellationToken);
                continue;
            }

            var query = Query(target![(split + 1)..]);
            if (query.GetValueOrDefault("state") != state)
            {
                await AnswerAsync(stream, "400 Bad Request", "The sign-in could not be completed: it was not started by this app. Close this window and try again.", cancellationToken);
                throw new SignInRequiredException("The sign-in answer did not carry the state this app sent.");
            }

            if (query.TryGetValue("error", out var error) || !query.ContainsKey("code"))
            {
                await AnswerAsync(stream, "400 Bad Request", "The sign-in was not completed. Close this window and try again.", cancellationToken);
                throw new SignInRequiredException($"The sign-in was refused ({error ?? "no code"}).");
            }

            await AnswerAsync(stream, "200 OK", "You are signed in. You can close this window and return to the app.", cancellationToken);
            return query;
        }
    }

    public void Dispose() => _listener.Dispose();

    private static async Task<string?> TargetAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxHead];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
            if (buffer.AsSpan(0, read).IndexOf("\r\n\r\n"u8) >= 0)
            {
                break;
            }
        }

        var line = Encoding.ASCII.GetString(buffer, 0, read).Split("\r\n", 2)[0].Split(' ');
        return line is ["GET", var target, ..] ? target : null;
    }

    private static Dictionary<string, string> Query(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static pair => pair.Split('=', 2))
            .GroupBy(static pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First() is [_, var value] ? Uri.UnescapeDataString(value.Replace('+', ' ')) : string.Empty, StringComparer.Ordinal);

    private static async Task AnswerAsync(NetworkStream stream, string status, string message, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes($"<!doctype html><html><head><meta charset=\"utf-8\"><title>Sign in</title></head><body style=\"font-family:sans-serif;text-align:center;margin-top:4em\"><p>{WebUtility.HtmlEncode(message)}</p></body></html>");
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(head, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }
}
