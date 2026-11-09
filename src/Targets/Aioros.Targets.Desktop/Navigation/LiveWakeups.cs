using System.Net.NetworkInformation;
using Aioros.Runtime.Pages;
using Avalonia.Controls;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>
/// Wakes the live stream's reconnection when the window is reactivated or the network becomes available, until the
/// window closes (React Query's <c>refetchOnWindowFocus</c> and <c>refetchOnReconnect</c>, the browser's
/// <c>visibilitychange</c> and <c>online</c>).
/// </summary>
internal static class LiveWakeups
{
    public static void Attach(Window window, LiveReconnection reconnection)
    {
        void Activated(object? sender, EventArgs args) => reconnection.Wake();

        void Available(object? sender, NetworkAvailabilityEventArgs args)
        {
            if (args.IsAvailable)
            {
                reconnection.Wake();
            }
        }

        void Closed(object? sender, EventArgs args)
        {
            window.Activated -= Activated;
            window.Closed -= Closed;
            NetworkChange.NetworkAvailabilityChanged -= Available;
        }

        window.Activated += Activated;
        window.Closed += Closed;
        NetworkChange.NetworkAvailabilityChanged += Available;
    }
}
