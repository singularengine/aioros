using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Routing.Files;

namespace Aioros.Cli.Commands;

public static class Planner
{
    public const string Usage = """
        aioros <command>

          new <name>   Create an app (dotnet new aioros)
          dev          Run the app with hot reload (dotnet watch) on its launch profile's
                       port (3000 without one), or the next free port; --port=<n> chooses another;
                       --desktop runs it as its desktop window
          build        Publish the app to .aioros/publish and precompile its pages;
                       --server=<url> bakes in the server the installed app works against;
                       --installer --version=<semver> packs the desktop app with Velopack
                       into .aioros/installer/releases (Setup.exe, .app or AppImage)
          start        Run the published app
          routes       List the routes of the app's Pages folder

        """;

    /// <summary>The version of Velopack's <c>vpk</c> the installer is packed with, the version of the <c>Velopack</c> package the desktop target references.</summary>
    public const string VelopackVersion = "1.2.161";

    /// <summary>
    /// Plans the command; <paramref name="free"/> says whether a local port can be listened on (every port when null), so
    /// <c>dev</c> moves past one that is taken, and <paramref name="token"/> is the development token <c>dev</c> gives the
    /// app, with which the tool posts the build's errors to it; <paramref name="owner"/> names the process holding a taken port, when the machine can tell.
    /// </summary>
    public static CommandPlan Plan(IReadOnlyList<string> args, AppFolder folder, string version, string? runtime = null, Func<int, bool>? free = null, string? token = null, Func<int, string?>? owner = null) => (args.Count == 0 ? "help" : args[0]) switch
    {
        "help" or "--help" or "-h" => CommandPlan.Print(Usage),
        var command when args.Skip(1).Any(static arg => arg is "--help" or "-h") && UsageOf(command) is { } usage => CommandPlan.Print(usage),
        "new" when args.Count > 1 => CommandPlan.Run(
            DotnetStep.Of("new", "install", $"Aioros.Templates::{version}"),
            DotnetStep.Of("new", "aioros", "-n", args[1])),
        "dev" or "build" or "start" or "routes" when folder.Problem is { } problem => CommandPlan.Print(problem, 1),
        "dev" when folder.Project is { } project => Dev(args, project, folder.Port ?? DefaultPort, free ?? (static _ => true), token, owner),
        "build" when Server(args) is { Problem: { } problem } => CommandPlan.Print(problem, 1),
        "build" when args.Contains(InstallerFlag) && folder.Project is { } project => Installer(args, project, runtime ?? PortableRuntime),
        "build" when folder.Project is { } project => CommandPlan.Run(
            DotnetStep.Of(["publish", project, "-c", "Release", "-o", Publish, .. Server(args).Property]),
            new DotnetStep([Assembly(project), "--precompile=" + PlansFile], Production) { WorkingDirectory = Publish }),
        "start" when folder.Project is { } project => CommandPlan.Run(new DotnetStep([Assembly(project)], Production) { WorkingDirectory = Publish }),
        "routes" => Routes(folder.Files),
        _ => CommandPlan.Print(Usage, 1),
    };

    private static string? UsageOf(string command) => command switch
    {
        "new" => """
            aioros new <name>

            Creates an app from the Aioros template (dotnet new aioros).

            """,
        "dev" => """
            aioros dev [--port=<n>] [--desktop]

            Runs the app with hot reload (dotnet watch).

              --port=<n>   Listen on this port instead of the launch profile's (3000 without one);
                           when it is taken, the next free one is used
              --desktop    Run the app as its desktop window

            """,
        "build" => """
            aioros build [--server=<url>] [--installer --version=<semver>]

            Publishes the app to .aioros/publish and precompiles its pages.

              --server=<url>       The server the installed app works against
              --installer          Pack the desktop app with Velopack into .aioros/installer/releases
              --version=<semver>   The release's version, required with --installer

            """,
        "start" => """
            aioros start

            Runs the app published by aioros build.

            """,
        "routes" => """
            aioros routes

            Lists the routes of the app's Pages folder.

            """,
        _ => null,
    };

    private static string Publish { get; } = Path.Combine(".aioros", "publish");

    private const string ServerOption = "--server=";

    /// <summary>
    /// The server origin <c>--server=</c> bakes into the installed app, as the MSBuild property <c>AiorosServer</c> the
    /// generator reads (Expo's <c>origin</c>); nothing without the option, so the project's own property applies.
    /// </summary>
    private static (string[] Property, string? Problem) Server(IReadOnlyList<string> args) =>
        args.FirstOrDefault(static arg => arg.StartsWith(ServerOption, StringComparison.Ordinal))?[ServerOption.Length..] switch
        {
            null => ([], null),
            var url when Uri.TryCreate(url, UriKind.Absolute, out var server) && (server.Scheme == Uri.UriSchemeHttps || server.Scheme == Uri.UriSchemeHttp) =>
                (["-p:AiorosServer=" + server.GetLeftPart(UriPartial.Authority)], null),
            var url => ([], $"--server must be an absolute http or https URL, not '{url}'.\n"),
        };

    private const string PlansFile = "aioros.plans.json";

    private const string InstallerFlag = "--installer";

    /// <summary>
    /// The portable runtime identifier of this machine (<c>linux-x64</c>, <c>win-arm64</c>, <c>osx-arm64</c>); a distribution's
    /// own .NET build reports its own (<c>arch-x64</c>), for which no runtime pack exists on nuget.org.
    /// </summary>
    public static string PortableRuntime =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    private const string VersionOption = "--version=";

