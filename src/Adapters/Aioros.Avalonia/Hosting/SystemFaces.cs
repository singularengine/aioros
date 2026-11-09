using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// Loads every face of a system font family into Avalonia's system font collection before anything asks it for one.
/// Avalonia 12.1.3 resolves a weight a system family lacks to the nearest face it has already cached
/// (<c>SystemFontCollection.TryGetGlyphTypeface</c>), so the face a semibold text gets depends on what the process asked
/// for first: once a page written for the web has asked for the regular face of its family, a later render's semibold
/// falls back to the regular face with a synthetic bold instead of the bold one. With every face cached first, the
/// nearest face is chosen among them all, as CSS Fonts' matching chooses it in the browser, whatever the process
/// rendered before.
/// </summary>
internal static class SystemFaces
{
    private static ImmutableHashSet<string> _loaded = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The families the application names: the default family and every family its styles and resources set.</summary>
    public static void Load(Application application)
    {
        Load(FontManager.Current.DefaultFontFamily);
        Walk(application.Styles);
        Walk(application.Resources);
    }

    /// <summary>The families a composed page names on its controls, in their styles and in their resources.</summary>
    public static void Load(Control root)
    {
        foreach (var control in root.GetSelfAndLogicalDescendants().OfType<Control>())
        {
            if (control.IsSet(TextElement.FontFamilyProperty))
            {
                Load(control.GetValue(TextElement.FontFamilyProperty));
            }

            Walk(control.Styles);
            Walk(control.Resources);
        }
    }

    private static void Load(FontFamily? family)
    {
        if (family is null)
        {
            return;
        }

        foreach (var name in family.FamilyNames)
        {
            Load(name == FontFamily.DefaultFontFamilyName ? FontManager.Current.DefaultFontFamily.Name : name);
        }
    }

    private static void Load(string name)
    {
        var loaded = Volatile.Read(ref _loaded);
        if (loaded.Contains(name) || !ImmutableInterlocked.Update(ref _loaded, static (set, added) => set.Add(added), name))
        {
            return;
        }

        var fonts = FontManager.Current.SystemFonts;
        if (!fonts.TryGetFamilyTypefaces(name, out var faces))
        {
            return;
        }

        foreach (var face in faces)
        {
            fonts.TryGetGlyphTypeface(name, face.Style, face.Weight, face.Stretch, out _);
        }
    }

    private static void Walk(object? value)
    {
        switch (value)
        {
            case FontFamily family:
                Load(family);
                break;
            case ResourceDictionary dictionary:
                WalkAll(dictionary.Values);
                WalkAll(dictionary.MergedDictionaries);
                WalkAll(dictionary.ThemeDictionaries.Values);
                break;
            case global::Avalonia.Markup.Xaml.Styling.ResourceInclude include:
                Walk(include.Loaded);
                break;
            case Styles styles:
                WalkAll(styles);
                Walk(styles.Resources);
                break;
            case StyleBase style:
                WalkAll(style.Setters.OfType<Setter>().Select(static setter => setter.Value));
                WalkAll(style.Children);
                Walk(style.Resources);
                break;
            case global::Avalonia.Markup.Xaml.Styling.StyleInclude include:
                Walk(include.Loaded);
                break;
        }
    }

    private static void WalkAll(System.Collections.IEnumerable values)
    {
        foreach (var value in values)
        {
            Walk(value);
        }
    }
}
