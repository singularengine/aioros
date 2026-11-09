using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace Aioros.Targets.Desktop.Hosting;

/// <summary>
/// Whether Avalonia still has animation work for the coming frames: its global animation clock has a subscriber (a
/// running or delayed <c>Animation</c>, a transition) or an animation frame was requested
/// (<c>TopLevel.RequestAnimationFrame</c>, which charts redraw through). Avalonia keeps this on its media context's clock
/// and does not expose it, so it is read through compile-time bound accessors, which throw if Avalonia renames them.
/// </summary>
public static class PendingAnimations
{
    private const string Context = "Avalonia.Media.MediaContext, Avalonia.Base";
    private const string Clock = "Avalonia.Media.MediaContext+MediaContextClock, Avalonia.Base";
    private const string GlobalClock = "Avalonia.Animation.IGlobalClock, Avalonia.Base";

    /// <summary>Whether an animation, a transition or a requested animation frame is pending, read on the UI thread.</summary>
    public static bool Active
    {
        get
        {
            Dispatcher.UIThread.VerifyAccess();
            return HasSubscriptions(ClockOf(Instance(null)));
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_Instance")]
    [return: UnsafeAccessorType(Context)]
    private static extern object Instance([UnsafeAccessorType(Context)] object? context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Clock")]
    [return: UnsafeAccessorType(GlobalClock)]
    private static extern object ClockOf([UnsafeAccessorType(Context)] object context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_HasSubscriptions")]
    private static extern bool HasSubscriptions([UnsafeAccessorType(Clock)] object clock);
}
