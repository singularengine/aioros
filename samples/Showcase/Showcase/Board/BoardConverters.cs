using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Showcase.Board;

/// <summary>The priority's color.</summary>
public sealed class PriorityBrush : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        Priority.Urgent => new SolidColorBrush(Color.Parse("#E5484D")),
        Priority.High => new SolidColorBrush(Color.Parse("#F76B15")),
        Priority.Medium => new SolidColorBrush(Color.Parse("#5B5BD6")),
        _ => new SolidColorBrush(Color.Parse("#8B8D98")),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A due date as people say it: "due today", "due in 3 days", "2 days late".</summary>
public sealed class DueText : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateOnly due)
        {
            return "No date";
        }

        var days = due.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        return days switch
        {
            0 => "Due today",
            1 => "Due tomorrow",
            > 1 => string.Create(culture, $"Due in {days} days"),
            -1 => "1 day late",
            _ => string.Create(culture, $"{-days} days late"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Whether a due date has passed.</summary>
public sealed class IsLate : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly due && due < DateOnly.FromDateTime(DateTime.Today);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Two initials of a name.</summary>
public sealed class Initials : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } name ? name[..Math.Min(2, name.Length)].ToUpper(culture) : "?";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A count with its noun, singular or plural: "1 report", "4 reports". The noun is the parameter.</summary>
public sealed class Counted : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count ? string.Create(culture, $"{count} {parameter}{(count == 1 ? string.Empty : "s")}") : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
