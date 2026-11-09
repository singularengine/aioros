using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace Aioros.Avalonia.Tests.Models;

public sealed record Node(string Name, bool IsSeparator = false, bool Visible = true, IReadOnlyList<Node>? Children = null)
{
    public bool Shown { get; set; } = Visible;

    public Feed<string?> Badge { get; } = new(Name[..1], silent: false);
}

public sealed class Tagged(string[]? tags)
{
    public string[]? Tags { get; } = tags;
}

public static class Swatches
{
    public static IReadOnlyList<string> Names { get; } = ["Red", "Blue"];

    public static IReadOnlyList<int> Sizes { get; } = [12, 14];
}

public sealed class Scoped(global::Avalonia.Styling.ThemeVariant? variant)
{
    public global::Avalonia.Styling.ThemeVariant? Variant { get; } = variant;
}

public sealed class Wrapped(Tagged? inner)
{
    public Tagged? Inner { get; } = inner;
}

public sealed class Outlined(params Node[] nodes)
{
    public IReadOnlyList<Node> Nodes { get; } = nodes;

    public string Title { get; init; } = "Menu";
}

/// <summary>A menu shaped like Ursa's <c>NavMenu</c>: its items bind their header and children through bindings the menu holds.</summary>
public sealed class Outline : ItemsControl
{
    public static readonly StyledProperty<BindingBase?> HeaderBindingProperty = AvaloniaProperty.Register<Outline, BindingBase?>(nameof(HeaderBinding));

    public static readonly StyledProperty<BindingBase?> SubItemsBindingProperty = AvaloniaProperty.Register<Outline, BindingBase?>(nameof(SubItemsBinding));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty = AvaloniaProperty.Register<Outline, IDataTemplate?>(nameof(HeaderTemplate));

    public static readonly StyledProperty<BindingBase?> IconBindingProperty = AvaloniaProperty.Register<Outline, BindingBase?>(nameof(IconBinding));

    public static readonly StyledProperty<IDataTemplate?> IconTemplateProperty = AvaloniaProperty.Register<Outline, IDataTemplate?>(nameof(IconTemplate));

    public IDataTemplate? IconTemplate
    {
        get => GetValue(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    public static readonly StyledProperty<double> IndentProperty = AvaloniaProperty.Register<Outline, double>(nameof(Indent), 12);

    [AssignBinding]
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public BindingBase? HeaderBinding
    {
        get => GetValue(HeaderBindingProperty);
        set => SetValue(HeaderBindingProperty, value);
    }

    [AssignBinding]
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public BindingBase? IconBinding
    {
        get => GetValue(IconBindingProperty);
        set => SetValue(IconBindingProperty, value);
    }

    [AssignBinding]
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public BindingBase? SubItemsBinding
    {
        get => GetValue(SubItemsBindingProperty);
        set => SetValue(SubItemsBindingProperty, value);
    }

    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public double Indent
    {
        get => GetValue(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<OutlineItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new OutlineItem();
}

/// <summary>An item of an <see cref="Outline"/>: it takes its level from its place, and its header, children and templates from the menu.</summary>
public sealed class OutlineItem : global::Avalonia.Controls.Primitives.HeaderedItemsControl
{
    public static readonly StyledProperty<bool> IsSeparatorProperty = AvaloniaProperty.Register<OutlineItem, bool>(nameof(IsSeparator));

    public static readonly DirectProperty<OutlineItem, int> LevelProperty = AvaloniaProperty.RegisterDirect<OutlineItem, int>(nameof(Level), item => item.Level, (item, value) => item.Level = value);

    public static readonly StyledProperty<double> IndentProperty = Outline.IndentProperty.AddOwner<OutlineItem>();

    public static readonly StyledProperty<object?> IconProperty = AvaloniaProperty.Register<OutlineItem, object?>(nameof(Icon));

    public static readonly StyledProperty<IDataTemplate?> IconTemplateProperty = Outline.IconTemplateProperty.AddOwner<OutlineItem>();

    public IDataTemplate? IconTemplate
    {
        get => GetValue(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    public static readonly DirectProperty<OutlineItem, bool> IsPickedProperty = AvaloniaProperty.RegisterDirect<OutlineItem, bool>(nameof(IsPicked), item => item.IsPicked);

    /// <summary>Whether a visitor picked the item, as Ursa's <c>NavMenuItem.IsHighlighted</c>: state of the item no binding sets.</summary>
    public bool IsPicked
    {
        get;
        private set => SetAndRaise(IsPickedProperty, ref field, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsSeparator
    {
        get => GetValue(IsSeparatorProperty);
        set => SetValue(IsSeparatorProperty, value);
    }

    public int Level
    {
        get;
        set => SetAndRaise(LevelProperty, ref field, value);
    }

    public double Indent
    {
        get => GetValue(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<OutlineItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new OutlineItem();

    protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        SetCurrentValue(LevelProperty, this.GetLogicalAncestors().TakeWhile(ancestor => ancestor is not Outline).OfType<OutlineItem>().Count() + 1);
        if (this.FindLogicalAncestorOfType<Outline>() is { } root)
        {
            if (root.HeaderBinding is { } header)
            {
                Bind(HeaderProperty, header);
            }

            if (root.IconBinding is { } icon)
            {
                Bind(IconProperty, icon);
            }

            if (root.SubItemsBinding is { } children)
            {
                Bind(ItemsSourceProperty, children);
            }

            this[!HeaderTemplateProperty] = root[!Outline.HeaderTemplateProperty];
            this[!IndentProperty] = root[!Outline.IndentProperty];
            this[!IconTemplateProperty] = root[!Outline.IconTemplateProperty];
        }
    }
}

/// <summary>An item's left padding: its level times the menu's indent.</summary>
public sealed class LevelIndent : IMultiValueConverter
{
    public static LevelIndent Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [double indent, int level] ? new Thickness(indent * level, 2, 0, 2) : AvaloniaProperty.UnsetValue;
}

/// <summary>Turns a name into one of three icons, as Ursa's demo turns each menu item's name into a geometry.</summary>
public sealed class NameIcons : IValueConverter
{
    private static readonly string[] Paths = ["M0,0 L10,0 L10,10 Z", "M0,0 L12,6 L0,12 Z", "M2,2 L8,2 L8,8 L2,8 Z"];

    public static NameIcons Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string name ? global::Avalonia.Media.StreamGeometry.Parse(Paths[name.Length % Paths.Length]) : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A mark that follows whether its item is picked, as Ursa's <c>TwoTonePathIcon.IsActive</c> follows its menu item's highlight.</summary>
public sealed class Picked : global::Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<Picked, bool>(nameof(IsOn));

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }
}
