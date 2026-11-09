namespace Aioros.Runtime.Rendering;

/// <summary>
/// The CSS rule sets a render's slot renderers contribute to the whole page, each under a key and written once however
/// many renderers contribute it (styled-components' and Emotion's style registries): the first contribution of a key in
/// document order is kept, and the keys keep the order they were first contributed in.
/// </summary>
public sealed class PageStyles
{
    private readonly List<KeyValuePair<string, string>> _styles = [];

    /// <summary>The rule sets contributed, by key, in the order their keys were first contributed.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> All => _styles;

    /// <summary>Whether a rule set was contributed under this key.</summary>
    public bool Has(string key)
    {
        Check(key);
        return _styles.Exists(style => style.Key == key);
    }

    /// <summary>Contributes a rule set under a key, unless one was contributed under it already.</summary>
    public void Add(string key, string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        if (!Has(key))
        {
            _styles.Add(new(key, css));
        }
    }

    /// <summary>Refuses a key that is empty or holds anything other than letters, digits, <c>-</c>, <c>_</c> and <c>.</c>.</summary>
    public static void Check(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length == 0 || !key.All(static letter => char.IsAsciiLetterOrDigit(letter) || letter is '-' or '_' or '.'))
        {
            throw new ArgumentException($"The style key '{key}' must be letters, digits, '-', '_' or '.'.", nameof(key));
        }
    }
}

/// <summary>A rule set a slot renderer contributed to the page while the engine recorded its markup, contributed again on each render.</summary>
public sealed record PageStyleNode(string Key, string Css) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope) => output.AddStyle(Key, Css);
}
