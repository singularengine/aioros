using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;

namespace Aioros.Hosting;

/// <summary>
/// The page engine started when a page first needs it: right away in development, and in production only on a page or a
/// post the plans file does not cover, so a precompiled app never loads Avalonia. Work sent to the UI thread before it
/// starts runs in order on serial work.
/// </summary>
public sealed class DeferredPageEngine(Func<Task<IPageEngine>> start) : IPageEngine, IPageCapture, IUiThread, IModelThread, IEngineStart
{
    private readonly Runtime.Pages.SerialWork _work = new();

    private Task<IPageEngine>? _engine;

    private Task _dispatched = Task.CompletedTask;

    public bool Started => Volatile.Read(ref _engine) is not null;

    public Task<IPageEngine> Engine => Volatile.Read(ref _engine) ?? BeginAsync();

    public static DeferredPageEngine Start(Func<Task<IPageEngine>> start)
    {
        var engine = new DeferredPageEngine(start);
        _ = engine.Engine;
        return engine;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken) => await Engine.WaitAsync(cancellationToken);

    public async ValueTask<CompiledPage> RenderAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
        await (await Engine).RenderAsync(page, models, theme, cancellationToken);

    public async ValueTask<CompiledPage> AnswerAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
        await (await Engine).AnswerAsync(page, models, theme, cancellationToken);

    public async ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, CancellationToken cancellationToken) =>
        await (await Engine).PostAsync(page, models, form, files, theme, cancellationToken);

    public async ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, bool planAnswers, CancellationToken cancellationToken) =>
        await (await Engine).PostAsync(page, models, form, files, theme, planAnswers, cancellationToken);

    /// <summary>Once the engine has started, Avalonia runs in the process and its posts' commands run through it; before, the plans run them without Avalonia.</summary>
    public bool OwnsCommands => Volatile.Read(ref _engine) is { IsCompletedSuccessfully: true };

    /// <summary>A command the plan cannot run anywhere starts the engine, as a page the plans file does not cover does.</summary>
    public bool RunsCommands => true;

    public async ValueTask<PageCapture> CaptureAsync(PageDefinition page, ImmutableArray<object?> models, CaptureOptions options, CancellationToken cancellationToken) =>
        await ((IPageCapture)await Engine).CaptureAsync(page, models, options, cancellationToken);

    /// <summary>
    /// Creates the page's models on the engine's thread once the engine runs; before it starts (a production app whose
    /// plans cover its pages), on the caller's, so that loading a page that only redirects never starts Avalonia.
    /// </summary>
    public async ValueTask<T> LoadAsync<T>(PageDefinition page, Func<ValueTask<T>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);
        return Volatile.Read(ref _engine) is { } engine && await engine is IModelThread thread
            ? await thread.LoadAsync(page, load, cancellationToken)
            : await load();
    }

    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        Task? dispatched = null;
        await _work.InvokeAsync(
            () =>
            {
                if (Volatile.Read(ref _engine) is { } engine)
                {
                    dispatched = _dispatched = DispatchAsync(_dispatched, engine, action, cancellationToken);
                }
                else
                {
                    action();
                }
            },
            cancellationToken);
        if (dispatched is not null)
        {
            await dispatched;
        }
    }

    private static async Task DispatchAsync(Task previous, Task<IPageEngine> engine, Action action, CancellationToken cancellationToken)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await ((IUiThread)await engine).InvokeAsync(action, cancellationToken);
    }

    private Task<IPageEngine> BeginAsync()
    {
        var started = new TaskCompletionSource<IPageEngine>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _engine, started.Task, null) is { } existing)
        {
            return existing;
        }

        _ = RunAsync(started);
        return started.Task;
    }

    private async Task RunAsync(TaskCompletionSource<IPageEngine> started)
    {
        try
        {
            await _work.InvokeAsync(static () => { }, CancellationToken.None);
            started.TrySetResult(await start());
        }
        catch (Exception error)
        {
            started.TrySetException(error);
        }
    }
}
