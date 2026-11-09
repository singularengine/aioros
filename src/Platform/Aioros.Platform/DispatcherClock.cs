using System.Reflection;
using Avalonia.Threading;

namespace Aioros.Platform;

/// <summary>
/// Works around Avalonia issue 22064 (open on 2026-10-10, Avalonia 12.1.x): the UI dispatcher takes its clock from a
/// stopwatch started when it is built, while the platform's run loop compares timer due times against a clock of its
/// own started later, so on Wayland every timer fires 120 to 260 ms late and animations run at 4 to 7 frames a second
/// unless input keeps arriving. Pointing the dispatcher's clock at the platform's makes both agree (60 frames a second,
/// measured on Hyprland with NVIDIA and AMD). It is skipped when a later Avalonia no longer has the fields.
/// </summary>
internal static class DispatcherClock
{
    public static bool Align()
    {
        var dispatcher = Dispatcher.UIThread;
        var fields = typeof(Dispatcher).GetRuntimeFields().Where(field => !field.IsStatic).ToList();
        var provider = fields.FirstOrDefault(field => field.Name == "_timeProvider");
        if (provider is null || provider.FieldType != typeof(Func<long>)
            || fields.FirstOrDefault(field => field.Name == "_impl")?.GetValue(dispatcher) is not { } platform
            || platform.GetType().GetInterfaces().Select(contract => contract.GetProperty("Now")).FirstOrDefault(now => now?.PropertyType == typeof(long)) is not { } now)
        {
            return false;
        }

        provider.SetValue(dispatcher, new Func<long>(() => (long)now.GetValue(platform)!));
        return true;
    }
}
