using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Controls;

/// <summary>
/// The static title and search metadata of a page or layout, set on its root element (<c>a:Page.Title="Buttons"</c>).
/// The generator reads them from the XAML at build time; a loader of the same page or layout wins over them.
/// </summary>
public sealed class Page : AvaloniaObject
{
    /// <summary>The page's title, or a layout's title for the pages below it that have none.</summary>
    public static readonly AttachedProperty<string?> TitleProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("Title");

    /// <summary>On a layout, the template of the titles below it, with <c>%s</c> where their title goes (Next.js <c>title.template</c>).</summary>
    public static readonly AttachedProperty<string?> TitleTemplateProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("TitleTemplate");

    /// <summary>A title used as written, ignoring the layouts' templates (Next.js <c>title.absolute</c>).</summary>
    public static readonly AttachedProperty<string?> AbsoluteTitleProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("AbsoluteTitle");

    public static readonly AttachedProperty<string?> DescriptionProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("Description");

    public static readonly AttachedProperty<string?> CanonicalProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("Canonical");

    public static readonly AttachedProperty<string?> ImageProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("Image");

    /// <summary>The Open Graph type, <c>website</c> unless set.</summary>
    public static readonly AttachedProperty<string?> TypeProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("Type");

    public static readonly AttachedProperty<bool> IndexableProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, bool>("Indexable", true);

    /// <summary>
    /// Where the page runs, as platform names separated by commas (<c>Desktop</c>, <c>Mobile</c>, <c>Web</c>); a page
    /// that leaves out <c>Web</c> answers on the web with the native-only stand-in.
    /// </summary>
    public static readonly AttachedProperty<string?> PlatformsProperty =
        AvaloniaProperty.RegisterAttached<Page, Control, string?>("Platforms");

    public static string? GetPlatforms(Control control) => control.GetValue(PlatformsProperty);

    public static void SetPlatforms(Control control, string? value) => control.SetValue(PlatformsProperty, value);

    public static string? GetTitle(Control control) => control.GetValue(TitleProperty);

    public static void SetTitle(Control control, string? value) => control.SetValue(TitleProperty, value);

    public static string? GetTitleTemplate(Control control) => control.GetValue(TitleTemplateProperty);

    public static void SetTitleTemplate(Control control, string? value) => control.SetValue(TitleTemplateProperty, value);

    public static string? GetAbsoluteTitle(Control control) => control.GetValue(AbsoluteTitleProperty);

    public static void SetAbsoluteTitle(Control control, string? value) => control.SetValue(AbsoluteTitleProperty, value);

    public static string? GetDescription(Control control) => control.GetValue(DescriptionProperty);

    public static void SetDescription(Control control, string? value) => control.SetValue(DescriptionProperty, value);

    public static string? GetCanonical(Control control) => control.GetValue(CanonicalProperty);

    public static void SetCanonical(Control control, string? value) => control.SetValue(CanonicalProperty, value);

    public static string? GetImage(Control control) => control.GetValue(ImageProperty);

    public static void SetImage(Control control, string? value) => control.SetValue(ImageProperty, value);

    public static string? GetType(Control control) => control.GetValue(TypeProperty);

    public static void SetType(Control control, string? value) => control.SetValue(TypeProperty, value);

    public static bool GetIndexable(Control control) => control.GetValue(IndexableProperty);

    public static void SetIndexable(Control control, bool value) => control.SetValue(IndexableProperty, value);
}
