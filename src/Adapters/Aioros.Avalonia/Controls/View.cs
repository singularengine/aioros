using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Controls;

/// <summary>
/// Places another view by its source, a page without a class included (<c>&lt;a:View Source="/Pages/inbox/Page.axaml"/&gt;</c>).
/// The generator finds it at build time; the view brings its own view model and loader wherever it is placed and does
/// not see its host's data.
/// </summary>
public sealed class View : ContentControl
{
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<View, string?>(nameof(Source));

    internal static readonly AttachedProperty<int> SlotProperty = AvaloniaProperty.RegisterAttached<View, Control, int>("Slot", -1);

    internal static readonly AttachedProperty<string?> KeyProperty = AvaloniaProperty.RegisterAttached<View, Control, string?>("Key");

    internal static readonly AttachedProperty<object?> HeldProperty = AvaloniaProperty.RegisterAttached<View, Control, object?>("Held");

    /// <summary>
    /// The member path of a list item that names the view placed for it in the list's item template
    /// (<c>&lt;v:Card a:View.ItemKey="Id"/&gt;</c>); without it, the item's position names it. The generator reads it at
    /// build time, so the view's batch loader receives each item's key.
    /// </summary>
    public static readonly AttachedProperty<string?> ItemKeyProperty = AvaloniaProperty.RegisterAttached<View, Control, string?>("ItemKey");

    /// <summary>The key of the list item a repeated view's control shows, as its region and fields name it.</summary>
    internal static readonly AttachedProperty<string?> ItemProperty = AvaloniaProperty.RegisterAttached<View, Control, string?>("Item");

    /// <summary>The list item a repeated view's control was built for.</summary>
    internal static readonly AttachedProperty<object?> ListItemProperty = AvaloniaProperty.RegisterAttached<View, Control, object?>("ListItem");

    /// <summary>The view's file, from the project's root (<c>/Pages/inbox/Page.axaml</c>).</summary>
    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);

    /// <summary>The model slot of the view instance this control shows, or <c>-1</c> when it shows none.</summary>
    internal static int GetSlot(Control control) => control.GetValue(SlotProperty);

    internal static string GetKey(Control control) => control.GetValue(KeyProperty) ?? string.Empty;

    public static string? GetItemKey(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(ItemKeyProperty);
    }

    public static void SetItemKey(Control control, string? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ItemKeyProperty, value);
    }

    /// <summary>The key of the list item a repeated view's control shows, or <see langword="null"/> for a view placed once.</summary>
    internal static string? GetItem(Control control) => control.GetValue(ItemProperty);
}
