using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// Contains what a page's own code throws on Avalonia's thread outside the call that ran it. An <c>async void</c> handler
/// in code-behind (or a view model created there) reports its exception to Avalonia's synchronization context, which
/// throws it from a later dispatcher job; unhandled, it would fail whichever page that job renders or, from the main
/// loop, end the process. Avalonia's dispatcher raises <see cref="Dispatcher.UnhandledException"/> for it, as WPF's
/// does: the exception is marked handled and charged to the page whose job started that code, found through the
/// execution context the code captured. While that job runs, the job fails with it (its request is answered 500); once
/// the job has answered, it is logged. Blazor Server likewise fails only the circuit whose code threw.
/// </summary>
internal sealed partial class PageFaults(ILogger logger)
{
    private const string Outside = "code outside any page";

    private readonly AsyncLocal<PageRun?> _run = new();

    public T Run<T>(string? page, Func<T> body)
    {
        var run = Begin(page);
        try
        {
            var result = body();
            run.ThrowIfFaulted();
            return result;
        }
        finally
        {
            run.Answered = true;
        }
    }

    public async Task<T> RunAsync<T>(string? page, Func<Task<T>> body)
    {
        var run = Begin(page);
        try
        {
            var result = await body();
            run.ThrowIfFaulted();
            return result;
        }
        finally
        {
            run.Answered = true;
        }
    }

    public void Contain(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (_run.Value is { Answered: false } run)
        {
            run.Fault ??= e.Exception;
            return;
        }

        FailedAfterAnswer(logger, _run.Value?.Page ?? Outside, e.Exception);
    }

    private PageRun Begin(string? page)
    {
        var run = new PageRun(page ?? Outside);
        _run.Value = run;
        return run;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The code of {Page} failed on Avalonia's thread after its answer; the server keeps serving")]
    private static partial void FailedAfterAnswer(ILogger logger, string page, Exception error);

    private sealed class PageRun(string page)
    {
        public string Page { get; } = page;

        public bool Answered { get; set; }

        public Exception? Fault { get; set; }

        public void ThrowIfFaulted()
        {
            if (Fault is { } fault)
            {
                throw new InvalidOperationException($"The code of {Page} failed on Avalonia's thread while it ran: {fault.Message}", fault);
            }
        }
    }
}
