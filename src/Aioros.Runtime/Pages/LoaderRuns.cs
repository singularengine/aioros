using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Runtime.Pages;

/// <summary>
/// What the loaders of a page answered, by slot, and the data keys each read: the keys only once a loader read one, so a
/// page without keys carries nothing for them.
/// </summary>
internal readonly record struct SlotResults(LoaderOutcome[] Data, ImmutableArray<string>[]? Keys);

/// <summary>
/// Runs a page's loaders in parallel, each in its own scope and the views' behind their error boundaries, collecting their
/// results and, from each loader's own services, the keys it read. Each run returns its keys, so one owner, the page's
/// load, gathers them once the runs completed, without sharing state between them.
/// </summary>
internal static class LoaderRuns
{
    private static readonly LoaderOutcome Hidden = new(ViewState.Hidden);

    private static readonly LoaderOutcome Failed = new(ViewState.Failed);

    private static readonly Action<ILogger, string, string, Exception?> ViewFailed =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, "ViewFailed"), "The view {View} of {Route} failed to load; its region shows an error");

    /// <summary>Whether a slot's loader runs: every one by default, otherwise the page's and its layouts' and the given views'.</summary>
    public static bool Runs(PageDefinition page, ImmutableArray<int> only, int index) => only.IsDefault || index < page.FirstView || only.Contains(index);

    public static async ValueTask<SlotResults> LoadAsync(IServiceProvider services, PageDefinition page, PageRequest request, PageLoad load, CancellationToken cancellationToken, ImmutableArray<int> only = default)
    {
        if (services.GetService(typeof(ILoaderResults)) is ILoaderResults remote)
        {
            var answered = await remote.LoadAsync(page, request, cancellationToken);
            return new SlotResults(ImmutableCollectionsMarshal.AsArray(answered.Outcomes) ?? [], answered.Keys.IsDefaultOrEmpty ? null : ImmutableCollectionsMarshal.AsArray(answered.Keys));
        }

        var data = new LoaderOutcome[page.ModelCount];
        var run = new Run(services, request, load, Count(page, only), cancellationToken);
        var gathered = new Gathered(data.Length);
        for (var index = 0; index < data.Length; index++)
        {
            if (PageLoader.LoaderAt(page, index) is { } loader && Runs(page, only, index) && !PageLoader.Repeated(page, index))
            {
                gathered = gathered.Add(index, index < page.FirstView
                    ? KeepAsync(loader, run, data, index)
                    : KeepViewAsync(page, loader, run, data, index));
            }
        }

        return new SlotResults(data, await gathered.KeysAsync());
    }

    private static int Count(PageDefinition page, ImmutableArray<int> only)
    {
        var count = 0;
        for (var index = 0; index < page.ModelCount; index++)
        {
            count += PageLoader.LoaderAt(page, index) is not null && Runs(page, only, index) && !PageLoader.Repeated(page, index) ? 1 : 0;
        }

        return count;
    }

    /// <summary>Runs only the given views' loaders, in process or on the server for the installed app.</summary>
    public static async ValueTask<SlotResults> LoadViewsAsync(IServiceProvider services, PageDefinition page, PageRequest request, PageLoad load, ImmutableArray<int> views, CancellationToken cancellationToken)
    {
        var data = new LoaderOutcome[page.ModelCount];
        var remote = services.GetService(typeof(ILoaderResults)) as ILoaderResults;
        var run = new Run(services, request, load, views.Length, cancellationToken);
        var gathered = new Gathered(data.Length);
        foreach (var view in views)
        {
            if (remote is null && PageLoader.Repeated(page, view))
            {
                continue;
            }

            gathered = gathered.Add(view, remote is null
                ? KeepViewAsync(page, PageLoader.LoaderAt(page, view)!, run, data, view)
                : KeepRemoteAsync(remote, page, request, data, view, cancellationToken));
        }

        return new SlotResults(data, await gathered.KeysAsync());
    }

    public static async ValueTask<ViewResult> LoadViewAsync(IServiceProvider services, PageDefinition page, PageRequest request, int slot, CancellationToken cancellationToken)
    {
        var data = new LoaderOutcome[page.ModelCount];
        var keys = await KeepViewAsync(page, PageLoader.LoaderAt(page, slot)!, new Run(services, request, PageLoad.For(services), 1, cancellationToken), data, slot);
        return new ViewResult(data[slot], keys.IsDefault ? [] : keys);
    }

    public static void Log(IServiceProvider services, PageDefinition page, int index, Exception error)
    {
        if (services.GetService(typeof(ILoggerFactory)) is ILoggerFactory logs)
        {
            ViewFailed(logs.CreateLogger("Aioros.Views"), page.Views[index - page.FirstView].Key, page.Route, error);
        }
    }

    private static async ValueTask<ImmutableArray<string>> KeepRemoteAsync(ILoaderResults remote, PageDefinition page, PageRequest request, LoaderOutcome[] data, int view, CancellationToken cancellationToken)
    {
        var answered = await remote.LoadViewAsync(page, request, view - page.FirstView, cancellationToken);
        data[view] = answered.Outcome;
        return answered.Keys;
    }

    private static async ValueTask<ImmutableArray<string>> KeepAsync(LoaderDefinition loader, Run run, LoaderOutcome[] data, int index)
    {
        if (run.Gate is { } gate)
        {
            await gate.EnterAsync(run.CancellationToken);
        }

        try
        {
            if (run.Scopes is null)
            {
                data[index] = await loader.LoadAsync(run.Services, run.Request, run.CancellationToken);
                return default;
            }

            await using var scope = run.Scopes.CreateAsyncScope();
            var own = new LoaderServices(run.Services, scope.ServiceProvider, run.Load, run.Request.User);
            data[index] = await loader.LoadAsync(own, run.Request, run.CancellationToken);
            return own.ReadAny ? own.Keys : default;
        }
        finally
        {
            run.Gate?.Leave();
        }
    }

    /// <summary>Runs a load in a scope of its own under the gate, as <see cref="KeepAsync"/> does, and returns its value and the keys it read.</summary>
    public static async ValueTask<(T Value, ImmutableArray<string> Keys)> ScopedAsync<T>(Run run, Func<IServiceProvider, PageRequest, CancellationToken, ValueTask<T>> load)
    {
        if (run.Gate is { } gate)
        {
            await gate.EnterAsync(run.CancellationToken);
        }

        try
        {
            if (run.Scopes is null)
            {
                return (await load(run.Services, run.Request, run.CancellationToken), default);
            }

            await using var scope = run.Scopes.CreateAsyncScope();
            var own = new LoaderServices(run.Services, scope.ServiceProvider, run.Load, run.Request.User);
            var value = await load(own, run.Request, run.CancellationToken);
            return (value, own.ReadAny ? own.Keys : default);
        }
        finally
        {
            run.Gate?.Leave();
        }
    }

    /// <summary>
    /// Loads an embedded view behind its error boundary: a visitor its loader's rules refuse gets the hidden marker without
    /// the loader running, a loader that answers not found or a redirect hides the view instead of stopping the page, and
    /// one that throws marks the view failed, logged, while the page renders. Its title and metadata are never merged:
    /// only the page and its layouts name the page.
    /// </summary>
    private static async ValueTask<ImmutableArray<string>> KeepViewAsync(PageDefinition page, LoaderDefinition loader, Run run, LoaderOutcome[] data, int index)
    {
        var cancellationToken = run.CancellationToken;
        var services = run.Services;
        try
        {
            if (!await ViewAccess.AllowsAsync(services, run.Request.User, page.Views[index - page.FirstView].View.Access, cancellationToken))
            {
                data[index] = Hidden;
                return default;
            }

            var keys = await KeepAsync(loader, run, data, index);
            if (data[index].Stops)
            {
                data[index] = Hidden;
                return default;
            }

            return keys;
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            Log(services, page, index, error);
            data[index] = Failed;
            return default;
        }
    }

    /// <summary>The loaders that start together: their page's services, scopes and request, the load they share and its gate.</summary>
    internal readonly struct Run(IServiceProvider services, PageRequest request, PageLoad load, int runs, CancellationToken cancellationToken)
    {
        public IServiceProvider Services { get; } = services;

        public IServiceScopeFactory? Scopes { get; } = services.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;

        public PageRequest Request { get; } = request;

        public PageLoad Load { get; } = load;

        public LoaderGate? Gate { get; } = load.Gate(runs);

        public CancellationToken CancellationToken { get; } = cancellationToken;
    }

    /// <summary>
    /// The runs of one load and the keys of those that completed: the keys array only once a run read a key, the list of
    /// pending runs only once one did not complete at once.
    /// </summary>
    private readonly struct Gathered(int count)
    {
        private readonly ImmutableArray<string>[]? _keys;

        private readonly List<(int Index, Task<ImmutableArray<string>> Run)>? _pending;

        private Gathered(int count, ImmutableArray<string>[]? keys, List<(int Index, Task<ImmutableArray<string>> Run)>? pending)
            : this(count) => (_keys, _pending) = (keys, pending);

        public Gathered Add(int index, ValueTask<ImmutableArray<string>> run)
        {
            if (run.IsCompletedSuccessfully)
            {
#pragma warning disable VSTHRD002
                return new Gathered(count, Note(_keys, index, run.Result, count), _pending);
#pragma warning restore VSTHRD002
            }

            var pending = _pending ?? [];
            pending.Add((index, run.AsTask()));
            return new Gathered(count, _keys, pending);
        }

        public async ValueTask<ImmutableArray<string>[]?> KeysAsync()
        {
            if (_pending is null)
            {
                return _keys;
            }

            await Task.WhenAll(_pending.Select(static pending => pending.Run));
            var keys = _keys;
            foreach (var (index, run) in _pending)
            {
                keys = Note(keys, index, await run, count);
            }

            return keys;
        }

        private static ImmutableArray<string>[]? Note(ImmutableArray<string>[]? keys, int index, ImmutableArray<string> read, int count)
        {
            if (read.IsDefaultOrEmpty)
            {
                return keys;
            }

            keys ??= new ImmutableArray<string>[count];
            keys[index] = read;
            return keys;
        }
    }
}

/// <summary>One view's loader result and the data keys it read, as the server answers the installed app for one view.</summary>
public readonly record struct ViewResult(LoaderOutcome Outcome, ImmutableArray<string> Keys);

/// <summary>The loader results of a page's slots, outermost first, and the data keys each read; default keys when none read any.</summary>
public readonly record struct LoaderResults(ImmutableArray<LoaderOutcome> Outcomes, ImmutableArray<ImmutableArray<string>> Keys);
