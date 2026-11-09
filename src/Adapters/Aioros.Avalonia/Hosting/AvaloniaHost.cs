using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace Aioros.Avalonia.Hosting;

public sealed class AvaloniaHost : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PageFaults _faults;
    private readonly EngineJobs _jobs = new();

    private AvaloniaHost(ILogger logger)
    {
        _faults = new PageFaults(logger);
    }

    /// <summary>
    /// Starts Avalonia's headless platform on a thread of its own. With <paramref name="web"/>, every XAML it loads is
    /// loaded for the web, so <c>a:OnPlatform</c> chooses its <c>Web</c> value; a process renders either for the web or
    /// for the desktop. What a page's code throws on Avalonia's thread outside the call that ran it fails only that page's
    /// job, or is written to <paramref name="logger"/> once the page has answered; it never ends the process.
    /// </summary>
    public static async Task<AvaloniaHost> StartAsync(Type applicationType, bool web = false, ILogger? logger = null)
    {
        if (web)
        {
            Controls.XamlPlatform.UseWeb();
        }

        var host = new AvaloniaHost(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => host.Run(applicationType, ready)) { IsBackground = true, Name = "Aioros UI" };
        thread.Start();
        await ready.Task;
        return host;
    }

    /// <summary>Whether the thread holds an Avalonia dispatcher, which the first Avalonia object it creates gives it.</summary>
    public static bool HasDispatcher(Thread thread) => Dispatcher.FromThread(thread) is not null;

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken) => InvokeAsync(null, action, cancellationToken);

    public Task<T> InvokeAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) => InvokeAsync(null, action, cancellationToken);

    /// <summary>
    /// Runs a page's job on Avalonia's thread in the culture and UI culture of its caller (the visitor's, which ASP.NET
    /// Core's request localization chose), Avalonia's thread taking its own back afterwards; what the page's code throws
    /// there while it runs fails the job.
    /// </summary>
    public async Task<T> InvokeAsync<T>(string? page, Func<T> action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_stop.IsCancellationRequested, this);
        var caller = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var cancelled = cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));
        var queued = _jobs.Enqueue(() =>
        {
            if (done.Task.IsCompleted)
            {
                return;
            }

            try
            {
                done.TrySetResult(InCulture(caller, () => _faults.Run(page, action)));
            }
            catch (Exception error)
            {
                done.TrySetException(error);
            }
        }, Aioros.Runtime.Rendering.BackgroundWork.IsRunning);
        if (!queued)
        {
            throw new Aioros.Runtime.Rendering.EngineBusyException();
        }

        return await done.Task.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    /// <summary>Runs a page's asynchronous job on Avalonia's thread in its caller's culture, its continuations included; what the page's code throws there while it runs fails the job.</summary>
    public async Task<T> InvokeAsync<T>(string? page, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_stop.IsCancellationRequested, this);
        cancellationToken.ThrowIfCancellationRequested();
        var caller = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        var started = new TaskCompletionSource<Task<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = _jobs.Enqueue(() =>
        {
            try
            {
                started.SetResult(_jobs.RunAsync(() => InCultureAsync(caller, () => _faults.RunAsync(page, action))));
            }
            catch (Exception error)
            {
                started.SetException(error);
            }
        }, Aioros.Runtime.Rendering.BackgroundWork.IsRunning);
        if (!queued)
        {
            throw new Aioros.Runtime.Rendering.EngineBusyException();
        }

        return await (await started.Task.ConfigureAwait(false)).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    private static T InCulture<T>((CultureInfo Culture, CultureInfo Ui) caller, Func<T> action)
    {
        var own = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = caller;
        try
        {
            return action();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = own;
        }
    }

    /// <summary>Runs an asynchronous job in the caller's culture; the culture set in this method flows to its continuations and is undone for the thread when it returns.</summary>
    private static async Task<T> InCultureAsync<T>((CultureInfo Culture, CultureInfo Ui) caller, Func<Task<T>> action)
    {
        (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = caller;
        return await action();
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        Dispatcher.UIThread.Post(static () => { });
        await _stopped.Task;
        _stop.Dispose();
    }

    private void Run(Type applicationType, TaskCompletionSource ready)
    {
        if (Dispatcher.UIThread is { } taken && !taken.CheckAccess())
        {
            ready.SetException(new InvalidOperationException(
                $"Avalonia's UI dispatcher belongs to thread {taken.Thread.ManagedThreadId} ('{taken.Thread.Name ?? "unnamed"}'), not to Aioros's engine: a page's model created an Avalonia object outside Avalonia's thread before the engine started, and the first Avalonia object a thread creates takes Avalonia's UI dispatcher for it. "
                + "Create such objects only in models the generator marks (ModelsUseAvalonia), which are created on Avalonia's thread, or keep them out of the models (a path's data as a string, an image as its URI)."));
            return;
        }

        try
        {
            AppBuilder.Configure(() => (Application)Activator.CreateInstance(applicationType)!)
                .UseSkia()
                .UseHarfBuzz()
                .WithInterFont()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ready.SetException(error);
            return;
        }

        Dispatcher.UIThread.UnhandledException += _faults.Contain;
        SystemFaces.Load(Application.Current!);
        ready.SetResult();
        Dispatcher.UIThread.MainLoop(_stop.Token);
        _stopped.SetResult();
    }
}
