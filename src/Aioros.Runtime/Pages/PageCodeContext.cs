using Aioros.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aioros.Runtime.Pages;

/// <summary>
/// The synchronization context a page's own code runs under on a request's thread, which has none: an <c>async void</c>
/// method a view model starts reports its exception here instead of to the thread pool, where it would end the process.
/// What it reports while the code runs is rethrown when the code returns, so that request fails (500); what it reports
/// later is logged and the server keeps serving. Code already running under a context (Avalonia's thread, where the
/// engine and the desktop create models) keeps it. A context no <c>async void</c> method outlived is kept for the
/// thread's next page, so a page whose code starts none allocates nothing. Precedent: ASP.NET's
/// <c>AspNetSynchronizationContext</c> and Blazor's <c>RendererSynchronizationContext</c>, which both catch what an
/// <c>async void</c> handler throws.
/// </summary>
internal sealed partial class PageCodeContext : SynchronizationContext
{
    [ThreadStatic]
    private static PageCodeContext? t_idle;

    private readonly int _thread = Environment.CurrentManagedThreadId;
    private string _page = string.Empty;
    private ILogger _logger = NullLogger.Instance;
    private bool _open;
    private int _running;
    private List<(SendOrPostCallback Callback, object? State)>? _held;

    private bool Pinned { get; set; }

    public static void Run<TState>(IServiceProvider services, PageDefinition page, TState state, Action<TState> code)
    {
        if (Current is not null)
        {
            code(state);
            return;
        }

        var context = t_idle ?? new PageCodeContext();
        t_idle = null;
        context._page = page.Route;
        Volatile.Write(ref context._open, true);
        SetSynchronizationContext(context);
        Exception? failure;
        try
        {
            code(state);
        }
        finally
        {
            SetSynchronizationContext(null);
            failure = context.Release(services);
            if (!context.Pinned)
            {
                t_idle = context;
            }
        }

        if (failure is not null)
        {
            throw new InvalidOperationException($"The code of {page.Route} failed while its models were created: {failure.Message}", failure);
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (Environment.CurrentManagedThreadId == _thread && Volatile.Read(ref _open))
        {
            (_held ??= []).Add((d, state));
            return;
        }

        ThreadPool.UnsafeQueueUserWorkItem(static work => work.Context.RunAfterAnswer(work.Callback, work.State), (Context: this, Callback: d, State: state), preferLocal: false);
    }

    public override void OperationStarted() => Interlocked.Increment(ref _running);

    public override void OperationCompleted() => Interlocked.Decrement(ref _running);

    public override SynchronizationContext CreateCopy() => this;

    private Exception? Release(IServiceProvider services)
    {
        Exception? first = null;
        for (var index = 0; _held is { } held && index < held.Count; index++)
        {
            try
            {
                held[index].Callback(held[index].State);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                first ??= error;
            }
        }

        _held?.Clear();
        Volatile.Write(ref _open, false);
        if (Volatile.Read(ref _running) > 0)
        {
            _logger = services.GetService(typeof(ILoggerFactory)) is ILoggerFactory logs ? logs.CreateLogger("Aioros.Pages") : NullLogger.Instance;
            Pinned = true;
        }

        return first;
    }

    private void RunAfterAnswer(SendOrPostCallback callback, object? state)
    {
        try
        {
            callback(state);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            FailedAfterAnswer(_logger, _page, error);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The code of {Page} failed after its answer; the server keeps serving")]
    private static partial void FailedAfterAnswer(ILogger logger, string page, Exception error);
}
