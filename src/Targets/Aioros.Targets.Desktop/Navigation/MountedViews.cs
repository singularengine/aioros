using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Hosting;
using Aioros.Pages;
using Avalonia.Controls;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>The layouts and the page the window shows, outermost first, each with the source it was loaded from.</summary>
internal sealed class MountedViews(Window window, XamlSources sources)
{
    private ImmutableArray<(string Source, Control Control)> _mounted = [];

    public int Count => _mounted.Length;

    /// <summary>How many of the mounted layouts the next page shares (<see cref="PageMounts.Shared"/>).</summary>
    public int Shared(ImmutableArray<string> next) => PageMounts.Shared([.. _mounted.Select(static mount => mount.Source)], next);

    /// <summary>Shows the page: the layouts it shares stay, with their models, and the rest is loaded below them.</summary>
    public void Show(PageDefinition? page, ImmutableArray<object?> models)
    {
        var kept = Shared(page?.Sources ?? []);
        for (var index = 0; index < kept; index++)
        {
            _mounted[index].Control.DataContext = models[index];
        }

        Mount(page, models, kept);
    }

    /// <summary>
    /// Loads again, with the same models, the views from the first that shows <paramref name="source"/> (every view when
    /// null), as hot reload does after an edit, putting back the scroll, the focus and the text typed in unbound named
    /// text boxes; false when the window does not show that source.
    /// </summary>
    public bool Reload(PageDefinition? page, ImmutableArray<object?> models, string? source)
    {
        var from = source is null ? 0 : _mounted.IndexOf(_mounted.FirstOrDefault(mount => mount.Source == source));
        if (_mounted.IsEmpty || from < 0 || (source is not null && _mounted[from].Source != source))
        {
            return false;
        }

        var state = ViewState.Of(window);
        Mount(page, models, from);
        state.Restore(window);
        return true;
    }

    /// <summary>Gives the mounted views the models a refresh loaded, where they changed.</summary>
    public void Rebind(ImmutableArray<object?> before, ImmutableArray<object?> fresh)
    {
        for (var index = 0; index < fresh.Length && index < _mounted.Length; index++)
        {
            if (!ReferenceEquals(before[index], fresh[index]))
            {
                _mounted[index].Control.DataContext = fresh[index];
            }
        }
    }

    public void BindViews(PageDefinition? page, ImmutableArray<object?> models)
    {
        for (var index = 0; page is not null && index < _mounted.Length; index++)
        {
            PageComposer.BindViews(sources, page, _mounted[index].Control, index, models);
        }
    }

    public void Clear() => _mounted = [];

    private void Mount(PageDefinition? page, ImmutableArray<object?> models, int from)
    {
        var shown = page?.Sources ?? [];
        var outlet = from > 0 ? PageComposer.OutletOf(_mounted[from - 1].Control) : null;
        PageMounts.Unmount(window, outlet);
        var chain = PageComposer.Chain([.. shown.Skip(from).Select(source => (Func<Control>)(() => sources.Load(source)))], models.IsDefaultOrEmpty ? [] : models[from..]);
        if (chain is [var top, ..])
        {
            DesktopRequests.KeepLinksInside(top);
        }

        if (outlet is null)
        {
            window.Content = chain.FirstOrDefault();
        }
        else
        {
            outlet.Content = chain[0];
        }

        _mounted = [.. _mounted.Take(from), .. shown.Skip(from).Zip(chain)];
        BindViews(page, models);
    }
}
