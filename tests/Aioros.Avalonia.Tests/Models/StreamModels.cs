namespace Aioros.Avalonia.Tests.Models;

public sealed class Headline(string? title, string note, bool silent = false)
{
    public static Headline First => new("Hello", "first note");

    public static Headline Second => new("World", "second note");

    public static Headline Blank => new(null, "blank note");

    public static Headline Quiet => new(null, "quiet note", silent: true);

    public Feed<string?> Title { get; } = new(title, silent);

    public Task<string> Note { get; } = Task.FromResult(note);
}

public sealed class Feed<T>(T value, bool silent) : IObservable<T>
{
    public int Open { get; private set; }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        Open++;
        if (!silent)
        {
            observer.OnNext(value);
        }

        return new Closing(this);
    }

    private sealed class Closing(Feed<T> stream) : IDisposable
    {
        public void Dispose() => stream.Open--;
    }
}

public sealed class Loud : global::Avalonia.Data.Converters.IValueConverter
{
    public static Loud Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is string { Length: > 0 } text ? text.ToUpper(culture) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
