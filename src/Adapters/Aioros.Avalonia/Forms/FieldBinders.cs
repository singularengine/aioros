using System.Collections.Immutable;
using Aioros.Avalonia.Html.Families;
using Aioros.Avalonia.Plans;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Forms;

internal static class FieldBinders
{
    public static ImmutableArray<IFieldBinder> All { get; } =
    [
        new ModalBinder(),
        .. TextFamily.Binders,
        .. RangesFamily.Binders,
        .. ItemsFamily.Binders,
        .. NavigationFamily.Binders,
        .. MediaFamily.Binders,
        .. LayoutFamily.Binders,
        .. ButtonsFamily.Binders,
    ];

    /// <summary>
    /// Binds a posted value into its control, and updates at once the source of a binding that would wait its <c>Delay</c>
    /// first: the post is sent once the visitor paused, so the wait is already over.
    /// </summary>
    public static void Bind(Control control, string? value)
    {
        All.FirstOrDefault(binder => binder.Handles(control))?.Bind(control, value);
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(control))
        {
            if (LocalBindings.Of(control, property) is { } binding && BindingModes.Delay(binding) > TimeSpan.Zero)
            {
                binding.UpdateSource();
            }
        }
    }
}
