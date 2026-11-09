using System.Collections.Immutable;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>The prompts a post left open in one host panel of the page.</summary>
internal sealed record PromptLayer(Panel Host, ImmutableArray<Control> Controls);

/// <summary>The notices a post showed in one panel outside the page, as a notification manager's card stack.</summary>
internal sealed record NoticeLayer(Panel Panel, ImmutableArray<Control> Controls);

/// <summary>
/// What a post left for the user that the answer writes after the page: prompts (with the steps that opened them) and
/// notices. The page's own render leaves them out where they sit, so its fields keep their names.
/// </summary>
internal sealed record Overlays(ImmutableArray<PromptLayer> Prompts, string? Replay, ImmutableArray<NoticeLayer> Notices)
{
    public static Overlays None { get; } = new([], null, []);

    /// <summary>The step that showed the notices, which a notice's button posts in <c>_prompt</c> to be replayed before its own step.</summary>
    public string? NoticeReplay { get; init; }

    public bool IsEmpty => Prompts.IsEmpty && Notices.IsEmpty;

    /// <summary>
    /// The prompts without the overlays a library marked for the browser (<c>a:Web.ModalOf</c>), which their owner's dialog
    /// writes; the steps that opened them stay, so a command waiting on one resumes when a post replays them.
    /// </summary>
    public Overlays WithoutModals() =>
        this with { Prompts = [.. Prompts.Select(layer => layer with { Controls = [.. layer.Controls.Where(control => !Variants.BrowserModals.Owned(control))] }).Where(layer => !layer.Controls.IsEmpty)] };

    public bool Hides(Control control) =>
        Prompts.Any(layer => layer.Controls.Contains(control)) || Notices.Any(layer => layer.Controls.Contains(control));

    /// <summary>The anchor name of a host panel that holds prompts, for the dialog placed on its box.</summary>
    public string? Anchor(Control control)
    {
        for (var index = 0; index < Prompts.Length; index++)
        {
            if (ReferenceEquals(Prompts[index].Host, control))
            {
                return "prompt" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }
}
