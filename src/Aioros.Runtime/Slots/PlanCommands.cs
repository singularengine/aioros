using System.Collections.Immutable;
using System.Windows.Input;

namespace Aioros.Runtime.Slots;

/// <summary>
/// How a command type is run and awaited, generated at build time: an awaitable <c>ExecuteAsync(object?)</c>, an
/// <c>Execute(TParam)</c> returning an <see cref="IObservable{T}"/>, or a plain <see cref="ICommand.Execute"/>. An
/// exact shape matches only its type; another matches every command that is one (an interface or a base class).
/// </summary>
public sealed record CommandShape(Type Type, Func<ICommand, object?, ValueTask> Run)
{
    public bool Exact { get; init; }
}

/// <summary>
/// Runs a view model's command and awaits it with the app's generated shapes, the first that matches its run-time type,
/// or with the shape the fallback finds for a type no generated shape covers.
/// </summary>
public static class PlanCommands
{
    private static ImmutableList<CommandShape> _shapes = [];

    private static ImmutableDictionary<Type, Func<ICommand, object?, ValueTask>?> _found = ImmutableDictionary<Type, Func<ICommand, object?, ValueTask>?>.Empty;

    public static void Register(ImmutableArray<CommandShape> shapes)
    {
        if (shapes.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableInterlocked.Update(ref _shapes, static (all, added) => all.AddRange(added), shapes);
        Volatile.Write(ref _found, ImmutableDictionary<Type, Func<ICommand, object?, ValueTask>?>.Empty);
    }

    /// <summary>Runs the command with the parameter when it can execute; the task ends when the command has ended.</summary>
    public static ValueTask RunAsync(ICommand? command, object? parameter, Func<Type, Func<ICommand, object?, ValueTask>>? fallback)
    {
        if (command is null || !command.CanExecute(parameter))
        {
            return ValueTask.CompletedTask;
        }

        if (command is MethodCommand method)
        {
            return method.ExecuteAsync(parameter);
        }

        var type = command.GetType();
        var run = Find(type) ?? fallback?.Invoke(type) ?? throw new InvalidOperationException($"No generated shape runs {type.FullName} and no fallback finds one.");
        return run(command, parameter);
    }

    /// <summary>The generated shape that runs a command type: its exact one, or the first that the type is one of.</summary>
    public static Func<ICommand, object?, ValueTask>? Find(Type type) => ImmutableInterlocked.GetOrAdd(ref _found, type, static type =>
    {
        var shapes = Volatile.Read(ref _shapes);
        return (shapes.FirstOrDefault(shape => shape.Exact && shape.Type == type) ?? shapes.FirstOrDefault(shape => !shape.Exact && shape.Type.IsAssignableFrom(type)))?.Run;
    });

    /// <summary>Awaits a command's <c>ValueTask&lt;T&gt;</c>, for a generated shape.</summary>
    public static async ValueTask AwaitAsync<T>(ValueTask<T> pending) => await pending;

    /// <summary>Ends when an observable a command returned completes, or fails with its error, for a generated shape.</summary>
    public static ValueTask ObserveAsync<T>(IObservable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var completion = new Completion<T>();
        source.Subscribe(completion);
        return new ValueTask(completion.Task);
    }

    private sealed class Completion<T> : IObserver<T>
    {
        private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Task => _ended.Task;

        public void OnCompleted() => _ended.TrySetResult();

        public void OnError(Exception error) => _ended.TrySetException(error);

        public void OnNext(T value)
        {
        }
    }
}
