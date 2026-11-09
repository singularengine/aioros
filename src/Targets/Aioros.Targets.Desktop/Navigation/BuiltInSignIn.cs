using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Desktop.Handoff;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>
/// The installed app's sign-in and sign-out routes. On the sign-in route the window says to continue in the system browser
/// (RFC 8252, as VS Code does) while the sign-in client signs the user in there, and a <c>401</c> that remains after a
/// refresh is sent here; on the sign-out route the refresh token is revoked, the stored tokens are dropped and the window
/// offers to sign in again.
/// </summary>
internal sealed class BuiltInSignIn(SignInClient client, Func<string, bool> isPage)
{
    private string Path => client.Options.SignInPath;

    public bool Handles(string route) => Is(route, Path) || Is(route, client.Options.SignOutPath);

    /// <summary>Where a failure sends the user: the sign-in route with the page as <c>ReturnUrl</c> when it asks for a sign-in, unless it came from there.</summary>
    public string? Redirect(Exception exception, string? current, string path) =>
        exception is SignInRequiredException && !From(current) && !From(path) ? Path + "?ReturnUrl=" + Uri.EscapeDataString(path) : null;

    /// <summary>The page to show once signed in: the local <c>ReturnUrl</c>, or the home page.</summary>
    public static string ReturnUrl(ImmutableDictionary<string, string> query) =>
        query.GetValueOrDefault("ReturnUrl") is { } back && DesktopRequests.IsLocal(back) ? back : "/";

    /// <summary>
    /// Runs the route: signs out and shows the signed-out view, whose button calls <paramref name="navigate"/> with the
    /// sign-in route; or drops the stored tokens and signs in through the browser while <paramref name="show"/> shows the
    /// waiting view, answering the page to show next: on the first run the page the server kept for the browser when it
    /// is one of the app's pages, otherwise the <c>ReturnUrl</c>. <see langword="null"/> when there is nothing to show
    /// or a newer navigation took over, and an <see cref="OperationCanceledException"/> when the user cancelled.
    /// </summary>
    public async Task<string?> RunAsync(PageRequest request, Func<Control, Task> show, CancellationTokenSource waiting, Func<Uri, Task> browser, Func<bool> superseded, Action<string> navigate)
    {
        if (Is(request.Path, client.Options.SignOutPath))
        {
            await client.SignOutAsync(waiting.Token);
            if (!superseded())
            {
                await show(SignedOut(() => navigate(Path + "?ReturnUrl=%2F")));
            }

            return null;
        }

        try
        {
            await show(Waiting(waiting.Cancel));
            await client.SignOutAsync(waiting.Token);
            var back = await client.SignInAsync(browser, request.Query.GetValueOrDefault(FirstRun.Marker) == "true", waiting.Token);
            return superseded() ? null : back is not null && DesktopRequests.IsLocal(back) && isPage(back.Split('?', 2)[0]) ? back : ReturnUrl(request.Query);
        }
        catch (OperationCanceledException) when (superseded())
        {
            return null;
        }
    }

    private static bool Is(string route, string path) => string.Equals(route, path, StringComparison.OrdinalIgnoreCase);

    private bool From(string? path) => path is not null && path.StartsWith(Path, StringComparison.OrdinalIgnoreCase);

    private static StackPanel SignedOut(Action signIn)
    {
        var again = new Button { Content = "Sign in", HorizontalAlignment = HorizontalAlignment.Center };
        again.Click += (_, _) => signIn();
        return Centered(new TextBlock { Text = "You are signed out", FontSize = 22, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center }, again);
    }

    private static StackPanel Waiting(Action cancel)
    {
        var giveUp = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Center };
        giveUp.Click += (_, _) => cancel();
        return Centered(
            new TextBlock { Text = "Continue in your browser", FontSize = 22, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = "A browser window opened to sign you in. This window continues once you are signed in.", Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center },
            giveUp);
    }

    private static StackPanel Centered(params Control[] children)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
        panel.Children.AddRange(children);
        return panel;
    }
}
