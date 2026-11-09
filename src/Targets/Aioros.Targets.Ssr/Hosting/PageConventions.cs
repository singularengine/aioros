using Aioros.Pages;

namespace Aioros.Targets.Ssr.Hosting;

/// <summary>
/// Who may open pages that have no loader to carry <c>[Authorize]</c>, set where the app is composed, as Razor Pages'
/// <c>AuthorizeFolder</c>, <c>AuthorizePage</c>, <c>AllowAnonymousToFolder</c> and <c>AllowAnonymousToPage</c>. A
/// folder or a page is named by its folder under <c>Pages</c> (<c>/account</c>, <c>/(app)/reports</c>,
/// <c>/issues/[id]</c>), matched without case; a folder covers every page below it. A convention's rules come before
/// the page's own, and allowing anonymous visitors, by a convention or by <c>[AllowAnonymous]</c> on the page, lifts
/// every rule.
/// </summary>
public sealed class PageConventions
{
    private readonly List<Convention> _conventions = [];

    /// <summary>Every page in the folder and below it requires a signed-in user, and the policy when one is given.</summary>
    public PageConventions AuthorizeFolder(string folder, string? policy = null) => Add(folder, folder: true, new PageAccess(policy, null));

    /// <summary>The page requires a signed-in user, and the policy when one is given.</summary>
    public PageConventions AuthorizePage(string page, string? policy = null) => Add(page, folder: false, new PageAccess(policy, null));

    /// <summary>Every page in the folder and below it is open to anonymous visitors, whatever else protects it.</summary>
    public PageConventions AllowAnonymousToFolder(string folder) => Add(folder, folder: true, access: null);

    /// <summary>The page is open to anonymous visitors, whatever else protects it.</summary>
    public PageConventions AllowAnonymousToPage(string page) => Add(page, folder: false, access: null);

    /// <summary>The page with the conventions that match it applied to its access rules.</summary>
    public PageDefinition Apply(PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var path = FolderOf(page.Source);
        var matching = _conventions.Where(convention => convention.Matches(path)).ToList();
        if (matching.Count == 0)
        {
            return page;
        }

        return page.AllowAnonymous || matching.Any(convention => convention.Access is null)
            ? page with { Access = [], AllowAnonymous = true }
            : page with { Access = [.. matching.Select(convention => convention.Access!), .. page.Access] };
    }

    private PageConventions Add(string path, bool folder, PageAccess? access)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _conventions.Add(new Convention(Normalize(path), folder, access));
        return this;
    }

    private static string FolderOf(string source)
    {
        var folder = source.Replace('\\', '/');
        var end = folder.LastIndexOf('/');
        folder = end < 0 ? string.Empty : folder[..end];
        return Normalize(folder.StartsWith("Pages", StringComparison.Ordinal) ? folder["Pages".Length..] : folder);
    }

    private static string Normalize(string path) => "/" + path.Trim().Replace('\\', '/').Trim('/');

    private sealed record Convention(string Path, bool Folder, PageAccess? Access)
    {
        public bool Matches(string page) =>
            string.Equals(page, Path, StringComparison.OrdinalIgnoreCase)
            || (Folder && (Path == "/" || page.StartsWith(Path + "/", StringComparison.OrdinalIgnoreCase)));
    }
}
