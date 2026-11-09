using Aioros.Markup;
using Aioros.Avalonia.Controls;
using Avalonia.Controls;

namespace Aioros.Avalonia.Forms;

internal static class ControlIds
{
    public static bool OpensPopover(Button button) => Web.GetToggles(button) is not null || button.Flyout is not null;
}