    /// <summary>
    /// Publishes the installed app self-contained for <paramref name="runtime"/> with the <c>Aioros.Installed</c> switch,
    /// so it runs on the desktop without <c>--desktop</c>, then packs it with Velopack's <c>vpk</c> run through
    /// <c>dotnet dnx</c> at the version the framework references: a Windows <c>Setup.exe</c>, a macOS <c>.app</c> with the
    /// <c>Info.plist</c> the build wrote (<c>CFBundleURLTypes</c>), or a Linux AppImage, with the release feed beside it.
    /// </summary>
    private static CommandPlan Installer(IReadOnlyList<string> args, string project, string runtime)
    {
        var version = args.FirstOrDefault(static arg => arg.StartsWith(VersionOption, StringComparison.Ordinal))?[VersionOption.Length..];
        if (version is null || !System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return CommandPlan.Print($"--installer needs --version=<major.minor.patch>, the release's semantic version, not '{version}'.\n", 1);
        }

        var name = Path.GetFileNameWithoutExtension(project);
        var publish = Path.Combine(".aioros", "installer", "publish");
        string[] mac = runtime.StartsWith("osx", StringComparison.Ordinal) ? ["--plist", Path.Combine(publish, "Info.plist")] : [];
        return CommandPlan.Run(
            DotnetStep.Of(["publish", project, "-c", "Release", "-r", runtime, "--self-contained", "-o", publish, "-p:AiorosInstalled=true", "-p:Version=" + version, .. Server(args).Property]),
            DotnetStep.Of([
                "dnx", "vpk@" + VelopackVersion, "--yes", "pack", "--packId", name, "--packVersion", version, "--packDir", publish,
                "--mainExe", runtime.StartsWith("win", StringComparison.Ordinal) ? name + ".exe" : name, "--outputDir", Path.Combine(".aioros", "installer", "releases"), .. mac]));
    }

    /// <summary>The environment variable that gives the app the tool's development token (<c>Aioros:DevelopmentToken</c>).</summary>
    public const string DevelopmentTokenVariable = "Aioros__DevelopmentToken";

    /// <summary>The environment variable that gives the app the tool's process id (<c>Aioros:DevelopmentTool</c>), so the app stops when the tool is gone.</summary>
    public const string DevelopmentToolVariable = "Aioros__DevelopmentTool";

    private const string PortOption = "--port=";

    private const string DesktopFlag = "--desktop";

    private const int DefaultPort = 3000;

    private const int PortsTried = 10;

    /// <summary>
    /// Runs the app under <c>dotnet watch</c> on <c>--port</c> (the launch profile's port, or 3000) or, when it is taken, the next free one of
    /// the ten after it, as <c>next dev</c> and Vite do, without opening a browser or injecting <c>dotnet watch</c>'s own
    /// refresh script: the development channel refreshes the page in place.
    /// </summary>
    private static CommandPlan Dev(IReadOnlyList<string> args, string project, int port, Func<int, bool> free, string? token, Func<int, string?>? owner)
    {
        if (args.Contains(DesktopFlag))
        {
            return CommandPlan.Run(new DotnetStep(["watch", "--project", project, "--non-interactive", "--", DesktopFlag], Development));
        }

        var option = args.FirstOrDefault(static arg => arg.StartsWith(PortOption, StringComparison.Ordinal))?[PortOption.Length..];
        var wanted = port;
        if (option is not null && !(int.TryParse(option, NumberStyles.None, CultureInfo.InvariantCulture, out wanted) && wanted is >= 1 and <= 65535))
        {
            return CommandPlan.Print($"--port must be a number from 1 to 65535, not '{option}'.\n", 1);
        }

        var listened = Enumerable.Range(wanted, Math.Min(PortsTried + 1, 65536 - wanted)).Where(free).Select(static candidate => (int?)candidate).FirstOrDefault();
        if (listened is null)
        {
            return CommandPlan.Print($"Ports {wanted} to {Math.Min(wanted + PortsTried, 65535)} are all in use; choose another with --port.\n", 1);
        }

        var step = new DotnetStep(["watch", "--project", project, "--non-interactive", "--", $"--urls=http://localhost:{listened}"], token is null ? Development : Development.Add(DevelopmentTokenVariable, token));
        return listened == wanted ? CommandPlan.Run(step) : new CommandPlan(0, $"Port {wanted} is in use{(owner?.Invoke(wanted) is { } process ? " by " + process : string.Empty)}, using {listened} instead.\n", [step]);
    }

    private static string Assembly(string project) => Path.GetFileNameWithoutExtension(project) + ".dll";

    private static ImmutableDictionary<string, string> Production { get; } =
        ImmutableDictionary<string, string>.Empty.Add("ASPNETCORE_ENVIRONMENT", "Production");

    private static ImmutableDictionary<string, string> Development { get; } =
        ImmutableDictionary<string, string>.Empty.Add("ASPNETCORE_ENVIRONMENT", "Development").Add("DOTNET_WATCH_SUPPRESS_EMOJIS", "1")
            .Add("DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER", "1").Add("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH", "1");

    private static CommandPlan Routes(IReadOnlyList<string> files)
    {
        var map = FileRoutes.Map(files);
        var output = new StringBuilder();
        foreach (var route in map.Routes.OrderBy(route => route.Route, StringComparer.Ordinal))
        {
            output.Append(route.Route).Append("  ").Append(route.Source);
            foreach (var layout in route.Layouts)
            {
                output.Append("  < ").Append(layout);
            }

            output.Append('\n');
        }

        foreach (var conflict in map.Conflicts)
        {
            output.Append("conflict: ").Append(conflict.Route).Append("  ").Append(conflict.First).Append("  ").Append(conflict.Second).Append('\n');
        }

        return CommandPlan.Print(output.ToString(), map.Conflicts.Count == 0 ? 0 : 1);
    }
}
