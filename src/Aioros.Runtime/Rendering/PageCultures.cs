using System.Globalization;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The culture pages render in on the current flow, as ASP.NET Core's request localization sets it for a request: the
/// current culture and UI culture, and the key under which the plans and answers of that culture are kept. An app that
/// does not localize renders in its own culture under the empty key, whatever the process's culture is.
/// </summary>
public static class PageCultures
{
    private static readonly AsyncLocal<string?> Chosen = new();

    /// <summary>The key of the culture the current flow renders in, or the empty key when no culture was chosen.</summary>
    public static string Key => Chosen.Value ?? string.Empty;

    /// <summary>The key of a culture and a UI culture: the name they share, or both names.</summary>
    public static string KeyOf(CultureInfo culture, CultureInfo uiCulture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(uiCulture);
        return culture.Name == uiCulture.Name ? culture.Name : culture.Name + "|" + uiCulture.Name;
    }

    /// <summary>Renders the rest of the current flow in the culture and the UI culture, under their key.</summary>
    public static void Use(CultureInfo culture, CultureInfo uiCulture)
    {
        Chosen.Value = KeyOf(culture, uiCulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = uiCulture;
    }
}
