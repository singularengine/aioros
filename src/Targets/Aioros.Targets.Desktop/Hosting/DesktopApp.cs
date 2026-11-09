using System.Collections.Immutable;
using System.Reflection;
using Aioros.Pages;

namespace Aioros.Targets.Desktop.Hosting;

public sealed record DesktopApp(
    Type? ApplicationType,
    Assembly Assembly,
    IServiceProvider Services,
    ImmutableArray<PageDefinition> Pages,
    PageDefinition? NotFound,
    string StartPath,
    Func<CancellationToken, Task> StartAsync,
    Func<CancellationToken, Task> StopAsync)
{
    /// <summary>The app's error page, shown when a page fails to load.</summary>
    public PageDefinition? Error { get; init; }

    /// <summary>Cancelled when the host is asked to stop, as <c>SIGTERM</c> does; the window then closes and the app exits.</summary>
    public CancellationToken Stopping { get; init; }

    /// <summary>
    /// The app's URL scheme (<c>AiorosScheme</c>): the app is then single-instance per user, opens deep links to its
    /// pages and, once installed, registers itself for the scheme.
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>Whether the app was published as an installer (<c>aioros build --installer</c>).</summary>
    public bool Installed { get; init; }

    /// <summary>Whether this is the installed app's first run after its install: it then signs in first, to land on the page the user came from.</summary>
    public bool FirstRun { get; init; }

    /// <summary>The Velopack feed of the installed app's updates, a URL or a folder.</summary>
    public string? Updates { get; init; }

    /// <summary>The app's project folder, where hot reload watches its XAML in development and names its positions from.</summary>
    public string? ProjectDirectory { get; init; }
}
