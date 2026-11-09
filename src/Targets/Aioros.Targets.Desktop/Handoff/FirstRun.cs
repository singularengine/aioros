using Aioros.Runtime.SignIn;

namespace Aioros.Targets.Desktop.Handoff;

/// <summary>
/// The installed app's first run after its install. The website kept the page the user came from (the download link
/// carried it) for the browser that downloaded the app, and the only bridge from that browser to the app is the sign-in
/// in that same browser, so the first run signs in first, as Slack and GitHub Desktop do, and asks for that page.
/// </summary>
public static class FirstRun
{
    /// <summary>The query value that marks the sign-in route of the first run.</summary>
    public const string Marker = "FirstRun";

    /// <summary>
    /// The first path: on the first run of an app with the installed-app sign-in, the sign-in route with
    /// <paramref name="start"/> as <c>ReturnUrl</c>; otherwise <paramref name="start"/>.
    /// </summary>
    public static string Path(string start, bool firstRun, InstalledAppSignInOptions? signIn) =>
        firstRun && signIn is not null ? $"{signIn.SignInPath}?ReturnUrl={Uri.EscapeDataString(start)}&{Marker}=true" : start;
}
