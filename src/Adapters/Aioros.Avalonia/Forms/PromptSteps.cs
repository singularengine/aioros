using System.Collections.Immutable;
using System.Text;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// The steps an open prompt carries in its <c>_prompt</c> field, one per line, each a URL-encoded form: the button that
/// opened it, then each step that left it open, with its button (none for a changed selection) and the prompt's fields.
/// A browser posts its line breaks as CR LF, as it posts every line break of a form's values.
/// </summary>
internal static class PromptSteps
{
    /// <summary>
    /// The steps a post names: none without the field, or nothing when one is malformed, the first names no button or
    /// there are more than <see cref="MaxSteps"/>; a later step without a button changed a field of the prompt.
    /// </summary>
    /// <summary>The most steps a prompt replays: each is a render and a press on the engine's one thread (the security audit's F2).</summary>
    public const int MaxSteps = 64;

    public static ImmutableArray<ImmutableDictionary<string, string>>? Parse(ImmutableDictionary<string, string> form)
    {
        if (!form.TryGetValue(PlanForms.PromptField, out var field))
        {
            return [];
        }

        var lines = field.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length > MaxSteps)
        {
            return null;
        }

        var steps = ImmutableArray.CreateBuilder<ImmutableDictionary<string, string>>();
        foreach (var line in lines)
        {
            var step = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (var pair in line.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    return null;
                }

                step[Uri.UnescapeDataString(pair[..separator])] = Uri.UnescapeDataString(pair[(separator + 1)..]);
            }

            steps.Add(step.ToImmutable());
        }

        return steps[0].TryGetValue(PlanForms.ButtonField, out var button) && button.Length > 0 ? steps.ToImmutable() : null;
    }

    public static string Encode(IEnumerable<ImmutableDictionary<string, string>> steps) =>
        string.Join('\n', steps.Select(static step =>
        {
            var line = new StringBuilder();
            foreach (var (key, value) in step.OrderBy(static pair => pair.Key == PlanForms.ButtonField ? 0 : 1).ThenBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                line.Append(line.Length == 0 ? string.Empty : "&").Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            }

            return line.ToString();
        }));
}
