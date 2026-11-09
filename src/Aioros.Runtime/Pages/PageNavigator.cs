using Aioros.Navigation;

namespace Aioros.Runtime.Pages;

/// <summary>
/// The <see cref="INavigator"/> of one page, scoped like its models: on the web it records the navigation the page asks for
/// and the page handler answers it with a redirect; on the desktop, once the page is shown, it follows it at once.
/// </summary>
public sealed class PageNavigator : INavigator
{
    private static readonly Uri Origin = new("https://aioros.invalid/");

    private string _page = "/";
    private Func<string, ValueTask>? _follow;

    /// <summary>The last navigation asked for since the page opened, resolved to a path or to another site's URL.</summary>
    public string? Requested { get; private set; }

    /// <summary>Starts a page: relative paths resolve against it and earlier navigations are forgotten.</summary>
    public void Open(string page)
    {
        _page = page;
        _follow = null;
        Requested = null;
    }

    /// <summary>Follows every later navigation as soon as it is asked for, instead of only recording it.</summary>
    public void Follow(Func<string, ValueTask> follow) => _follow = follow;

    public ValueTask NavigateAsync(NavigationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = Resolve(_page, request.Path);
        Requested = target;
        return _follow?.Invoke(target) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resolves a path against the page as a browser resolves a link on it (RFC 3986, section 5): a path stays a path, an
    /// <c>http</c> or <c>https</c> URL of another site stays that URL, and any other scheme is refused.
    /// </summary>
    public static string Resolve(string page, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = new Uri(new Uri(Origin, page), path);
        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"Only a path or an http or https URL can be navigated to, not '{path}'.", nameof(path));
        }

        return resolved.Scheme == Origin.Scheme && resolved.Authority == Origin.Authority ? resolved.PathAndQuery + resolved.Fragment : resolved.AbsoluteUri;
    }

    /// <summary>Whether a resolved navigation leaves the app for another site.</summary>
    public static bool IsExternal(string target) => !target.StartsWith('/');
}
