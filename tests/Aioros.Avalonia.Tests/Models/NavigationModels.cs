using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Editor : ObservableObject
{
    [ObservableProperty]
    public partial bool Bold { get; set; } = true;

    [ObservableProperty]
    public partial int Tab { get; set; } = 1;

    public string? Opened { get; private set; }

    public System.Collections.Immutable.ImmutableArray<string> Commands { get; } = ["Open", "Close"];

    public int Saved { get; private set; }

    [RelayCommand]
    private void Open(string name) => Opened = name;

    [RelayCommand]
    private void Save() => Saved++;

    public int Deleted { get; private set; }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete() => Deleted++;

    private static bool CanDelete() => false;
}

public sealed class MenuRow(string name)
{
    public string Name { get; } = name;
}

public sealed partial class MenuRows(params string[] names) : ObservableObject
{
    public MenuRows()
        : this("First", "Second", "Third")
    {
    }

    public System.Collections.Immutable.ImmutableArray<MenuRow> Items { get; } = [.. names.Select(name => new MenuRow(name))];

    public System.Collections.Immutable.ImmutableList<string> Opened { get; private set; } = [];

    [RelayCommand]
    private void Open(MenuRow item) => Opened = Opened.Add(item.Name);
}

public sealed partial class SortChoice : ObservableObject
{
    public System.Collections.Immutable.ImmutableArray<string> Options { get; } = ["Due Date", "Title"];

    [ObservableProperty]
    public partial string Sort { get; set; } = "Due Date";

    public int Applied { get; private set; }

    [RelayCommand]
    private void Apply() => Applied++;
}

public sealed partial class ValidatedOrder : ObservableObject
{
    public int Quantity
    {
        get;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Order at least one.");
            }

            SetProperty(ref field, value);
        }
    } = 1;
}

public sealed partial class Registration : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A name is required.")]
    public partial string Name { get; set; } = "Ada";

    public int Submitted { get; private set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Submit() => Submitted++;

    public static Registration Invalid()
    {
        var registration = new Registration { Name = string.Empty };
        registration.ValidateAllProperties();
        return registration;
    }
}

public sealed partial class FadedRegistration : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A name is required.")]
    public partial string Name { get; set; } = string.Empty;

    public double Fade { get; set; } = 0.5;

    public static FadedRegistration Invalid()
    {
        var registration = new FadedRegistration();
        registration.ValidateAllProperties();
        return registration;
    }
}

public static class NavigationSamples
{
    public static Notification Saved { get; } = new("Saved", "The file was saved.", NotificationType.Success);

    public static Exception Invalid { get; } = new InvalidOperationException("The value is not valid.");
}

public sealed partial class Enrollment : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A name is required.")]
    public partial string Name { get; set; } = "Ada";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MinLength(3, ErrorMessage = "A code has three letters.")]
    public partial string Code { get; set; } = "ABC";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A start is required.")]
    public partial DateTime? Start { get; set; } = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Unspecified);

    public static Enrollment Invalid()
    {
        var enrollment = new Enrollment { Name = string.Empty, Code = "x", Start = null };
        enrollment.ValidateAllProperties();
        return enrollment;
    }
}

public sealed record Described(string? Description);

public sealed record Crumb(string Name, string Href);

public sealed class Trail(params Crumb[] crumbs)
{
    public ImmutableArray<Crumb> Crumbs { get; } = [.. crumbs];
}

public sealed class Crumbs : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new CrumbItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<CrumbItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is CrumbItem crumb)
        {
            crumb.IsFirst = index == 0;
        }
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        if (container is CrumbItem crumb)
        {
            crumb.IsFirst = newIndex == 0;
        }
    }
}

public sealed class CrumbItem : ContentControl
{
    public static readonly StyledProperty<bool> IsFirstProperty = AvaloniaProperty.Register<CrumbItem, bool>(nameof(IsFirst));

    public bool IsFirst
    {
        get => GetValue(IsFirstProperty);
        set => SetValue(IsFirstProperty, value);
    }
}

public sealed class Folds : ItemsControl
{
    public static readonly StyledProperty<global::Avalonia.Controls.Templates.IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<Folds, global::Avalonia.Controls.Templates.IDataTemplate?>(nameof(ContentTemplate));

    public global::Avalonia.Controls.Templates.IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new Expander();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<Expander>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is Expander expander && !ReferenceEquals(container, item))
        {
            expander.Header = item;
            expander.HeaderTemplate = ItemTemplate;
            expander.Content = item;
            expander.ContentTemplate = ContentTemplate;
        }
    }
}
