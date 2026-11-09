using System.Globalization;
using System.Text.RegularExpressions;
using Aioros.Markup;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Aioros.Targets.Desktop.Motion;

/// <summary>
/// Counts a text marked <c>a:Web.CountUp</c> up to its number, once, the first time its page lays it out, as the web
/// counts it (DESKTOP-102): an Avalonia animation from zero to the value, easing out, each step written in the text's own
/// format, ending on exactly its text.
/// </summary>
internal sealed partial class Counts
{
    private static readonly AttachedProperty<double> ValueProperty = AvaloniaProperty.RegisterAttached<Counts, TextBlock, double>("Value");

    private static readonly AttachedProperty<bool> StartedProperty = AvaloniaProperty.RegisterAttached<Counts, TextBlock, bool>("Started");

    private static readonly AttachedProperty<Figure?> FigureProperty = AvaloniaProperty.RegisterAttached<Counts, TextBlock, Figure?>("Figure");

    static Counts() => ValueProperty.Changed.AddClassHandler<TextBlock>(static (text, change) =>
    {
        if (text.GetValue(FigureProperty) is { } figure)
        {
            text.SetCurrentValue(TextBlock.TextProperty, figure.Format(change.GetNewValue<double>()));
        }
    });

    private Counts()
    {
    }

    public static void Attach(Window window) =>
        window.LayoutUpdated += (_, _) =>
        {
            foreach (var text in window.GetLogicalDescendants().OfType<TextBlock>().Where(text => Web.GetCountUp(text) > 0 && !text.GetValue(StartedProperty) && text.Bounds.Width > 0))
            {
                Start(text);
            }
        };

    private static void Start(TextBlock text)
    {
        text.SetValue(StartedProperty, true);
        if (Figure.Parse(text.Text) is not { } figure)
        {
            return;
        }

        text.SetValue(FigureProperty, figure);
        var count = new Animation
        {
            Duration = TimeSpan.FromSeconds(Web.GetCountUp(text)),
            Easing = new ExponentialEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ValueProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ValueProperty, figure.Value) } },
            },
        };
        _ = CountAsync(text, count, figure);
    }

    private static async Task CountAsync(TextBlock text, Animation count, Figure figure)
    {
        await count.RunAsync(text);
        text.SetValue(FigureProperty, null);
        text.SetCurrentValue(TextBlock.TextProperty, figure.Text);
    }

    /// <summary>A number inside a text: what comes before and after it, its grouping and decimal separators and its decimals.</summary>
    private sealed partial record Figure(string Text, string Prefix, string Suffix, string Group, string Decimal, int Decimals, double Value)
    {
        public static Figure? Parse(string? text)
        {
            if (text is null || Number().Match(text) is not { Success: true } match)
            {
                return null;
            }

            var body = match.Groups[2].Value;
            var separators = Digits().Replace(body, string.Empty);
            var (group, point) = (string.Empty, string.Empty);
            if (separators.Length > 0)
            {
                var last = separators[^1].ToString();
                var distinct = separators.Distinct().ToList();
                if (distinct.Count > 1)
                {
                    (point, group) = (last, distinct.First(separator => separator.ToString() != last).ToString());
                }
                else if (separators.Length == 1 && body.Length - body.LastIndexOf(last, StringComparison.Ordinal) - 1 != 3)
                {
                    point = last;
                }
                else
                {
                    group = last;
                }
            }

            var whole = point.Length > 0 ? body[..body.LastIndexOf(point, StringComparison.Ordinal)] : body;
            var fraction = point.Length > 0 ? body[(body.LastIndexOf(point, StringComparison.Ordinal) + 1)..] : string.Empty;
            var digits = (group.Length > 0 ? whole.Replace(group, string.Empty, StringComparison.Ordinal) : whole) + (fraction.Length > 0 ? "." + fraction : string.Empty);
            return new Figure(text, match.Groups[1].Value, match.Groups[3].Value, group, point, fraction.Length, double.Parse(digits, CultureInfo.InvariantCulture));
        }

        public string Format(double value)
        {
            var fixedText = value.ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture).Split('.');
            var whole = Group.Length > 0 ? Thousands().Replace(fixedText[0], Group) : fixedText[0];
            return Prefix + whole + (Decimals > 0 ? Decimal + fixedText[1] : string.Empty) + Suffix;
        }

        [GeneratedRegex(@"^(.*?)(\d[\d.,   ]*\d|\d)(.*)$", RegexOptions.Singleline)]
        private static partial Regex Number();

        [GeneratedRegex(@"\d")]
        private static partial Regex Digits();

        [GeneratedRegex(@"\B(?=(\d{3})+(?!\d))")]
        private static partial Regex Thousands();
    }
}
