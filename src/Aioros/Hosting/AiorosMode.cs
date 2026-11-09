using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Aioros.Hosting;

public sealed record AiorosMode(bool Desktop, string StartPath, string[] Arguments)
{
    public const string DesktopFlag = "--desktop";

    public const string PrecompileOption = "--precompile=";

    /// <summary>The runtime switch <c>aioros build --installer</c> sets, so the installed app runs on the desktop without <c>--desktop</c>.</summary>
    public const string InstalledSwitch = "Aioros.Installed";

    /// <summary>Whether this process is an app published as an installer.</summary>
    public static bool IsInstalled => AppContext.TryGetSwitch(InstalledSwitch, out var installed) && installed;

    public string? Precompile { get; init; }

    /// <summary>The remote server the installed app works against, from <c>Aioros:Server</c>; only on the desktop.</summary>
    public Uri? Server { get; init; }

    /// <summary>What this host is: the website, the desktop running the server's services in process, or the installed client.</summary>
    public AiorosRole Role => !Desktop ? AiorosRole.Server : Server is null ? AiorosRole.InProcess : AiorosRole.Client;

    /// <summary>Reads the mode from the arguments and the configuration.</summary>
    /// <param name="args">The app's arguments.</param>
    /// <param name="configuration">The app's configuration; <c>Aioros:Server</c> names the server of the installed app.</param>
    /// <param name="baked">
    /// The server origin <c>aioros build</c> baked into the app (<c>AiorosServer</c>), used by the installed app outside
    /// development when <c>Aioros:Server</c> is not configured.
    /// </param>
    /// <param name="development">Whether the app runs in <c>Development</c>, where the baked origin is ignored.</param>
    /// <param name="installed">Whether the app was published as an installer, which runs on the desktop without <c>--desktop</c>.</param>
    public static AiorosMode From(string[] args, IConfiguration configuration, string? baked = null, bool development = false, bool installed = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var desktop = installed || args.Contains(DesktopFlag, StringComparer.Ordinal);
        var server = configuration[AiorosFunctionRegistration.ServerSetting] is { Length: > 0 } configured ? configured : development ? null : baked;
        return new(desktop, Option(args, "--path=") ?? "/", args)
        {
            Precompile = Option(args, PrecompileOption),
            Server = desktop && server is { Length: > 0 } ? new Uri(server) : null,
        };
    }

    /// <summary>
    /// The installed app's Velopack feed: <c>Aioros:Updates</c> when set (a URL or a folder), otherwise the server's own
    /// <c>/updates/</c>, which <c>MapAioros</c> serves from <c>Aioros:UpdatesFolder</c>; none without a server.
    /// </summary>
    public static string? UpdateFeed(string? configured, Uri? server) =>
        configured is { Length: > 0 } ? configured : server is null ? null : new Uri(server, "/updates/").AbsoluteUri;

    /// <summary>Whether this is the installed app's first run after its install, as Velopack reports it.</summary>
    public bool FirstRun { get; init; }

    /// <summary>
    /// The arguments the command-line configuration reads: all but the switches, which take no value, and the deep links
    /// the operating system passes to the installed app (<c>shop://open?path=...</c>), which are no settings;
    /// an argument after a <c>--key</c> without <c>=</c> is that key's value, even a URL (<c>--urls http://...</c>).
    /// </summary>
    public static string[] Settings(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var settings = new List<string>(args.Length);
        var value = false;
        foreach (var arg in args)
        {
            if (value)
            {
                settings.Add(arg);
                value = false;
            }
            else if (arg != DesktopFlag && (IsKey(arg) || !arg.Contains("://", StringComparison.Ordinal)))
            {
                settings.Add(arg);
                value = IsKey(arg) && !arg.Contains('=', StringComparison.Ordinal);
            }
        }

        return [.. settings];
    }

    private static bool IsKey(string arg) => arg.StartsWith('-') || arg.StartsWith('/');

    private static string? Option(string[] args, string prefix) =>
        args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
