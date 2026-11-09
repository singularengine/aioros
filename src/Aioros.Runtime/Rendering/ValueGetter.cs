namespace Aioros.Runtime.Rendering;

public delegate object? ValueGetter(object? scope);

public delegate string ValueFormatter(object? value);

/// <summary>
/// Formats a value into <paramref name="destination"/> without a string, as its <see cref="ValueFormatter"/> would, or returns
/// false when it cannot (the value needs the formatter, or the text does not fit), and the formatter writes it instead.
/// </summary>
public delegate bool ValueSpanFormatter(object? value, Span<char> destination, out int written);

/// <summary>The markup of a raw value slot as UTF-8 bytes encoded once, written without transcoding it on every render.</summary>
public delegate ReadOnlySpan<byte> ValueUtf8Formatter(object? value);
