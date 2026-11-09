using System.Collections.Immutable;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace Aioros.Targets.Desktop.Handoff;

/// <summary>
/// Keeps one instance of the installed app per user. Windows and Linux start a new process for every deep link, and
/// Avalonia has no API for this, so the first instance listens on a named pipe and every later one forwards its
/// arguments there and exits, as VS Code and Tauri's single-instance plugin do. The pipe is the current user's only;
/// on Unix it is a socket file, and one left by a killed instance is replaced.
/// </summary>
public sealed class SingleInstance : IAsyncDisposable
{
    private const PipeOptions Options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

    private readonly string _name;
    private readonly Channel<ImmutableArray<string>> _forwarded = Channel.CreateUnbounded<ImmutableArray<string>>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listening;

    private SingleInstance(string name, NamedPipeServerStream server)
    {
        _name = name;
        _listening = ListenAsync(server);
    }

    /// <summary>The arguments later instances forwarded, in the order they arrived.</summary>
    public ChannelReader<ImmutableArray<string>> Forwarded => _forwarded.Reader;

    /// <summary>The pipe name of an app's scheme for the current user.</summary>
    public static string NameFor(string scheme) =>
        "aioros-" + scheme.ToLowerInvariant() + "-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName)))[..12];

    /// <summary>Where .NET keeps a pipe on Unix: a socket file in the temporary folder.</summary>
    public static string SocketPath(string name) => Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + name);

    /// <summary>
    /// Becomes the running instance, or forwards <paramref name="arguments"/> to the one already running and answers
    /// <see langword="null"/>, in which case this process should exit.
    /// </summary>
    public static SingleInstance? Claim(string name, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (TryListen(name) is { } server)
        {
            return new SingleInstance(name, server);
        }

        if (TryForward(name, arguments))
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            File.Delete(SocketPath(name));
            if (TryListen(name) is { } replaced)
            {
                return new SingleInstance(name, replaced);
            }
        }

        return TryForward(name, arguments) ? null : throw new IOException($"The pipe '{name}' is held by an instance that does not answer.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _listening;
        _stop.Dispose();
    }

    private static NamedPipeServerStream? TryListen(string name)
    {
        try
        {
            return Listen(name, PipeOptions.FirstPipeInstance);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static NamedPipeServerStream Listen(string name, PipeOptions first) =>
        new(name, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, Options | first);

    private static bool TryForward(string name, IReadOnlyList<string> arguments)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.Out, Options);
            client.Connect(0);
            client.Write(Encoding.UTF8.GetBytes(string.Join('\n', arguments)));
            client.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task ListenAsync(NamedPipeServerStream server)
    {
        try
        {
            while (true)
            {
                await server.WaitForConnectionAsync(_stop.Token);
                var next = Listen(_name, PipeOptions.None);
                try
                {
                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    var text = await reader.ReadToEndAsync(_stop.Token);
                    await _forwarded.Writer.WriteAsync(text.Length == 0 ? [] : [.. text.Split('\n')], _stop.Token);
                }
                finally
                {
                    await server.DisposeAsync();
                    server = next;
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        finally
        {
            await server.DisposeAsync();
            _forwarded.Writer.TryComplete();
        }
    }
}
