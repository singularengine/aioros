using System.Collections.Frozen;
using Aioros.Runtime.Rendering;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// Forces an interaction state on every input element of a window about to be captured, the way the compatibility bench
/// forces the matching CSS pseudo-classes in Chromium, with transitions off so the frame shows the state's final look.
/// </summary>
internal static class CaptureStates
{
    private static readonly FrozenDictionary<string, string[]> PseudoClasses = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [CaptureOptions.Normal] = [],
        ["pointerover"] = [":pointerover"],
        ["pressed"] = [":pointerover", ":pressed"],
        ["focus"] = [":focus", ":focus-visible"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static void Force(Window window, string state)
    {
        if (!PseudoClasses.TryGetValue(state, out var names))
        {
            throw new ArgumentException($"Unknown capture state '{state}'; expected one of {string.Join(", ", CaptureOptions.States)}.", nameof(state));
        }

        if (names.Length == 0)
        {
            return;
        }

        foreach (var element in window.GetVisualDescendants().OfType<InputElement>().Where(element => element.IsEffectivelyEnabled).Prepend(window).ToList())
        {
            element.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation);
            var classes = (IPseudoClasses)element.Classes;
            Array.ForEach(names, name => classes.Set(name, true));
        }
    }
}
