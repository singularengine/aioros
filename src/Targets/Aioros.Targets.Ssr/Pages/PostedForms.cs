using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// What a browser would post from a page the framework wrote (the HTML form model: a checked box's value, a radio
/// group's checked value, a select's chosen option, a text field's text, never a disabled field or a button), and the
/// fields it holds by input kind with the bounds the elements declare, the finite list a post's verification samples.
/// </summary>
public static partial class PostedForms
{
    /// <summary>The values the page's form posts as it is written, by name.</summary>
    public static ImmutableDictionary<string, string> Of(string html) =>
        Fields(html).Where(static field => field.Posted is not null).GroupBy(static field => field.Name, StringComparer.Ordinal)
            .ToImmutableDictionary(static group => group.Key, static group => group.First(field => field.Posted is not null).Posted!, StringComparer.Ordinal);

    /// <summary>The page's fields, one per element (a radio group as one field with every radio's value), in document order.</summary>
    public static IReadOnlyList<PostedField> Fields(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        html = Enabled(html);
        var fields = new List<PostedField>();
        foreach (var element in Element().Matches(html).Select(static match => match.Groups))
        {
            var tag = element["tag"].Value;
            var attributes = element["attributes"].Value;
            if (Attribute(attributes, "name") is not { } name || Has(attributes, "disabled"))
            {
                continue;
            }

            if (tag == "textarea")
            {
                fields.Add(new PostedField(name, "text", WebUtility.HtmlDecode(element["inner"].Value)));
            }
            else if (tag == "select")
            {
                fields.Add(Select(name, element["inner"].Value));
            }
            else
            {
                Input(fields, name, attributes);
            }
        }

        fields.AddRange(Button().Matches(html).Select(static match => match.Groups["attributes"].Value)
            .Where(static attributes => Attribute(attributes, "role") == "menuitemcheckbox" && Attribute(attributes, "name") == "_btn" && !Has(attributes, "disabled"))
            .Select(static attributes => new PostedField("_btn", "press", null) { Values = [Attribute(attributes, "value") ?? string.Empty] }));
        return fields;
    }

    /// <summary>The page without what disabled fieldsets hold, whose fields the browser never posts (a closed dialog's, which its script disables).</summary>
    private static string Enabled(string html)
    {
        var kept = new System.Text.StringBuilder(html.Length);
        var depth = 0;
        var at = 0;
        foreach (Match tag in Fieldset().Matches(html))
        {
            if (depth == 0)
            {
                kept.Append(html, at, tag.Index - at);
            }

            at = tag.Index + tag.Length;
            depth = tag.Value.StartsWith("</", StringComparison.Ordinal) ? Math.Max(0, depth - (depth > 0 ? 1 : 0))
                : depth > 0 || Has(tag.Groups["attributes"].Value, "disabled") ? depth + 1
                : 0;
        }

        if (depth == 0)
        {
            kept.Append(html, at, html.Length - at);
        }

        return kept.ToString();
    }

    private static void Input(List<PostedField> fields, string name, string attributes)
    {
        var type = Attribute(attributes, "type") ?? "text";
        var value = Attribute(attributes, "value");
        var on = Has(attributes, "checked");
        switch (type)
        {
            case "submit" or "button" or "reset" or "image" or "file":
                return;
            case "checkbox":
                fields.Add(new PostedField(name, "check", on ? value ?? "on" : null) { Values = [value ?? "on"] });
                return;
            case "radio":
                var at = fields.FindIndex(field => field.Name == name && field.Kind == "radio");
                var group = at < 0 ? new PostedField(name, "radio", null) : fields[at];
                group = group with { Values = group.Values.Add(value ?? "on"), Posted = on ? value ?? "on" : group.Posted };
                if (at < 0)
                {
                    fields.Add(group);
                }
                else
                {
                    fields[at] = group;
                }

                return;
            default:
                fields.Add(new PostedField(name, type is "hidden" or "number" or "range" or "date" or "time" or "color" ? type : "text", value ?? string.Empty)
                {
                    Values = [.. new[] { Attribute(attributes, "min"), Attribute(attributes, "max") }.OfType<string>()],
                });
                return;
        }
    }

    private static PostedField Select(string name, string inner)
    {
        var options = Option().Matches(inner).Where(option => !Has(option.Groups["attributes"].Value, "disabled"))
            .Select(option => (Value: Attribute(option.Groups["attributes"].Value, "value") ?? WebUtility.HtmlDecode(option.Groups["text"].Value), Selected: Has(option.Groups["attributes"].Value, "selected"))).ToList();
        var chosen = options.Find(option => option.Selected).Value ?? (options.Count > 0 ? options[0].Value : null);
        return new PostedField(name, "select", chosen) { Values = [.. options.Select(option => option.Value)] };
    }

    private static string? Attribute(string attributes, string name) =>
        Regex.Match(attributes, "(?:^|\\s)" + name + "=\"([^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is { Success: true } found ? WebUtility.HtmlDecode(found.Groups[1].Value) : null;

    private static bool Has(string attributes, string name) =>
        Regex.IsMatch(attributes, "(?:^|\\s)" + name + "(?:[\\s=>]|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [GeneratedRegex("<(?<tag>input|select|textarea)\\b(?<attributes>[^>]*)>(?:(?<inner>.*?)</\\k<tag>>)?", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Element();

    [GeneratedRegex("</fieldset>|<fieldset\\b(?<attributes>[^>]*)>", RegexOptions.CultureInvariant)]
    private static partial Regex Fieldset();

    [GeneratedRegex("<button\\b(?<attributes>[^>]*)>", RegexOptions.CultureInvariant)]
    private static partial Regex Button();

    [GeneratedRegex("<option\\b(?<attributes>[^>]*)>(?<text>[^<]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Option();
}

/// <summary>
/// One field a page posts: its name, its input kind (<c>text</c>, <c>check</c>, <c>radio</c>, <c>select</c>, <c>number</c>,
/// <c>range</c>, <c>date</c>, <c>time</c>, <c>color</c>, <c>hidden</c>, or <c>press</c> for a check menu item, a button
/// that changes a value with no command), what it posts as written (nothing for an
/// unchecked box), and its choices or bounds: a box's value, a group's radios' values, a select's options, a number's
/// <c>min</c> and <c>max</c>.
/// </summary>
public sealed record PostedField(string Name, string Kind, string? Posted)
{
    public ImmutableArray<string> Values { get; init; } = [];

    /// <summary>The values a verification posts for this field instead of the one written, each one change of it.</summary>
    public IEnumerable<string?> Samples() => Kind switch
    {
        "check" => Posted is null ? Values.Cast<string?>() : [null],
        "radio" or "select" => Values.Where(value => value != Posted),
        "number" or "range" => Values.Length == 2 ? [Values[0], Values[1]] : ["0", "1"],
        "text" => [(Posted ?? string.Empty) + "x", string.Empty],
        "press" => Values.Cast<string?>(),
        _ => [],
    };

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Name} ({Kind})");
}
