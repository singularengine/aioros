using System.Text;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// Joins two stylesheets rule by rule: the first whole, then each top-level rule of the second the first does not hold, in
/// its order, so an engine's answer carries its plan's CSS and its own without writing a shared rule twice. A rule is a
/// top-level block (a style rule, or an at-rule with its nested rules, however many lines it spans) or a statement ending
/// in <c>;</c>, its braces counted outside strings.
/// </summary>
internal static class StyleRules
{
    public static string Join(string first, string then)
    {
        var known = new HashSet<string>(Rules(first), StringComparer.Ordinal);
        var joined = new StringBuilder(first, first.Length + then.Length);
        foreach (var rule in Rules(then).Where(known.Add))
        {
            joined.Append(rule);
        }

        return joined.ToString();
    }

    private static IEnumerable<string> Rules(string css)
    {
        var start = 0;
        while (start < css.Length)
        {
            if (char.IsWhiteSpace(css[start]))
            {
                start++;
                continue;
            }

            var end = End(css, start);
            yield return css[start..end];
            start = end;
        }
    }

    /// <summary>Where the rule starting at <paramref name="start"/> ends, past the line break after it.</summary>
    private static int End(string css, int start)
    {
        var (depth, index) = (0, start);
        while (index < css.Length)
        {
            var character = css[index++];
            switch (character)
            {
                case '"' or '\'':
                    index = AfterString(css, index, character);
                    break;
                case '{':
                    depth++;
                    break;
                case '}' when --depth == 0:
                case ';' when depth == 0:
                    return index < css.Length && css[index] == '\n' ? index + 1 : index;
            }
        }

        return css.Length;
    }

    /// <summary>Where a string opened by <paramref name="quote"/> ends, its escaped characters skipped.</summary>
    private static int AfterString(string css, int index, char quote)
    {
        while (index < css.Length && css[index] != quote)
        {
            index += css[index] == '\\' ? 2 : 1;
        }

        return Math.Min(index + 1, css.Length);
    }
}
