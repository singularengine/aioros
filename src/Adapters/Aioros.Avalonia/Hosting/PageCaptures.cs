using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// A page's frame as Avalonia draws it, at the requested size and state, with the texts it shows, its animations as the
/// bench's browser side shows them (finite ones finished, endless ones at their start, <see cref="CaptureClock"/>), for
/// the compatibility bench.
/// </summary>
internal static class PageCaptures
{
    /// <summary>Runs a capture with a clock of its own for the page's animations, set on its window before the page joins it.</summary>
    public static T With<T>(Func<CaptureClock, T> capture) => capture(new CaptureClock());

    public static PageCapture Capture(Window window, CaptureOptions options, CaptureClock clock)
    {
        window.Width = options.Width;
        window.Height = options.Height;
        ForeignObjects.Refuse(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        clock.Settle();
        CaptureStates.Force(window, options.State);
        Dispatcher.UIThread.RunJobs();
        clock.Settle();
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Avalonia rendered no frame.");
        using var stream = new MemoryStream();
        frame.Save(stream, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        return new PageCapture(stream.ToArray(), CapturedTexts.Of(window));
    }
}
