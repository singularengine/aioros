using Avalonia.Controls;
using Avalonia.Input;

namespace Aioros.Avalonia.Html.Writers;

/// <summary>
/// A control's hot key (Avalonia's <see cref="HotKeyManager"/>, <c>Button.HotKey</c>), written as the shortcut the
/// framework's keyboard module runs from anywhere in the document (<c>data-aioros-key</c>, the browser's key code
/// after its modifiers) and as WAI-ARIA's <c>aria-keyshortcuts</c>.
/// </summary>
internal static class HotKeys
{
    private static readonly Dictionary<Key, (string Code, string Name)> Named = new()
    {
        [Key.Enter] = ("Enter", "Enter"), [Key.Escape] = ("Escape", "Escape"), [Key.Space] = ("Space", "Space"), [Key.Tab] = ("Tab", "Tab"),
        [Key.Back] = ("Backspace", "Backspace"), [Key.Delete] = ("Delete", "Delete"), [Key.Insert] = ("Insert", "Insert"), [Key.Home] = ("Home", "Home"),
        [Key.End] = ("End", "End"), [Key.PageUp] = ("PageUp", "PageUp"), [Key.PageDown] = ("PageDown", "PageDown"), [Key.Left] = ("ArrowLeft", "ArrowLeft"),
        [Key.Up] = ("ArrowUp", "ArrowUp"), [Key.Right] = ("ArrowRight", "ArrowRight"), [Key.Down] = ("ArrowDown", "ArrowDown"),
        [Key.OemQuestion] = ("Slash", "/"), [Key.OemComma] = ("Comma", ","), [Key.OemPeriod] = ("Period", "."), [Key.OemMinus] = ("Minus", "-"),
        [Key.OemPlus] = ("Equal", "="), [Key.OemSemicolon] = ("Semicolon", ";"), [Key.OemQuotes] = ("Quote", "'"), [Key.OemOpenBrackets] = ("BracketLeft", "["),
        [Key.OemCloseBrackets] = ("BracketRight", "]"), [Key.OemPipe] = ("Backslash", "\\"), [Key.OemTilde] = ("Backquote", "`"),
    };

    public static string Attributes(Control control) => Attributes(HotKeyManager.GetHotKey(control));

    /// <summary>The attributes of a key gesture, or nothing for a key the browser's codes do not name.</summary>
    public static string Attributes(KeyGesture? hotKey)
    {
        if (hotKey is not { } gesture || Code(gesture.Key) is not { } key)
        {
            return string.Empty;
        }

        var modifiers = gesture.KeyModifiers;
        var ctrl = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var code = (ctrl ? "ctrl+" : string.Empty) + (modifiers.HasFlag(KeyModifiers.Alt) ? "alt+" : string.Empty) + (modifiers.HasFlag(KeyModifiers.Shift) ? "shift+" : string.Empty) + key.Code;
        var aria = string.Concat(
            modifiers.HasFlag(KeyModifiers.Control) ? "Control+" : string.Empty,
            modifiers.HasFlag(KeyModifiers.Meta) ? "Meta+" : string.Empty,
            modifiers.HasFlag(KeyModifiers.Alt) ? "Alt+" : string.Empty,
            modifiers.HasFlag(KeyModifiers.Shift) ? "Shift+" : string.Empty,
            key.Name);
        return $" data-aioros-key=\"{code}\" aria-keyshortcuts=\"{Looks.CssValues.Encode(aria)}\"";
    }

    /// <summary>The attribute of the keys a button answers inside its key scope (<c>a:Web.Keys</c>), the gestures the browser's codes name, or nothing.</summary>
    public static string Scoped(string? gestures)
    {
        var codes = (gestures ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Gesture).Where(static code => code is not null).ToArray();
        return codes.Length == 0 ? string.Empty : $" data-aioros-keys=\"{string.Join(' ', codes)}\"";
    }

    private static string? Gesture(string text)
    {
        try
        {
            return Attributes(KeyGesture.Parse(text)) is { Length: > 0 } attributes ? attributes.Split('"')[1] : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static (string Code, string Name)? Code(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ("Key" + key, key.ToString()),
        >= Key.D0 and <= Key.D9 => ("Digit" + (key - Key.D0), (key - Key.D0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        >= Key.F1 and <= Key.F24 => (key.ToString(), key.ToString()),
        _ => Named.TryGetValue(key, out var named) ? named : null,
    };
}
