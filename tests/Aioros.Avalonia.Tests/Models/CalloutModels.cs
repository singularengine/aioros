using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Callout : ContentControl
{
    public static readonly StyledProperty<object?> TitleProperty = AvaloniaProperty.Register<Callout, object?>(nameof(Title));

    public static readonly StyledProperty<object?> DescriptionProperty = AvaloniaProperty.Register<Callout, object?>(nameof(Description));

    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}

public sealed class Portrait : ContentControl
{
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<Portrait, IImage?>(nameof(Source));

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}

public sealed class Proportion : Decorator
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<Proportion, double>(nameof(Value), 1, validate: ratio => ratio > 0 && double.IsFinite(ratio));

    static Proportion() => AffectsMeasure<Proportion>(ValueProperty);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsInfinity(availableSize.Width))
        {
            Child?.Measure(availableSize);
        }

        var width = double.IsInfinity(availableSize.Width) ? (Child?.DesiredSize.Width ?? 0) : availableSize.Width;
        var size = new Size(width, width / Value);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = new Size(finalSize.Width, finalSize.Width / Value);
        Child?.Arrange(new Rect(size));
        return size;
    }
}

public static class Pop
{
    public static readonly AttachedProperty<bool> IsOpenProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsOpen", typeof(Pop), defaultBindingMode: BindingMode.TwoWay);

    static Pop()
    {
        IsOpenProperty.Changed.AddClassHandler<Control>((control, change) => Apply(control, change.GetNewValue<bool>()));
        Control.LoadedEvent.AddClassHandler<Control>((control, _) =>
        {
            if (control.IsSet(IsOpenProperty) && GetIsOpen(control))
            {
                Apply(control, true);
            }
        });
    }

    public static bool GetIsOpen(Control control) => control.GetValue(IsOpenProperty);

    public static void SetIsOpen(Control control, bool value) => control.SetValue(IsOpenProperty, value);

    private static void Apply(Control control, bool open)
    {
        if ((control as Button)?.Flyout is not { } flyout || flyout.IsOpen == open)
        {
            return;
        }

        if (open && TopLevel.GetTopLevel(control) is null)
        {
            control.AttachedToVisualTree += Opened;
            void Opened(object? sender, VisualTreeAttachmentEventArgs e)
            {
                control.AttachedToVisualTree -= Opened;
                Apply(control, GetIsOpen(control));
            }
        }
        else if (open)
        {
            flyout.ShowAt(control);
        }
        else
        {
            flyout.Hide();
        }
    }
}

/// <summary>A library's button with a flag of its own that only styles it, as Aioros.Cosmo's <c>SidebarMenuButton.IsActive</c> sets <c>:active</c>.</summary>
public sealed class Tab : Button
{
    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<Tab, bool>(nameof(IsActive));

    static Tab() => IsActiveProperty.Changed.AddClassHandler<Tab>((tab, change) => tab.PseudoClasses.Set(":active", change.GetNewValue<bool>()));

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Tab);
}

public enum PhaseStatus
{
    Done,

    Running,
}

/// <summary>A library's line with an icon whose bound status styles its foreground, as Aioros.Cosmo's <c>TraceLine.Status</c> sets <c>:running</c>.</summary>
public sealed class Phase : ContentControl
{
    public static readonly StyledProperty<object?> IconProperty = AvaloniaProperty.Register<Phase, object?>(nameof(Icon));

    public static readonly StyledProperty<PhaseStatus> StatusProperty = AvaloniaProperty.Register<Phase, PhaseStatus>(nameof(Status));

    static Phase() => StatusProperty.Changed.AddClassHandler<Phase>((phase, change) => phase.PseudoClasses.Set(":running", change.GetNewValue<PhaseStatus>() == PhaseStatus.Running));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public PhaseStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}

public sealed record Bulletin(string Title, string? Description = null, double Shape = 1.5, bool Open = false, IImage? Photo = null, string Initials = "AB", PhaseStatus Phase = PhaseStatus.Done);

public sealed record Bulletins(IList<Bulletin> Items);

public sealed record Frameworks(IList<string> Names, string? Chosen = null, string? Search = null);

public sealed class Choice
{
    public IList<string> Names { get; init; } = ["Next.js", "SvelteKit", "Remix"];

    public string? Chosen { get; set; } = "Remix";
}

public sealed record Framework(string Name, string Slug);

public sealed class FrameworkChoice
{
    public IList<Framework> Items { get; init; } = [new("Next.js", "next"), new("Remix", "remix")];

    public Framework? Chosen { get; set; }
}
