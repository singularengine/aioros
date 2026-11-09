namespace Aioros.Pages;

/// <summary>
/// The static title and metadata a page or layout declares on its XAML root (<c>a:Page.Title</c>,
/// <c>a:Page.TitleTemplate</c>, <c>a:Page.Description</c>…), read by the generator. A loader of the same segment wins over
/// them.
/// </summary>
/// <param name="Title">The segment's title.</param>
/// <param name="TitleTemplate">
/// The template for the titles of the segments below, with <c>%s</c> where their title goes (Next.js <c>title.template</c>).
/// </param>
/// <param name="AbsoluteTitle">Whether <paramref name="Title"/> is used as written, ignoring the templates above (Next.js <c>title.absolute</c>).</param>
/// <param name="Metadata">The segment's search metadata.</param>
public sealed record PageHead(string? Title = null, string? TitleTemplate = null, bool AbsoluteTitle = false, PageMetadata? Metadata = null);
