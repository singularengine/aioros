using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Looks;

/// <summary>
/// Whether a style's selector, as Avalonia writes it, applies to a control at rest: a type (exact, or <c>:is(Type)</c> for it
/// and the types derived from it), classes, a name and positions (<c>:nth-child</c>, <c>:nth-last-child</c>), joined by
/// descendant (a space) and child (<c>&gt;</c>) steps, with
/// <c>,</c> between alternatives. A state (<c>:pointerover</c>), a template step or a nesting selector applies to no control
/// at rest, as Avalonia matches them only while that state or that template holds.
/// </summary>
internal static partial class SelectorText
{
    public static bool Matches(string selector, Control control) =>
        selector.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(alternative => MatchesSteps(Steps(alternative), control));

    private static List<(string Simple, bool Child)> Steps(string selector)
    {
        var steps = new List<(string Simple, bool Child)>();
        var child = false;
        foreach (var token in Tokens().Matches(selector).Select(static match => match.Value))
        {
            if (token == ">")
            {
                child = true;
                continue;
            }

            steps.Add((token, child));
            child = false;
        }

        return steps;
    }

    private static bool MatchesSteps(List<(string Simple, bool Child)> steps, Control control)
    {
        if (steps.Count == 0 || !MatchesSimple(steps[^1].Simple, control))
        {
            return false;
        }

        StyledElement? current = control;
        for (var index = steps.Count - 2; index >= 0; index--)
        {
            var direct = steps[index + 1].Child;
            current = current?.Parent;
            while (current is Control ancestor && !MatchesSimple(steps[index].Simple, ancestor))
            {
                if (direct)
                {
                    return false;
                }

                current = current.Parent;
            }

            if (current is null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSimple(string simple, Control control)
    {
        var match = Simple().Match(simple);
        if (!match.Success)
        {
            return false;
        }

        var type = match.Groups["type"].Value;
        var derived = match.Groups["is"].Value;
        return (type.Length == 0 || control.GetType().Name == type)
            && (derived.Length == 0 || Lineage(control.GetType()).Contains(derived, StringComparer.Ordinal))
            && (match.Groups["name"].Value is not { Length: > 0 } name || control.Name == name)
            && match.Groups["class"].Captures.All(capture => control.Classes.Contains(capture.Value))
            && match.Groups["position"].Captures.Select((capture, index) => (capture.Value, Argument: match.Groups["argument"].Captures[index].Value))
                .All(position => Position(control, position.Value == "nth-last-child", position.Argument));
    }

    /// <summary>Whether the control's place among its panel's children is one <c>:nth-child(an+b)</c> or <c>:nth-last-child(an+b)</c> names (MOTION-29).</summary>
    private static bool Position(Control control, bool fromEnd, string argument)
    {
        if (control.GetVisualParent() is not Panel panel)
        {
            return false;
        }

        var index = panel.Children.IndexOf(control);
        if (index < 0)
        {
            return false;
        }

        var place = fromEnd ? panel.Children.Count - index : index + 1;
        var (step, offset) = argument.Replace(" ", string.Empty, StringComparison.Ordinal) switch
        {
            "odd" => (2, 1),
            "even" => (2, 0),
            var text when text.Contains('n', StringComparison.Ordinal) => Linear(text),
            var text => (0, int.Parse(text, System.Globalization.CultureInfo.InvariantCulture)),
        };
        return step == 0 ? place == offset : (place - offset) % step == 0 && (place - offset) / step >= 0;
    }

    private static (int Step, int Offset) Linear(string text)
    {
        var parts = text.Split('n');
        var step = parts[0] switch { "" or "+" => 1, "-" => -1, var number => int.Parse(number, System.Globalization.CultureInfo.InvariantCulture) };
        return (step, parts[1].Length == 0 ? 0 : int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static IEnumerable<string> Lineage(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            yield return type.Name;
        }
    }

    [GeneratedRegex(@">|[^\s>]+")]
    private static partial Regex Tokens();

    [GeneratedRegex(@"^(?::is\((?:\w+\|)?(?<is>\w+)\))?(?:(?:\w+\|)?(?<type>\w+))?(?:\.(?<class>[\w-]+)|#(?<name>[\w-]+))*(?::(?<position>nth-child|nth-last-child)\((?<argument>[^)]+)\))*$")]
    private static partial Regex Simple();
}
