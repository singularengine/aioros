using System.Runtime.Versioning;
using Velopack;

namespace Aioros.Targets.Desktop.Handoff;

/// <summary>
/// The installed app's updates, through Velopack (MIT): on start it downloads a newer release from the feed, which
/// Velopack applies on the next start; when the server answers <c>426</c> (another contract), it applies the update now
/// and restarts the app on the same path with the arguments it started with. The feed is a URL (the website's
/// <c>/updates/</c> by default) or a folder.
/// </summary>
public sealed class AppUpdates(UpdateManager manager, TextWriter log, IReadOnlyList<string> arguments)
{
    private const string PathOption = "--path=";

    private Task _download = Task.CompletedTask;

    /// <summary>
    /// Runs Velopack's install, update and uninstall hooks, which must come first in an installed app's start, and
    /// applies an update downloaded before; an uninstall on Windows also removes the scheme's registration. Answers
    /// whether this is the first run after the install.
    /// </summary>
    public static bool Start(string[] args, string? scheme)
    {
        var first = false;
        var app = VelopackApp.Build().SetArgs(args).OnFirstRun(_ => first = true);
        if (scheme is not null && OperatingSystem.IsWindows())
        {
            app = app.OnBeforeUninstallFastCallback(Uninstalling(scheme));
        }

        app.Run();
        return first;
    }

    /// <summary>The arguments of the restarted app: the ones it started with, on <paramref name="path"/> instead of its first path or deep link.</summary>
    public static string[] RestartArguments(IEnumerable<string> arguments, string path) =>
        [.. arguments.Where(static arg => !arg.StartsWith(PathOption, StringComparison.Ordinal) && (arg.StartsWith('-') || !arg.Contains("://", StringComparison.Ordinal))), PathOption + path];

    /// <summary>Starts downloading a newer release in the background, so the next start runs it.</summary>
    public Task DownloadOnStartAsync(CancellationToken cancellationToken)
    {
        var download = DownloadAsync(cancellationToken);
        Volatile.Write(ref _download, download);
        return download;
    }

    /// <summary>
    /// Applies the newest release and restarts the app on <paramref name="path"/>; <see langword="false"/> when the app
    /// is not installed or no newer release is on the feed, so the failure is shown instead.
    /// </summary>
    public async Task<bool> RestartOnAsync(string path)
    {
        if (!manager.IsInstalled)
        {
            return false;
        }

        await Volatile.Read(ref _download);
        var pending = manager.UpdatePendingRestart;
        if (pending is null && await manager.CheckForUpdatesAsync() is { } update)
        {
            await manager.DownloadUpdatesAsync(update);
            pending = update.TargetFullRelease;
        }

        if (pending is null)
        {
            return false;
        }

        await log.WriteLineAsync($"The server needs version {pending.Version}; updating and restarting on {path}.");
        manager.ApplyUpdatesAndRestart(pending, RestartArguments(arguments, path));
        return true;
    }

    private async Task DownloadAsync(CancellationToken cancellationToken)
    {
        if (!manager.IsInstalled)
        {
            return;
        }

        try
        {
            if (await manager.CheckForUpdatesAsync() is { } update)
            {
                await manager.DownloadUpdatesAsync(update, null, cancellationToken);
                await log.WriteLineAsync($"Update {update.TargetFullRelease.Version} downloaded; it is applied on the next start.");
            }
        }
#pragma warning disable CA1031
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            await log.WriteLineAsync($"The update check failed: {exception.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static VelopackHook Uninstalling(string scheme) => _ => Unregister(scheme);

    [SupportedOSPlatform("windows")]
    private static void Unregister(string scheme) => Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + scheme.ToLowerInvariant(), throwOnMissingSubKey: false);
}
