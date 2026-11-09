using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Navigation;

/// <summary>
/// What a view holds that its view model does not, kept across a hot reload: each scroll viewer's offset (in the order they
/// appear), the named element with the focus, and the text of named text boxes whose text is not bound.
/// </summary>
internal sealed record ViewState(ImmutableArray<Vector> Offsets, string? Focused, ImmutableDictionary<string, string> Texts)
{
    public static ViewState Of(Window window)
    {
        var visuals = window.GetVisualDescendants().ToList();
        return new ViewState(
            [.. visuals.OfType<ScrollViewer>().Select(static viewer => viewer.Offset)],
            (TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement() as Control)?.Name,
            visuals.OfType<TextBox>().Where(static box => box.Name is { Length: > 0 } && Unbound(box)).GroupBy(static box => box.Name!)
                .ToImmutableDictionary(static boxes => boxes.Key, static boxes => boxes.First().Text ?? string.Empty));
    }

    /// <summary>
    /// Puts the state back once the reloaded views are laid out, the scroll last, since focusing an element brings it into
    /// view.
    /// </summary>
    public void Restore(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Dispatcher.UIThread.Post(() =>
        {
            window.UpdateLayout();
            var visuals = window.GetVisualDescendants().ToList();
            foreach (var box in visuals.OfType<TextBox>().Where(box => box.Name is { } name && Texts.ContainsKey(name) && Unbound(box)))
            {
                box.Text = Texts[box.Name!];
            }

            if (Focused is not null && visuals.OfType<InputElement>().FirstOrDefault(element => (element as Control)?.Name == Focused) is { } focused)
            {
                focused.Focus();
            }

            window.UpdateLayout();
            foreach (var (viewer, offset) in visuals.OfType<ScrollViewer>().Zip(Offsets))
            {
                viewer.Offset = offset;
            }
        }, DispatcherPriority.Loaded);
    }

    private static bool Unbound(TextBox box) => BindingOperations.GetBindingExpressionBase(box, TextBox.TextProperty) is null;
}
