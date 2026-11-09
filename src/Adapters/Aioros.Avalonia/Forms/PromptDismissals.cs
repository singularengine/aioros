using Aioros.Avalonia.Html.Writers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Forms;

/// <summary>
/// A prompt dismissed in the browser as Avalonia's user dismisses it: Escape is the key Escape pressed where the focus is
/// in the prompt (on its surface when nothing there has it), and a click outside it is a press on its mask, so the app's
/// dialog decides as on the desktop whether it closes (Radix's Escape and outside click; an alert dialog that ignores the
/// outside press stays open).
/// </summary>
internal static class PromptDismissals
{
    public const string Escape = "escape";

    public const string Outside = "outside";

    public static void Dismiss(Window window, Prompts prompts, string kind)
    {
        Hosting.Realization.LayOut(window);
        var open = prompts.Open().ToList();
        var masks = open.Where(control => control.GetVisualParent() is Panel host && PromptWriters.Mask(control, host.Bounds.Size) is not null).ToList();
        if (kind == Outside && masks.LastOrDefault() is { } mask)
        {
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            mask.RaiseEvent(new PointerPressedEventArgs(mask, pointer, window, new Point(1, 1), 0, properties, KeyModifiers.None));
        }
        else if (kind == Escape && Target(window, open, masks) is { } target)
        {
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = target, Key = Key.Escape });
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static Control? Target(Window window, List<Control> open, List<Control> masks) =>
        window.FocusManager?.GetFocusedElement() is Control focused && open.Any(prompt => prompt == focused || prompt.IsVisualAncestorOf(focused))
            ? focused
            : open.LastOrDefault(control => !masks.Contains(control));
}
