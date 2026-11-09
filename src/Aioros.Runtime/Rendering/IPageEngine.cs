using System.Collections.Immutable;
using Aioros.Pages;

namespace Aioros.Runtime.Rendering;

public interface IPageEngine
{
    ValueTask<CompiledPage> RenderAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken);

    /// <summary>
    /// Renders the page to answer one request and nothing more: it does not try to compile a plan, so the page it returns
    /// is never reusable. By default, <see cref="RenderAsync"/>.
    /// </summary>
    ValueTask<CompiledPage> AnswerAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
        RenderAsync(page, models, theme, cancellationToken);

    ValueTask<PostOutcome> PostAsync(
        PageDefinition page,
        ImmutableArray<object?> models,
        ImmutableDictionary<string, string> form,
        ImmutableArray<PostedFile> files,
        string theme,
        CancellationToken cancellationToken);

    /// <summary>
    /// Binds and runs a post as <see cref="PostAsync(PageDefinition, ImmutableArray{object?}, ImmutableDictionary{string, string}, ImmutableArray{PostedFile}, string, CancellationToken)"/>
    /// does, leaving its answer to the page's plan when <paramref name="planAnswers"/> holds: the outcome then carries no
    /// page, unless the post left a prompt only the engine's render shows. By default the engine writes every answer.
    /// </summary>
    ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, bool planAnswers, CancellationToken cancellationToken) =>
        PostAsync(page, models, form, files, theme, cancellationToken);

    /// <summary>
    /// Whether the engine runs Avalonia in this process now, so a post's command runs through the engine, on Avalonia's
    /// thread: what a command creates or touches there (an overlay dialog host, a window, a notification manager) belongs
    /// to that thread, and only the engine shows the prompt a command opens. By default, never: the plan runs commands.
    /// </summary>
    bool OwnsCommands => false;

    /// <summary>
    /// Whether a post's command can run through the engine, started for it when it does not run yet: an engine that can
    /// run Avalonia in this process (one started on demand in production) can, so a command the generator did not find
    /// runs on any thread (<c>AnywhereCommands</c>) is run there; one that cannot (no Avalonia, a Native AOT build) leaves
    /// every command to the plan. By default, whenever the engine owns commands.
    /// </summary>
    bool RunsCommands => OwnsCommands;
}

public sealed record CompiledPage(RenderPlan Plan, string Css, string Background, string Foreground, bool Reusable, string? Failure = null)
{
    /// <summary>Where in the XAML the element that made the plan fail is, when its position is known.</summary>
    public SourcePosition? FailureAt { get; init; }

    /// <summary>Whether the page holds a file input, so its form posts as <c>multipart/form-data</c>.</summary>
    public bool Uploads { get; init; }

    /// <summary>What the page's author should know about its plan, though it works (<c>AIOROS033</c>).</summary>
    public ImmutableArray<string> Warnings { get; init; } = [];
}

/// <summary>
/// A file a post carries for a field: the field's name in the form, the file's name and content type as the browser
/// sent them, its length, and the temporary file on disk that holds its content until the post is answered.
/// </summary>
public sealed record PostedFile(string Field, string Name, string ContentType, long Length, string Path);

public enum PostStatus
{
    Rendered,
    BadRequest,
    Conflict,

    /// <summary>The post names a list item that is no longer where the page wrote it: nothing was bound and the page is sent anew.</summary>
    Stale,
}

public sealed record PostOutcome(PostStatus Status, CompiledPage? Page)
{
    /// <summary>
    /// Whether the answer shows what the post left for the user and only its own render holds: a prompt its command waits
    /// on, or a notice it showed. Such an answer is written as rendered, never revalidated.
    /// </summary>
    public bool Shows { get; init; }
}

/// <summary>A position in an app's XAML: the file relative to the project (<c>Pages/inbox/Page.axaml</c>), the line and the column, from 1.</summary>
public sealed record SourcePosition(string Source, int Line, int Column)
{
    /// <summary>The position as MSBuild and the IDEs read it in a diagnostic: <c>Pages/inbox/Page.axaml(15,6)</c>.</summary>
    public override string ToString() => $"{Source}({Line},{Column})";
}
