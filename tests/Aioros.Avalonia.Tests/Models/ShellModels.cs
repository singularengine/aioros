using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A library's app shell, as Aioros.Cosmo's <c>c:SidebarProvider</c>: its side at its own width, the rest beside it, the side collapsing with <see cref="IsOpen"/>.</summary>
public sealed class Shell : Panel
{
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Shell, bool>(nameof(IsOpen), true, defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSheetOpenProperty = AvaloniaProperty.Register<Shell, bool>(nameof(IsSheetOpen));

    public static readonly StyledProperty<double> BreakpointProperty = AvaloniaProperty.Register<Shell, double>(nameof(Breakpoint), 600);

    private TopLevel? _top;

    static Shell()
    {
        AffectsMeasure<Shell>(IsOpenProperty, IsSheetOpenProperty);
        IsOpenProperty.Changed.AddClassHandler<Shell>((shell, _) => shell.Mark());
        IsSheetOpenProperty.Changed.AddClassHandler<Shell>((shell, _) => shell.Mark());
    }

    public Shell() => Children.CollectionChanged += (_, _) => Mark();

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsSheetOpen
    {
        get => GetValue(IsSheetOpenProperty);
        set => SetValue(IsSheetOpenProperty, value);
    }

    public double Breakpoint
    {
        get => GetValue(BreakpointProperty);
        set => SetValue(BreakpointProperty, value);
    }

    /// <summary>Whether the window is narrower than <see cref="Breakpoint"/>, read after its bounds change as Aioros.Cosmo's provider reads it.</summary>
    public bool IsNarrow { get; private set; }

    public ShellSide? Side => Children.OfType<ShellSide>().FirstOrDefault();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is not null)
        {
            _top.PropertyChanged += OnTopChanged;
        }

        Narrow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_top is not null)
        {
            _top.PropertyChanged -= OnTopChanged;
            _top = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnTopChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(Narrow);
        }
    }

    private void Narrow()
    {
        var narrow = _top is not null && _top.Bounds.Width > 0 && _top.Bounds.Width < Breakpoint;
        if (narrow != IsNarrow)
        {
            IsNarrow = narrow;
            InvalidateMeasure();
            Mark();
        }
    }

    public void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Side;
        side?.Measure(IsNarrow ? availableSize : new Size(double.PositiveInfinity, availableSize.Height));
        var width = IsNarrow ? 0 : side?.DesiredSize.Width ?? 0;
        var height = side?.DesiredSize.Height ?? 0;
        foreach (var child in Children.Where(child => child != side))
        {
            child.Measure(new Size(Math.Max(0, availableSize.Width - width), availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width + Children.Where(child => child != side).Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max(), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var side = Side;
        var width = IsNarrow ? 0 : Math.Min(side?.DesiredSize.Width ?? 0, finalSize.Width);
        var right = side is not null && DockPanel.GetDock(side) == Dock.Right;
        side?.Arrange(IsNarrow ? new Rect(finalSize) : new Rect(right ? finalSize.Width - width : 0, 0, width, finalSize.Height));
        foreach (var child in Children.Where(child => child != side))
        {
            child.Arrange(new Rect(right ? 0 : width, 0, Math.Max(0, finalSize.Width - width), finalSize.Height));
        }

        return finalSize;
    }

    private void Mark() => Side?.Follow(IsOpen, IsNarrow, IsSheetOpen);
}

/// <summary>A shell's side: <c>:collapsed</c> while its shell is closed, which its theme narrows to its icons.</summary>
public sealed class ShellSide : ContentControl
{
    public static readonly StyledProperty<bool> RebuildsProperty = AvaloniaProperty.Register<ShellSide, bool>(nameof(Rebuilds));

    public static readonly StyledProperty<string?> CollapsedTipProperty = AvaloniaProperty.Register<ShellSide, string?>(nameof(CollapsedTip));

    public string? CollapsedTip
    {
        get => GetValue(CollapsedTipProperty);
        set => SetValue(CollapsedTipProperty, value);
    }

    public bool Rebuilds
    {
        get => GetValue(RebuildsProperty);
        set => SetValue(RebuildsProperty, value);
    }

    internal void Follow(bool open, bool narrow = false, bool sheet = false)
    {
        PseudoClasses.Set(":collapsed", !open && !narrow);
        PseudoClasses.Set(":narrow", narrow);
        PseudoClasses.Set(":sheet", narrow && sheet);
        if (CollapsedTip is { } tip)
        {
            ToolTip.SetTip(Content as Button ?? (Control)this, open ? null : tip);
        }
        if (Rebuilds)
        {
            Content = new TextBlock { Text = "Fresh" };
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Follow((Parent as Shell)?.IsOpen ?? true, (Parent as Shell)?.IsNarrow ?? false, (Parent as Shell)?.IsSheetOpen ?? false);
    }
}

/// <summary>A button that toggles the shell around it in its own code, as Aioros.Cosmo's <c>c:SidebarTrigger</c>.</summary>
public sealed class ShellToggle : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnClick()
    {
        this.FindLogicalAncestorOfType<Shell>()?.Toggle();
        base.OnClick();
    }
}

/// <summary>A library's collapsible menu item, as Aioros.Cosmo's <c>c:SidebarMenuItem</c>: <c>:expanded</c> while <see cref="IsExpanded"/>, which its button toggles in its own code.</summary>
public sealed class ShellItem : StackPanel
{
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<ShellItem, bool>(nameof(IsExpanded), defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    static ShellItem() => IsExpandedProperty.Changed.AddClassHandler<ShellItem>((item, change) => item.PseudoClasses.Set(":expanded", change.GetNewValue<bool>()));

    public ShellItem() => AddHandler(Button.ClickEvent, (_, e) =>
    {
        if (e.Source is Button button && button.Parent == this)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }
    });

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }
}

public sealed class ShellModel(bool open)
{
    public bool Open { get; set; } = open;
}

/// <summary>A library's content area, as Aioros.Cosmo's <c>c:SidebarInset</c>: a content control of its own type, looking like Avalonia's.</summary>
public sealed class Inset : ContentControl
{
    protected override Type StyleKeyOverride => typeof(ContentControl);
}
