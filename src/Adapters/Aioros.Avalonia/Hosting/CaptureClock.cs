using System.Runtime.CompilerServices;
using Avalonia.Animation;
using Avalonia.Controls;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The clock a captured page's animations run on, so the capture shows them as the bench's browser side does (finite
/// animations finished, endless ones at their start) however long it takes: set on the window before the page joins it
/// (its controls inherit it), pulsed to its start and then far past every finite animation's end, at a time every common
/// duration divides, which puts an endless animation back at its start. Avalonia keeps its clocks internal, so they are
/// reached through compile-time bound accessors, which throw if Avalonia renames them.
/// </summary>
internal sealed class CaptureClock
{
    private const string Base = "Avalonia.Animation.ClockBase, Avalonia.Base";
    private const string Interface = "Avalonia.Animation.IClock, Avalonia.Base";

    /// <summary>Ten hours: a whole number of every duration in whole milliseconds that divides 2520 seconds (any of 1 to 10 ms, 50 ms, 0.7 s, 1.2 s...).</summary>
    private static readonly TimeSpan Far = TimeSpan.FromSeconds(2520 * 14);

    private readonly object _clock = Create();
    private int _pulses;

    public void Attach(Window window)
    {
        SetClock(window, _clock);
        Pulse(_clock, TimeSpan.Zero);
    }

    /// <summary>Pulses the clock to its next far time, once the animations and transitions started since the last pulse began at it.</summary>
    public void Settle()
    {
        Pulse(_clock, Far * _pulses);
        Pulse(_clock, Far * ++_pulses);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType(Base)]
    private static extern object Create();

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Clock")]
    private static extern void SetClock(Animatable target, [UnsafeAccessorType(Interface)] object clock);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Pulse")]
    private static extern void Pulse([UnsafeAccessorType(Base)] object clock, TimeSpan time);
}
