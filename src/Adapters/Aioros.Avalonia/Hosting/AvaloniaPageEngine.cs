using System.Buffers;
using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

public sealed class AvaloniaPageEngine : IPageEngine, IPageCapture, IUiThread, IModelThread
{
    private readonly AvaloniaHost _host;
    private readonly XamlSources _sources;
    private readonly HeldChanges _held = new();

    public AvaloniaPageEngine(AvaloniaHost host, XamlSources sources)
    {
        _host = host;
        _sources = sources;
    }

    public async ValueTask<CompiledPage> RenderAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(page.Route, () => _held.While(() => WithPage(page, models, theme, (window, root) => WithViewError(page, theme, error => VerifiedPlans.Compile(window, root, models, error)))), cancellationToken);

    public async ValueTask<CompiledPage> AnswerAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(page.Route, () => _held.While(() => WithPage(page, models, theme, (window, root) => HtmlCompiler.Compile(window, root, models, planning: false))), cancellationToken);

    public async ValueTask<PostOutcome> PostAsync(
        PageDefinition page,
        ImmutableArray<object?> models,
        ImmutableDictionary<string, string> form,
        ImmutableArray<PostedFile> files,
        string theme,
        CancellationToken cancellationToken) =>
        await PostAsync(page, models, form, files, theme, planAnswers: false, cancellationToken);

    public async ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, bool planAnswers, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(page.Route, () => EnginePosts.PostAsync(() => PageComposer.Compose(_sources, page, models), models, new PostedForm(form, files), theme, planAnswers), cancellationToken);

    /// <summary>Avalonia runs on the engine's thread once the engine exists.</summary>
    public bool OwnsCommands => true;

    public async ValueTask<CompiledPage> RenderXamlAsync(string xaml, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(() => _held.While(() => PageWindows.With(() => PageComposer.Compose([() => _sources.Parse(xaml)], models), theme, (window, root) => VerifiedPlans.Compile(window, root, models, null))), cancellationToken);

    public async ValueTask<PostOutcome> PostXamlAsync(string xaml, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, CancellationToken cancellationToken, bool planAnswers = false) =>
        await _host.InvokeAsync(() => EnginePosts.PostAsync(() => PageComposer.Compose([() => _sources.Parse(xaml)], models), models, new PostedForm(form, files), theme, planAnswers), cancellationToken);

    public async ValueTask<PageCapture> CaptureAsync(PageDefinition page, ImmutableArray<object?> models, CaptureOptions options, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(page.Route, () => PageCaptures.With(clock => PageWindows.With(() => PageComposer.Compose(_sources, page, models), options.Theme, (window, _) => PageCaptures.Capture(window, options, clock), clock.Attach)), cancellationToken);

    public async ValueTask<PageCapture> CaptureXamlAsync(string xaml, ImmutableArray<object?> models, CaptureOptions options, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(() => PageCaptures.With(clock => PageWindows.With(() => PageComposer.Compose([() => _sources.Parse(xaml)], models), options.Theme, (window, _) => PageCaptures.Capture(window, options, clock), clock.Attach)), cancellationToken);

    public async ValueTask<string?> ReloadAsync(string source, string xaml, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(() => Reload(source, xaml), cancellationToken);

    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken) =>
        await await _host.InvokeAsync(() => _held.ApplyAsync(action), cancellationToken);

    public async ValueTask<T> LoadAsync<T>(PageDefinition page, Func<ValueTask<T>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(load);
        return await _host.InvokeAsync(page.Route, () => load().AsTask(), cancellationToken);
    }

    public async ValueTask<T> DispatchAsync<T>(Func<T> action, CancellationToken cancellationToken) =>
        await _host.InvokeAsync(action, cancellationToken);

    private string? Reload(string source, string xaml)
    {
        if (_sources.IsCurrent(source, xaml))
        {
            return null;
        }

        if (_sources.Reload(source, xaml) is { } failure)
        {
            return failure;
        }

        if (XamlSources.Restyles(xaml))
        {
            ThemeLooks.Clear();
            MarkLooks.Clear();
            Html.Writers.TipWriters.Clear();
            StyleContext.Clear();
        }

        return null;
    }

    private T WithPage<T>(PageDefinition page, ImmutableArray<object?> models, string theme, Func<Window, Control, T> body) =>
        PageWindows.With(() => PageComposer.Compose(_sources, page, models), theme, body);

    /// <summary>Shows the view error template in a window of its own while a page with views is compiled, for its plan's error bodies.</summary>
    private T WithViewError<T>(PageDefinition page, string theme, Func<Control?, T> body)
    {
        if (page.Views.IsEmpty)
        {
            return body(null);
        }

        var error = PageComposer.ViewError(_sources, page);
        var (window, root) = PageWindows.Mount(() => error, theme);
        try
        {
            PageWindows.Settle(window);
            return body(root);
        }
        finally
        {
            PageWindows.Unmount(window, root);
        }
    }
}
