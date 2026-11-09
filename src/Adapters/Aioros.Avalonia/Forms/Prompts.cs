using System.Collections.Immutable;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Writers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// What a window showed before a post's commands ran, to tell what they left for the user: a prompt over the page (a
/// control added to a panel of the page that is not an items panel, as an overlay dialog host adds its dialog and its
/// mask), which a command still pending waits on, and the notices of the post's last step (a content control added to a
/// panel outside the page, as a window notification manager adds its cards to the adorner layer).
/// </summary>
internal sealed class Prompts
{
    private readonly Window _window;
    private readonly HashSet<Visual> _before;
    private HashSet<Visual> _step;
    private HashSet<Visual>? _noticesSince;

    private Prompts(Window window)
    {
        _window = window;
        _before = [.. window.GetVisualDescendants()];
        _step = _before;
    }

    /// <summary>The steps that opened the prompts shown, for their <c>_prompt</c> field.</summary>
    public string? Replay { get; set; }

    /// <summary>The step that showed this post's notices, which a button of a notice posts to be replayed before its own.</summary>
    public string? NoticeReplay { get; set; }

    public static Prompts Watch(Window window) => new(window);

    /// <summary>Starts a step: the notices shown from now on are this step's.</summary>
    public void Step() => _step = [.. _window.GetVisualDescendants()];

    /// <summary>Whether a prompt is open over the page.</summary>
    public bool Opened() => Opened(_before);

    /// <summary>Whether a prompt opened in this step.</summary>
    public bool OpenedInStep() => Opened(_step);

    /// <summary>Whether the post showed notices so far.</summary>
    public bool Noticed() => Notices(_before).Any();

    /// <summary>Runs a step that presses a button of a notice an earlier step showed: the notices written meanwhile are all of this post's, so its button is found.</summary>
    public async ValueTask<T> WithNoticesAsync<T>(Func<ValueTask<T>> step)
    {
        _noticesSince = _before;
        try
        {
            return await step();
        }
        finally
        {
            _noticesSince = null;
        }
    }

    /// <summary>The open prompts' controls, in the order they were added.</summary>
    public IEnumerable<Control> Open() => OpenPrompts(_before);

    /// <summary>Whether a control belongs to a prompt open now.</summary>
    public Func<Control, bool> Held()
    {
        var open = OpenPrompts(_before).ToHashSet<Visual>();
        return control => control.GetSelfAndVisualAncestors().Any(open.Contains);
    }

    /// <summary>What the answer writes after the page: the open prompts by host, and this step's notices by the panel that shows them.</summary>
    public Overlays Shown()
    {
        var prompts = OpenPrompts(_before).GroupBy(control => (Panel)control.GetVisualParent()!)
            .Select(group => new PromptLayer(group.Key, [.. group]))
            .ToImmutableArray();
        var notices = Notices(_noticesSince ?? _step).GroupBy(control => (Panel)control.GetVisualParent()!)
            .Select(group => new NoticeLayer(group.Key, [.. group]))
            .ToImmutableArray();
        return new Overlays(prompts, prompts.IsEmpty ? null : Replay, notices) { NoticeReplay = notices.IsEmpty ? null : NoticeReplay };
    }

    private bool Opened(HashSet<Visual> before) => OpenPrompts(before).Any();

    private IEnumerable<Control> OpenPrompts(HashSet<Visual> before) =>
        _window.Content is Visual page
            ? _window.GetVisualDescendants().OfType<Control>().Where(control =>
                !before.Contains(control) && control.GetVisualParent() is Panel { IsItemsHost: false } panel && _before.Contains(panel)
                && (ReferenceEquals(panel, page) || page.IsVisualAncestorOf(panel)))
            : [];

    private IEnumerable<Control> Notices(HashSet<Visual> since)
    {
        if (_window.Content is not Visual page)
        {
            return [];
        }

        var shown = _window.GetVisualDescendants().OfType<ContentControl>()
            .Where(control => !since.Contains(control) && control.GetVisualParent() is Panel { IsItemsHost: false } && !ReferenceEquals(control, page) && !page.IsVisualAncestorOf(control))
            .ToList();
        var set = shown.ToHashSet();
        return shown.Where(control => !control.GetVisualAncestors().Any(ancestor => ancestor is ContentControl outer && set.Contains(outer)));
    }
}
