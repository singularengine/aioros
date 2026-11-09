using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

internal static class Realization
{
    /// <summary>
    /// Lays the page out at the window's size before a command runs, as a shown window has, so a control the command
    /// places from its host's size (an overlay dialog host sizes its mask and centers its dialog in its own box) finds it.
    /// </summary>
    public static void Measure(Window window)
    {
        if (window.Content is Control page)
        {
            var size = new Size(window.Width, window.Height);
            page.Measure(size);
            page.Arrange(new Rect(size));
        }
    }

    /// <summary>
    /// Shows the window and lays it out as Avalonia does before drawing it, so a control a command placed from its host's
    /// size (an overlay dialog host centers its dialog when it learns its size) lands where the window shows it.
    /// </summary>
    public static void LayOut(Window window)
    {
        ForeignObjects.Refuse(window);
        window.Show();
        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    public static void Realize(Visual visual)
    {
        if (visual is TextBlock or TextBox)
        {
            ((Control)visual).ApplyStyling();
            return;
        }

        if (visual is Control control)
        {
            control.ApplyStyling();
            control.ApplyTemplate();
        }

        foreach (var child in visual.GetVisualChildren().ToList())
        {
            Realize(child);
        }
    }
}
