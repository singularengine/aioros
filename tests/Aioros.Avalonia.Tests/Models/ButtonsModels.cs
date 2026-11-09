using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Toggles : ObservableObject
{
    [ObservableProperty]
    public partial bool Bold { get; set; } = true;

    [ObservableProperty]
    public partial bool Italic { get; set; }

    [ObservableProperty]
    public partial bool? Mixed { get; set; } = true;

    public List<SpinDirection> Spins { get; } = [];

    public int Repeats { get; private set; }

    public int Sends { get; private set; }

    [RelayCommand]
    private void Repeat() => Repeats++;

    [RelayCommand]
    private void Send() => Sends++;
}

public sealed partial class Choices : ObservableObject
{
    [ObservableProperty]
    public partial bool Small { get; set; }

    [ObservableProperty]
    public partial bool Medium { get; set; } = true;

    [ObservableProperty]
    public partial bool Large { get; set; }

    [ObservableProperty]
    public partial bool Red { get; set; } = true;

    [ObservableProperty]
    public partial bool Blue { get; set; }
}

public sealed class SpinProbe : ButtonSpinner
{
    public SpinProbe() => Spin += (_, args) => (DataContext as Toggles)?.Spins.Add(args.Direction);

    protected override Type StyleKeyOverride => typeof(ButtonSpinner);
}

/// <summary>A button that marks itself <c>:bare</c> when its template is applied without content, as Ursa's <c>IconButton</c> takes <c>:empty-content</c>.</summary>
public sealed class Marked : global::Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(global::Avalonia.Controls.Button);

    protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        PseudoClasses.Set(":bare", Content is null);
    }
}

public enum Sized
{
    Small,
    Medium,
    Large,
}

public sealed partial class SizedChoice : ObservableObject
{
    [ObservableProperty]
    public partial Sized Size { get; set; } = Sized.Medium;
}

public sealed class SizeIs : global::Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter as string, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : global::Avalonia.Data.BindingOperations.DoNothing;
}

public sealed partial class ParityColumn : ObservableObject
{
    [ObservableProperty]
    public partial bool Shown { get; set; } = true;
}

public sealed partial class ParityModel : ObservableObject
{
    public IReadOnlyList<string> Plans { get; } = ["Free", "Pro", "Team"];

    public IReadOnlyList<ParityColumn> Columns { get; } = [new ParityColumn(), new ParityColumn()];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string Title { get; set; } = "Report";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool Agree { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string Plan { get; set; } = "Free";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial Sized Size { get; set; } = Sized.Medium;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial Sized Print { get; set; } = Sized.Small;

    public string Summary => $"{Title} {Agree} {Plan} {Size} {Print} {Columns[1].Shown}";
}
