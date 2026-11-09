using Aioros.Markup;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The headless window a page is realized in on the engine's thread: mounted at the desktop size in the request's theme,
/// settled (styled, realized and laid out), and released with its page after use.
/// </summary>
internal static class PageWindows
{
    public static T With<T>(Func<Control> compose, string theme, Func<Window, Control, T> body, Action<Window>? prepare = null)
    {
        var (window, root) = Mount(compose, theme, prepare);
        try
        {
            Settle(window);
            return body(window, root);
        }
        finally
        {
            Unmount(window, root);
        }
    }

    /// <summary>Mounts the page in a new window, which <paramref name="prepare"/> sets up before the page joins it (a capture's clock).</summary>
    public static (Window Window, Control Root) Mount(Func<Control> compose, string theme, Action<Window>? prepare = null)
    {
        var root = compose();
        var window = new Window
        {
            Width = 1280,
            Height = 800,
            RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        PageClock.Still(window);
        Web.SetIsWritten(window, true);
        prepare?.Invoke(window);
        window.Content = root;
        return (window, root);
    }

    public static void Unmount(Window window, Control root)
    {
        PageComposer.Release(root);
        window.Close();
    }

    public static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.ApplyStyling();
        Realization.Realize(window);
        CultureDefaults.Apply(window);
        Dispatcher.UIThread.RunJobs();
    }
}
