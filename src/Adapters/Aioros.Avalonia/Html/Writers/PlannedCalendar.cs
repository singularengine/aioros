using System.Collections.Immutable;
using System.Globalization;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>A themed calendar's plan: its slot's bound paths and values, the learned look of each day key and the field of its day buttons.</summary>
internal sealed class PlannedCalendar(string paths, ImmutableArray<string> values, Dictionary<string, (string Open, string Middle)> looks, SlotSpec? field)
{
    public SlotSpec Spec(string part) => new("calendar", paths.Length == 0 ? null : paths, part) { Values = values };

    public static SlotSpec Part(string part) => new("calendar", null, part);

    public ValueNode Value(string part)
    {
        var spec = Spec(part);
        return new ValueNode(Slots.Getter(spec)) { Source = spec };
    }

    public static ValueNode ItemValue(string part)
    {
        var spec = Part(part);
        return new ValueNode(Slots.Getter(spec)) { Source = spec };
    }

    /// <summary>A day: the look learned for its key, its button numbered with the buttons and its text.</summary>
    public LearnedNode Days()
    {
        var spec = Part("key");
        var keys = looks.Keys.Order(StringComparer.Ordinal).ToImmutableArray();
        var id = new IdNode { Field = field, Button = true };
        var bodies = keys.Select(key => ImmutableArray.Create<IPlanNode>(new LiteralNode(looks[key].Open), id, new LiteralNode(looks[key].Middle), ItemValue("text"), new LiteralNode(PlannedCalendars.Close)));
        return new LearnedNode(Slots.Getter(spec), [.. keys.Select(LearnedNode.KeyOf)], [.. bodies]) { Source = spec };
    }

    /// <summary>The weeks: a shown one in its row with its number where the library writes it, a hidden one with bare buttons that keep their numbers.</summary>
    public RepeatNode Weeks(ImmutableArray<IPlanNode> before, ImmutableArray<IPlanNode> after)
    {
        var id = new IdNode { Field = field, Button = true };
        var each = Part("days");
        var days = new RepeatNode(Slots.Getter(each), [Days()]) { Source = each };
        var bare = new RepeatNode(Slots.Getter(each), [new LiteralNode("<td role=\"gridcell\"><button type=\"submit\" name=\"_btn\" value=\""), id, new LiteralNode("|\" tabindex=\"-1\"></button></td>")]) { Source = each };
        ImmutableArray<IPlanNode> shown = [new LiteralNode("<tr>"), .. before, days, .. after, new LiteralNode("</tr>")];
        ImmutableArray<IPlanNode> hidden = [new LiteralNode("<tr hidden>"), bare, new LiteralNode("</tr>")];
        var state = Part("shown");
        var spec = Spec("weeks");
        return new RepeatNode(Slots.Getter(spec), [new LearnedNode(Slots.Getter(state), [LearnedNode.KeyOf("shown"), LearnedNode.KeyOf("hidden")], [shown, hidden]) { Source = state }]) { Source = spec };
    }

    /// <summary>
    /// The nodes of the calendar's template with its caption computed for each render: the select offering the month names
    /// choosing the shown month, the one offering the learned years offering the years computed for the render and choosing
    /// the shown one, and the one element whose text is the caption; none when the plan learned selects it does not find or
    /// the caption is written elsewhere too (in an attribute, twice).
    /// </summary>
    public ImmutableArray<IPlanNode>? Captioned(ImmutableArray<IPlanNode> nodes, string caption)
    {
        var (months, years) = (0, 0);
        IPlanNode Select(IPlanNode node) => node switch
        {
            SelectNode { Items: null } select when Names() is { } names && select.Options.SequenceEqual(names) && ++months > 0
                => select with { Get = Slots.Getter(Spec("month")), Source = Spec("month") },
            SelectNode { Items: null } select when values.Any(static value => value.StartsWith("before=", StringComparison.Ordinal))
                && select.Options.Length > 1 && select.Options.All(static option => int.TryParse(option, NumberStyles.None, CultureInfo.CurrentCulture, out _)) && ++years > 0
                => select with { Get = Slots.Getter(Spec("year")), Source = Spec("year"), Items = Slots.Getter(Spec("years")), ItemsSource = Spec("years"), Options = [], Labels = [], Kinds = [] },
            ConditionNode condition => condition with { Body = [.. condition.Body.Select(Select)] },
            _ => node,
        };

        var selected = nodes.Select(Select).ToImmutableArray();
        if (months != (Names() is null ? 0 : 1) || years != (values.Any(static value => value.StartsWith("before=", StringComparison.Ordinal)) ? 1 : 0))
        {
            return null;
        }

        var encoded = CssValues.Encode(caption);
        var written = selected.OfType<LiteralNode>().Sum(literal => Count(literal.Text, encoded));
        var at = selected.ToList().FindIndex(node => node is LiteralNode literal && literal.Text.Contains(">" + encoded + "<", StringComparison.Ordinal));
        if (written == 0)
        {
            return selected;
        }

        if (written > 1 || at < 0)
        {
            return null;
        }

        var text = ((LiteralNode)selected[at]).Text;
        var cut = text.IndexOf(">" + encoded + "<", StringComparison.Ordinal) + 1;
        return selected.RemoveAt(at).InsertRange(at, ImmutableArray.Create<IPlanNode>(new LiteralNode(text[..cut]), Value("caption"), new LiteralNode(text[(cut + encoded.Length)..])));
    }

    private string[]? Names() =>
        values.Contains("names=abbr") ? [.. CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames.Take(12)]
        : values.Contains("names=full") ? [.. CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12)]
        : null;

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
