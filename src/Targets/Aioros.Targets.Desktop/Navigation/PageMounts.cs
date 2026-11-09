using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Hosting;
using Aioros.Pages;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>What the navigator does to the pages it mounts and the scopes that own their models.</summary>
internal static class PageMounts
{
    /// <summary>
    /// The models a navigation keeps, by index, from the layouts it shares with the page shown: a shared layout's model
    /// when its own route values are the same in both requests.
    /// </summary>
    public static ImmutableArray<object?> Kept(ImmutableArray<string> sources, int kept, ImmutableArray<object?> models, PageRequest? before, PageRequest after)
    {
        if (kept == 0 || before is null)
        {
            return [];
        }

        var chosen = new object?[sources.Length];
        for (var index = 0; index < kept; index++)
        {
            chosen[index] = DesktopRequests.SameValues(sources[index], before, after) ? models[index] : null;
        }

        return [.. chosen];
    }

    /// <summary>How many of the mounted layouts, outermost first, the next page shares; never its own page.</summary>
    public static int Shared(ImmutableArray<string> mounted, ImmutableArray<string> sources)
    {
        var kept = 0;
        while (kept < mounted.Length && kept < sources.Length - 1 && mounted[kept] == sources[kept])
        {
            kept++;
        }

        return kept;
    }

    public static async ValueTask ReleaseAsync(IServiceScope? scope)
    {
        if (scope is IAsyncDisposable asynchronous)
        {
            await asynchronous.DisposeAsync();
        }
        else
        {
            scope?.Dispose();
        }
    }

    /// <summary>Takes the page shown out of the window, or out of a kept layout's outlet, and releases its views.</summary>
    public static void Unmount(Window window, Outlet? outlet)
    {
        if ((outlet is null ? window.Content : outlet.Content) is not Control previous)
        {
            return;
        }

        if (outlet is null)
        {
            window.Content = null;
        }
        else
        {
            outlet.Content = null;
        }

        PageComposer.Release(previous);
    }

    public static async Task LaunchAsync(Window window, string url)
    {
        if (TopLevel.GetTopLevel(window) is { } top)
        {
            await top.Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
