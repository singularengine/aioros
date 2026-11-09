using Aioros.Avalonia.Hosting;
using System.Threading.Channels;
using Aioros.Development;
using Aioros.Events;
using Aioros.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aioros.Development;

internal sealed partial class XamlWatcher(AiorosApp app, DeferredPageEngine engine, XamlSources sources, IEventBus bus, ILogger<XamlWatcher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(app.ProjectDirectory))
        {
            return;
        }

        var changes = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        using var watcher = new FileSystemWatcher(app.ProjectDirectory, "*.axaml")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Changed += (_, change) => changes.Writer.TryWrite(change.FullPath);
        watcher.Created += (_, change) => changes.Writer.TryWrite(change.FullPath);
        watcher.Renamed += (_, change) => changes.Writer.TryWrite(change.FullPath);
        watcher.EnableRaisingEvents = true;
        Watching(log, app.ProjectDirectory);
        await WarmAsync(stoppingToken);
        await foreach (var path in changes.Reader.ReadAllAsync(stoppingToken))
        {
            var pending = new HashSet<string>(StringComparer.Ordinal) { path };
            while (changes.Reader.TryRead(out var next))
            {
                pending.Add(next);
            }

            foreach (var changed in pending)
            {
                await ReloadAsync(changed, stoppingToken);
            }
        }
    }

    private async Task WarmAsync(CancellationToken cancellationToken)
    {
        var host = (AvaloniaPageEngine)await engine.Engine;
        foreach (var page in app.Pages.Take(8))
        {
            var path = Path.Combine(app.ProjectDirectory, page.Source);
            if (!File.Exists(path))
            {
                continue;
            }

            var xaml = await File.ReadAllTextAsync(path, cancellationToken);
            await host.DispatchAsync(() => Parses(xaml), cancellationToken);
        }
    }

    private bool Parses(string xaml)
    {
        try
        {
            sources.Parse(xaml);
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return false;
        }
    }

    private async Task ReloadAsync(string path, CancellationToken cancellationToken)
    {
        var source = Path.GetRelativePath(app.ProjectDirectory, path).Replace('\\', '/');
        if (source.StartsWith("bin/", StringComparison.Ordinal) || source.StartsWith("obj/", StringComparison.Ordinal) || !File.Exists(path))
        {
            return;
        }

        var xaml = await File.ReadAllTextAsync(path, cancellationToken);
        var failure = await ((AvaloniaPageEngine)await engine.Engine).ReloadAsync(source, xaml, cancellationToken);
        if (failure is null)
        {
            Reloaded(log, source);
            await bus.PublishAsync(new XamlReloaded(source), cancellationToken);
        }
        else
        {
            Failed(log, source, failure);
            await bus.PublishAsync(new XamlReloadFailed(source, failure), cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload is watching {Directory}")]
    private static partial void Watching(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloaded {Source}")]
    private static partial void Reloaded(ILogger logger, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Source} does not parse: {Failure}")]
    private static partial void Failed(ILogger logger, string source, string failure);
}
