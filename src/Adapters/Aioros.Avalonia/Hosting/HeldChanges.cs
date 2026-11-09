namespace Aioros.Avalonia.Hosting;

internal sealed class HeldChanges
{
    private readonly Queue<(Action Change, TaskCompletionSource Applied)> _waiting = new();
    private int _holds;

    public Task ApplyAsync(Action change)
    {
        if (_holds == 0)
        {
            change();
            return Task.CompletedTask;
        }

        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting.Enqueue((change, applied));
        return applied.Task;
    }

    public T While<T>(Func<T> body)
    {
        _holds++;
        try
        {
            return body();
        }
        finally
        {
            if (--_holds == 0)
            {
                Release();
            }
        }
    }

    private void Release()
    {
        while (_holds == 0 && _waiting.TryDequeue(out var held))
        {
            try
            {
                held.Change();
                held.Applied.SetResult();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                held.Applied.SetException(error);
            }
        }
    }
}
