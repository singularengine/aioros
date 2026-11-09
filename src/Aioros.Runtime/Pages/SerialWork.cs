using System.Threading.Channels;

namespace Aioros.Runtime.Pages;

public sealed class SerialWork
{
    private readonly Channel<Func<Task>> _work = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });

    public SerialWork() => _ = RunAsync();

    public ValueTask InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync(
            () =>
            {
                action();
                return ValueTask.CompletedTask;
            },
            cancellationToken);
    }

    public async ValueTask InvokeAsync(Func<ValueTask> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Writer.TryWrite(async () =>
        {
            try
            {
                await work();
                done.TrySetResult();
            }
            catch (Exception error)
            {
                done.TrySetException(error);
            }
        });
        await done.Task.WaitAsync(cancellationToken);
    }

    private async Task RunAsync()
    {
        await foreach (var work in _work.Reader.ReadAllAsync())
        {
            await work();
        }
    }
}
