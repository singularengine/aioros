using System.Diagnostics;
using System.Threading.Channels;
using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Navigation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Aioros.Targets.Desktop.HotReload;

/// <summary>What a hot reload did: the source it loaded (none for a C# edit) and how long it took to show.</summary>
public sealed record HotReloaded(string? Source, TimeSpan Took);

/// <summary>
/// Hot reload for the desktop app in development: a saved <c>.axaml</c> of the project is loaded over the compiled XAML
/// and the window reloads it with the same view models (DESKTOP-98), an edit that does not parse is shown over the
/// window until it does, and a C# edit the runtime applies rebuilds every view (HotAvalonia's watcher, Flutter's reload).
/// </summary>
public sealed class DesktopHotReload(Window window, DesktopNavigator navigator, XamlSources sources, string projectDirectory)
{
    private readonly TaskCompletionSource _watching = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Dictionary<string, string> _tried = new(StringComparer.Ordinal);

    /// <summary>Completes once the project folder is watched.</summary>
    public Task Watching => _watching.Task;

    public event EventHandler<HotReloaded>? Reloaded;

    /// <summary>The error of the last edit that did not parse, with its source, until an edit parses.</summary>
    public string? Fault { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var changes = Channel.CreateUnbounded<string?>(new UnboundedChannelOptions { SingleReader = true });
        using var watcher = new FileSystemWatcher(projectDirectory, "*.axaml")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Changed += (_, change) => changes.Writer.TryWrite(change.FullPath);
        watcher.Created += (_, change) => changes.Writer.TryWrite(change.FullPath);
        watcher.Renamed += (_, change) => changes.Writer.TryWrite(change.FullPath);
        watcher.EnableRaisingEvents = true;
        var code = ForwardAsync(changes.Writer, cancellationToken);
        _watching.TrySetResult();
        try
        {
            await foreach (var change in changes.Reader.ReadAllAsync(cancellationToken))
            {
                var pending = new List<string?> { change };
                while (changes.Reader.TryRead(out var next))
                {
                    pending.Add(next);
                }

                foreach (var path in pending.Distinct())
                {
                    await ApplyAsync(path, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await code;
        }
    }

    private static async Task ForwardAsync(ChannelWriter<string?> changes, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in DesktopCodeUpdates.Updates.ReadAllAsync(cancellationToken))
            {
                changes.TryWrite(null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ApplyAsync(string? path, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        if (path is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => navigator.Reload());
            Reloaded?.Invoke(this, new HotReloaded(null, Stopwatch.GetElapsedTime(started)));
            return;
        }

        var source = Path.GetRelativePath(projectDirectory, path).Replace('\\', '/');
        if (source.StartsWith("bin/", StringComparison.Ordinal) || source.StartsWith("obj/", StringComparison.Ordinal) || !File.Exists(path))
        {
            return;
        }

        var xaml = await File.ReadAllTextAsync(path, cancellationToken);
        if (xaml.Length == 0)
        {
            return;
        }

        var shown = await Dispatcher.UIThread.InvokeAsync(() => Show(source, xaml));
        if (shown)
        {
            Reloaded?.Invoke(this, new HotReloaded(source, Stopwatch.GetElapsedTime(started)));
        }
    }

    /// <summary>Shows an edit on the UI thread, once per text: a file watcher reports one save more than once.</summary>
    private bool Show(string source, string xaml)
    {
        if (sources.IsCurrent(source, xaml))
        {
            _tried[source] = xaml;
            return Cleared();
        }

        if (_tried.TryGetValue(source, out var tried) && tried == xaml)
        {
            return false;
        }

        _tried[source] = xaml;

        if (sources.Reload(source, xaml) is { } failure)
        {
            Fault = $"{source}: {failure}";
            FaultOverlay.Show(window, Fault);
            return true;
        }

        Cleared();
        navigator.Reload(XamlSources.Restyles(xaml) ? null : source);
        return true;
    }

    /// <summary>Takes the error away once a save parses, which the text the page already shows does; false when none was shown.</summary>
    private bool Cleared()
    {
        if (Fault is null)
        {
            return false;
        }

        Fault = null;
        FaultOverlay.Hide(window);
        return true;
    }
}
