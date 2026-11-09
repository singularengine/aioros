using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Aioros.Avalonia.Tests.Fuzzing;

/// <summary>
/// Pages generated from Avalonia's own metadata: any public control of <c>Avalonia.Controls</c> with a public constructor,
/// nested as its content model allows, each given a few of its settable properties with values of their types, so the
/// pages reach what Avalonia accepts rather than what a grammar written by hand thought of.
/// </summary>
public static class AvaloniaPages
{
    private static readonly string[] Excluded =
    [
        "Window", "WindowBase", "TopLevel", "EmbeddableControlRoot", "PopupRoot", "OverlayPopupHost", "Popup", "NativeControlHost",
        "AdornerLayer", "OverlayLayer", "VisualLayerManager", "LightDismissOverlayLayer", "ChromeOverlayLayer", "TextSelectorLayer",
        "ContextMenu", "ToolTip", "NativeMenuBar", "CaptionButtons", "TitleBar", "TextSelectionHandle", "MenuFlyoutPresenter",
        "FlyoutPresenter", "Track", "SelectingItemsControl", "DateTimePickerPanel", "DatePickerPresenter", "TimePickerPresenter",
        "PickerPresenterBase", "ReversibleStackPanel", "VirtualizingCarouselPanel", "VirtualizingStackPanel", "TabStrip", "TabStripItem",
        "TemplatedControl", "Control", "HeaderedSelectingItemsControl", "SplitButton", "ToggleSplitButton", "DropDownButton",
    ];

    private static readonly string[] Unset =
    [
        "Name", "Classes", "DataContext", "Template", "Theme", "Tag", "Cursor", "FlowDirection", "Content", "Child", "Header",
        "IsVisible", "ZIndex", "ClipToBounds", "UseLayoutRounding", "Focusable", "IsTabStop", "TabIndex", "IsHitTestVisible",
        "FontFamily", "SelectedIndex", "SelectionMode", "AutoScrollToSelectedItem", "IsDropDownOpen", "IsPaneOpen", "IsExpanded",
        "IsSubMenuOpen", "MaxLines", "ItemCount", "Value", "Minimum", "Maximum",
    ];

    private static readonly string[] Words = ["One", "Two", "Three", "Alpha", "Beta", "Save", "Cancel"];

    private static readonly string[] Brushes = ["#C2185B", "#1565C0", "#2E7D32", "#FFF3E0", "#EEEEEE", "#212121"];

    private static readonly Lazy<Type[]> Controls = new(static () =>
    [
        .. typeof(Control).Assembly.GetExportedTypes()
            .Where(static type => !type.IsAbstract && !type.IsGenericType && !type.IsNested && typeof(Control).IsAssignableFrom(type)
                && type.Namespace is "Avalonia.Controls" or "Avalonia.Controls.Primitives" or "Avalonia.Controls.Shapes"
                && type.GetConstructor(Type.EmptyTypes) is { IsPublic: true } && !Excluded.Contains(type.Name))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal),
    ]);

    public static string Generate(int seed)
    {
        var random = new Random(seed);
        var body = new StringBuilder();
        var count = random.Next(1, 4);
        for (var index = 0; index < count; index++)
        {
            body.Append(Element(random, 0));
        }

        return $"<StackPanel Spacing=\"8\">{body}</StackPanel>";
    }

    private static string Element(Random random, int depth)
    {
        var type = Controls.Value[random.Next(Controls.Value.Length)];
        var attributes = Attributes(random, type);
        var content = Content(random, type, depth);
        return content.Length == 0 ? $"<{type.Name}{attributes} />" : $"<{type.Name}{attributes}>{content}</{type.Name}>";
    }

    /// <summary>What a control holds as its content model allows: a panel's children, a list's items, a content control's text or child, a decorator's child.</summary>
    private static string Content(Random random, Type type, int depth)
    {
        var nested = depth < 2;
        if (typeof(Panel).IsAssignableFrom(type))
        {
            return nested ? string.Concat(Enumerable.Range(0, random.Next(1, 4)).Select(_ => Element(random, depth + 1))) : string.Empty;
        }

        if (typeof(ItemsControl).IsAssignableFrom(type))
        {
            return string.Concat(Enumerable.Range(0, random.Next(1, 4)).Select(_ => $"<x:String>{Words[random.Next(Words.Length)]}</x:String>"));
        }

        if (typeof(ContentControl).IsAssignableFrom(type))
        {
            return nested && random.Next(3) == 0 ? Element(random, depth + 1) : Words[random.Next(Words.Length)];
        }

        if (typeof(Decorator).IsAssignableFrom(type) && nested)
        {
            return Element(random, depth + 1);
        }

        return string.Empty;
    }

    private static string Attributes(Random random, Type type)
    {
        var settable = AvaloniaPropertyRegistry.Instance.GetRegistered(type)
            .Where(static property => !property.IsReadOnly && !Unset.Contains(property.Name) && property.OwnerType.Namespace?.StartsWith("Avalonia", StringComparison.Ordinal) == true)
            .Where(property => Value(new Random(0), property) is not null)
            .DistinctBy(static property => property.Name)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToList();
        var attributes = new StringBuilder();
        foreach (var property in settable.OrderBy(_ => random.Next()).Take(random.Next(0, 4)))
        {
            attributes.Append(CultureInfo.InvariantCulture, $" {property.Name}=\"{Value(random, property)}\"");
        }

        if (type == typeof(TextBlock) || type == typeof(SelectableTextBlock))
        {
            attributes.Append(CultureInfo.InvariantCulture, $" Text=\"{Words[random.Next(Words.Length)]}\"");
        }

        if (typeof(RangeBase).IsAssignableFrom(type))
        {
            attributes.Append(" Minimum=\"0\" Maximum=\"10\" Value=\"4\"");
        }

        return attributes.ToString();
    }

    /// <summary>A value of the property's type as XAML writes it, or nothing for a type the generator does not make.</summary>
    private static string? Value(Random random, AvaloniaProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return type switch
        {
            _ when type == typeof(bool) => random.Next(2) == 0 ? "True" : "False",
            _ when type == typeof(double) => Number(random, property.Name).ToString(CultureInfo.InvariantCulture),
            _ when type == typeof(int) => random.Next(0, 4).ToString(CultureInfo.InvariantCulture),
            _ when type == typeof(string) => Words[random.Next(Words.Length)],
            _ when type.IsEnum && type.GetCustomAttribute<FlagsAttribute>() is null => Enum.GetNames(type)[random.Next(Enum.GetNames(type).Length)],
            _ when type == typeof(Thickness) => string.Join(',', Enumerable.Range(0, 4).Select(_ => random.Next(0, 13))),
            _ when type == typeof(CornerRadius) => random.Next(0, 13).ToString(CultureInfo.InvariantCulture),
            _ when type == typeof(IBrush) => Brushes[random.Next(Brushes.Length)],
            _ when type == typeof(Color) => Brushes[random.Next(Brushes.Length)],
            _ when type == typeof(FontWeight) => ((string[])["Normal", "Bold", "Light", "SemiBold"])[random.Next(4)],
            _ => null,
        };
    }

    private static double Number(Random random, string name) => name switch
    {
        _ when name.Contains("Opacity", StringComparison.Ordinal) => random.Next(1, 11) / 10.0,
        _ when name.Contains("FontSize", StringComparison.Ordinal) => random.Next(10, 25),
        _ when name.Contains("Width", StringComparison.Ordinal) || name.Contains("Height", StringComparison.Ordinal) || name.Contains("Size", StringComparison.Ordinal) => random.Next(10, 241),
        _ => random.Next(0, 25),
    };
}
