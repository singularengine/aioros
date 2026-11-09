using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Aioros.Avalonia.Commands;

/// <summary>
/// Runs a view model's command and awaits it, whatever its toolkit: an awaitable <c>ExecuteAsync(object?)</c>
/// (CommunityToolkit, Prism), an <c>Execute(TParam)</c> that returns an <see cref="IObservable{T}"/> (ReactiveUI), or
/// a plain <see cref="ICommand"/>. The app's generated shapes run it when one matches; otherwise the shape is found by
/// reflection once per command type and kept.
/// </summary>
public static class AwaitedCommands
{
    private static ImmutableDictionary<Type, Func<ICommand, object?, ValueTask>> _runs =
        ImmutableDictionary<Type, Func<ICommand, object?, ValueTask>>.Empty;

    /// <summary>Runs the command with the parameter when it can execute; the task ends when the command has ended.</summary>
    public static ValueTask RunAsync(ICommand? command, object? parameter) => Runtime.Slots.PlanCommands.RunAsync(command, parameter, Shape);

    /// <summary>The shape of a command type found by reflection, for a type no generated shape covers.</summary>
    public static Func<ICommand, object?, ValueTask> Shape(Type type) => ImmutableInterlocked.GetOrAdd(ref _runs, type, Compile);

    private static Func<ICommand, object?, ValueTask> Compile(Type type) => Awaitable(type) ?? Observable(type) ?? PlainAsync;

    private static ValueTask PlainAsync(ICommand command, object? parameter)
    {
        command.Execute(parameter);
        return ValueTask.CompletedTask;
    }

    private static Func<ICommand, object?, ValueTask>? Awaitable(Type type)
    {
        var method = type.GetMethods()
            .Concat(type.GetInterfaces().SelectMany(contract => contract.GetMethods()))
            .FirstOrDefault(candidate => candidate is { Name: "ExecuteAsync", IsStatic: false }
                && candidate.GetParameters() is [{ ParameterType: var parameter }] && parameter == typeof(object)
                && AsValueTask(candidate.ReturnType) is not null);
        return method is null ? null : Lambda((command, argument) => AsValueTask(method.ReturnType)!(Expression.Call(Expression.Convert(command, method.DeclaringType!), method, argument)));
    }

    private static Func<ICommand, object?, ValueTask>? Observable(Type type)
    {
        var (method, element) = type.GetMethods()
            .Where(candidate => candidate is { Name: "Execute", IsStatic: false } && candidate.GetParameters().Length == 1)
            .Select(candidate => (Method: candidate, Element: Observed(candidate.ReturnType)))
            .FirstOrDefault(candidate => candidate.Element is not null);
        if (method is null)
        {
            return null;
        }

        var parameter = method.GetParameters()[0].ParameterType;
        var observe = Generic<IObservable<object>>(ObserveAsync, element!);
        return Lambda((command, argument) =>
        {
            var value = Expression.Condition(Expression.Equal(argument, Expression.Constant(null)), Expression.Default(parameter), Expression.Convert(argument, parameter));
            return Expression.Call(observe, Expression.Call(Expression.Convert(command, type), method, value));
        });
    }

    private static Type? Observed(Type returned) =>
        returned.GetInterfaces().Prepend(returned)
            .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IObservable<>))?
            .GetGenericArguments()[0];

    private static Func<Expression, Expression>? AsValueTask(Type returned)
    {
        if (returned == typeof(ValueTask))
        {
            return static call => call;
        }

        if (typeof(Task).IsAssignableFrom(returned))
        {
            return static call => Expression.New(typeof(ValueTask).GetConstructor([typeof(Task)])!, call);
        }

        if (returned.IsGenericType && returned.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var awaiter = Generic<ValueTask<object>>(AwaitAsync, returned.GetGenericArguments()[0]);
            return call => Expression.Call(awaiter, call);
        }

        return null;
    }

    private static Func<ICommand, object?, ValueTask> Lambda(Func<Expression, Expression, Expression> body)
    {
        var command = Expression.Parameter(typeof(ICommand));
        var argument = Expression.Parameter(typeof(object));
        return Expression.Lambda<Func<ICommand, object?, ValueTask>>(body(command, argument), command, argument)
            .Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled);
    }

    private static MethodInfo Generic<T>(Func<T, ValueTask> method, Type argument) =>
        method.Method.GetGenericMethodDefinition().MakeGenericMethod(argument);

    private static async ValueTask AwaitAsync<T>(ValueTask<T> pending) => await pending;

    private static ValueTask ObserveAsync<T>(IObservable<T> source)
    {
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
