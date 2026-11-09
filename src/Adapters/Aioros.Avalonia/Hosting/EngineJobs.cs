using System.Threading.Channels;
using Avalonia.Threading;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The engine's own queue of jobs on Avalonia's thread, apart from Avalonia's dispatcher queue, as React's scheduler keeps
/// its tasks apart from the browser's event loop: a job starts only when no engine job is running on the thread, so a pump
/// of the dispatcher inside a job (a settle, a probe, a post's binding) runs only the work Avalonia posted, never another
/// page's compile or another request's job; requests' jobs are taken before background work. An asynchronous job holds
/// the thread only while one of its continuations runs, so a job awaiting its prompt lets the others run meanwhile.
/// The running state is owned by Avalonia's thread alone; other threads only write to the channels.
/// </summary>
internal sealed class EngineJobs
{
    /// <summary>The most requests' jobs that wait on the engine's thread; one more is refused (the security audit's F3).</summary>
    public const int RequestCapacity = 256;

    private readonly Channel<Action> _requests = Channel.CreateBounded<Action>(new BoundedChannelOptions(RequestCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Channel<Action> _background = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
    private int _running;
    private int _owed;

    /// <summary>
    /// Queues a job and the dispatcher job that starts it, below the requests' when it is background work; whether it was
    /// queued, a request's job being refused while <see cref="RequestCapacity"/> of them wait.
    /// </summary>
    public bool Enqueue(Action job, bool background)
    {
        if (!(background ? _background : _requests).Writer.TryWrite(job))
        {
            return false;
        }

        Dispatcher.UIThread.Post(Next, background ? DispatcherPriority.Background : DispatcherPriority.Default);
        return true;
    }

    /// <summary>Runs a part of an engine job: while it runs, the jobs the dispatcher would start wait until it has returned.</summary>
    public void Run(Action part)
    {
        _running++;
        try
        {
            part();
        }
        finally
        {
            if (--_running == 0 && _owed > 0)
            {
                for (; _owed > 0; _owed--)
                {
                    Dispatcher.UIThread.Post(Next, DispatcherPriority.Default);
                }
            }
        }
    }

    /// <summary>Runs an asynchronous job's start with its continuations posted through <see cref="Run"/>.</summary>
    public Task<T> RunAsync<T>(Func<Task<T>> job)
    {
        var outer = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new JobContext(this));
        try
        {
            return job();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(outer);
        }
    }

    private void Next()
    {
        if (_running > 0)
        {
            _owed++;
            return;
        }

        if (_requests.Reader.TryRead(out var job) || _background.Reader.TryRead(out job))
        {
            Run(job);
        }
    }

    /// <summary>The synchronization context of an asynchronous job: Avalonia's dispatcher, each continuation run as a part of the job.</summary>
    private sealed class JobContext(EngineJobs jobs) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            Dispatcher.UIThread.Post(_ => jobs.Run(() => Continue(d, state)), null, DispatcherPriority.Default);

        public override void Send(SendOrPostCallback d, object? state) =>
            Dispatcher.UIThread.Invoke(() => jobs.Run(() => Continue(d, state)), DispatcherPriority.Send);

        public override SynchronizationContext CreateCopy() => this;

        private void Continue(SendOrPostCallback d, object? state)
        {
            var outer = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(outer);
            }
        }
    }
}
