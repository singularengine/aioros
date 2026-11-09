using System.Reflection;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The current value of a binding through Avalonia's stream operator (<c>{Binding Title^}</c>), as its
/// <c>StreamNode</c> shows it at the time of a render: a task's result once it has completed, nothing before; an
/// observable's latest value among those it gives when subscribed, nothing when it gives none. The subscription ends
/// before the render goes on.
/// </summary>
internal static class StreamValues
{
    private static readonly MethodInfo LatestOf = typeof(StreamValues).GetMethod(nameof(Latest), BindingFlags.Public | BindingFlags.Static)!;

    public static Func<object, object?> Reader(Type type)
    {
        if (typeof(Task).IsAssignableFrom(type))
        {
            var result = type.GetProperty("Result");
            return result is null || !result.DeclaringType!.IsGenericType ? static _ => null : task => task is Task { IsCompletedSuccessfully: true } ? result.GetValue(task) : null;
        }

        var observable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IObservable<>) ? type
            : type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IObservable<>));
        return observable is null ? static _ => null : LatestOf.MakeGenericMethod(observable.GetGenericArguments()[0]).CreateDelegate<Func<object, object?>>();
    }

    /// <summary>
    /// The first stream member of a path (<c>Title^</c>) whose stream gives no value when it is read in this scope (an
    /// observable that gives none when subscribed, a task that has not completed), or nothing when each gives one.
    /// </summary>
    public static string? Silent(string path, object? scope)
    {
        for (var mark = path.StartsWith('^') ? -1 : path.IndexOf('^', StringComparison.Ordinal); mark > 0; mark = path.IndexOf('^', mark + 1))
        {
            if (PropertyPaths.Getter(path[..mark])(scope) is { } stream && !Gives(stream))
            {
                return path[..(mark + 1)];
            }
        }

        return null;
    }

    private static bool Gives(object stream)
    {
        if (stream is Task task)
        {
            return task.IsCompletedSuccessfully;
        }

        var observable = stream.GetType().GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IObservable<>));
        return observable is null || (bool)ProbeOf.MakeGenericMethod(observable.GetGenericArguments()[0]).Invoke(null, [stream])!;
    }

    private static readonly MethodInfo ProbeOf = typeof(StreamValues).GetMethod(nameof(Probe), BindingFlags.Public | BindingFlags.Static)!;

    public static bool Probe<T>(object source)
    {
        var observer = new LatestValue<T>();
        ((IObservable<T>)source).Subscribe(observer).Dispose();
        return observer.Given;
    }

    public static object? Latest<T>(object source)
    {
        var observer = new LatestValue<T>();
        ((IObservable<T>)source).Subscribe(observer).Dispose();
        return observer.Value;
    }

    private sealed class LatestValue<T> : IObserver<T>
    {
        public object? Value { get; private set; }

        public bool Given { get; private set; }

        public void OnNext(T value)
        {
            Value = value;
            Given = true;
        }

        public void OnError(Exception error) => Value = null;

        public void OnCompleted()
        {
        }
    }
}
