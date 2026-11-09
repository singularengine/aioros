using System.Threading.Tasks.Sources;
using System.Windows.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Step
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<object?> Calls { get; } = [];

    public bool Ended { get; private set; }

    public Task Started => _started.Task;

    public void Release() => _gate.SetResult();

    public void Fail(Exception exception) => _gate.SetException(exception);

    public async Task RunAsync(object? parameter)
    {
        Calls.Add(parameter);
        _started.TrySetResult();
        await _gate.Task;
        Ended = true;
    }
}

public interface IAsyncCommand : ICommand
{
    Task ExecuteAsync(object? parameter);
}

public sealed class PrismShapedCommand(Step step) : IAsyncCommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

#pragma warning disable VSTHRD100
    public async void Execute(object? parameter) => await ((IAsyncCommand)this).ExecuteAsync(parameter);
#pragma warning restore VSTHRD100

    Task IAsyncCommand.ExecuteAsync(object? parameter) => step.RunAsync(parameter);
}

public sealed class ValueTaskCommand(Step step) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _ = step.RunAsync(parameter);

    public ValueTask ExecuteAsync(object? parameter) => new(step.RunAsync(parameter));
}

public sealed class ReactiveShapedCommand<TParam, TResult>(Func<TParam, Task<TResult>> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public int Subscriptions { get; private set; }

    public List<TResult> Results { get; } = [];

    public bool CanExecute(object? parameter) => true;

    public IObservable<TResult> Execute(TParam parameter) => new Deferred(this, parameter);

    public IObservable<TResult> Execute() => Execute(default!);

    void ICommand.Execute(object? parameter) => (parameter is null ? Execute() : Execute((TParam)parameter)).Subscribe(new Ignored());

    private Task<TResult> RunAsync(TParam parameter) => execute(parameter);

    private sealed class Deferred(ReactiveShapedCommand<TParam, TResult> command, TParam parameter) : IObservable<TResult>
    {
        public IDisposable Subscribe(IObserver<TResult> observer)
        {
            command.Subscriptions++;
            _ = RunAsync(observer);
            return new Ignored();
        }

        private async Task RunAsync(IObserver<TResult> observer)
        {
            try
            {
                var result = await command.RunAsync(parameter);
                command.Results.Add(result);
                observer.OnNext(result);
            }
#pragma warning disable CA1031
            catch (Exception exception)
#pragma warning restore CA1031
            {
                observer.OnError(exception);
                return;
            }

            observer.OnCompleted();
        }
    }

    private sealed class Ignored : IObserver<TResult>, IDisposable
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(TResult value)
        {
        }

        public void Dispose()
        {
        }
    }
}

public sealed class PlainCommand(Action<object?> execute, bool enabled = true) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => enabled;

    public void Execute(object? parameter) => execute(parameter);
}

public sealed class Later
{
    public Later(Step step) => SaveCommand = new PrismShapedCommand(step);

    public Later(ICommand command) => SaveCommand = command;

    public ICommand SaveCommand { get; }

    public string Token { get; } = "note";
}

public sealed class Signal : IValueTaskSource
{
    private readonly TaskCompletionSource _awaited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ManualResetValueTaskSourceCore<bool> _core = new() { RunContinuationsAsynchronously = true };

    public List<object?> Calls { get; } = [];

    public bool Ended { get; private set; }

    public Task Awaited => _awaited.Task;

    public ValueTask WaitAsync(object? parameter)
    {
        Calls.Add(parameter);
        return new ValueTask(this, _core.Version);
    }

    public void Release()
    {
        Ended = true;
        _core.SetResult(true);
    }

    public void GetResult(short token) => _core.GetResult(token);

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _awaited.TrySetResult();
        _core.OnCompleted(continuation, state, token, flags);
    }
}

public sealed class SignalledCommand(Signal signal) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => signal.Calls.Add(parameter);

    public ValueTask ExecuteAsync(object? parameter) => signal.WaitAsync(parameter);
}
