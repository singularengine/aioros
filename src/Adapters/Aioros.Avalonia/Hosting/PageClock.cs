using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The clock a page's window runs on while the engine realizes and writes it, or answers a post: a clock of its own that
/// never moves, so a transition or an animation started then (an overlay's entrance) stands at its start however long the
/// writing takes, and the page writes it as an entrance; the platform's clock, which moves with real time, would end a
/// short one before the writer reads it. A transition starts at the clock's next pulse, so <see cref="Begin"/> pulses it
/// again at the same time before the entrances are read. Avalonia keeps its clocks internal, so they are reached through
/// compile-time bound accessors, which throw if Avalonia renames them.
/// </summary>
internal static class PageClock
{
    private const string Base = "Avalonia.Animation.ClockBase, Avalonia.Base";
    private const string Interface = "Avalonia.Animation.IClock, Avalonia.Base";

    private static readonly AttachedProperty<object?> ClockProperty = AvaloniaProperty.RegisterAttached<Window, object?>("PageClock", typeof(PageClock));

    /// <summary>Sets the window's still clock, before the page joins it (its controls inherit it).</summary>
    public static void Still(Window window)
    {
        var clock = Create();
        SetClock(window, clock);
        Pulse(clock, TimeSpan.Zero);
        window.SetValue(ClockProperty, clock);
    }

    /// <summary>Starts, at the still clock's time, the transitions and animations begun since its last pulse in the window holding <paramref name="visual"/>.</summary>
    public static void Begin(Visual visual)
    {
        if (TopLevel.GetTopLevel(visual)?.GetValue(ClockProperty) is { } clock)
        {
            Pulse(clock, TimeSpan.Zero);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType(Base)]
    private static extern object Create();

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Clock")]
    private static extern void SetClock(Animatable target, [UnsafeAccessorType(Interface)] object clock);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Pulse")]
    private static extern void Pulse([UnsafeAccessorType(Base)] object clock, TimeSpan time);
}
