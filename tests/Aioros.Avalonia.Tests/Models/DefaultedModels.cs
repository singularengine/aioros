using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A library control that writes its default into its two-way <see cref="Choice"/> on its first render, as Aioros.Cosmo's <c>c:Command</c> selects its first item, and shows it from its code.</summary>
public sealed class Defaulted : TemplatedControl
{
    public static readonly StyledProperty<string?> ChoiceProperty = AvaloniaProperty.Register<Defaulted, string?>(nameof(Choice), defaultBindingMode: BindingMode.TwoWay);

    private TextBlock? _shown;

    public string? Choice
    {
        get => GetValue(ChoiceProperty);
        set => SetValue(ChoiceProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _shown = e.NameScope.Find<TextBlock>("PART_Shown");
        if (Choice is null)
        {
            SetCurrentValue(ChoiceProperty, "first");
        }

        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChoiceProperty)
        {
            Show();
        }
    }

    private void Show()
    {
        if (_shown is not null)
        {
            _shown.Text = "Chose " + Choice;
        }
    }
}

public sealed class Defaults
{
    public string? Choice { get; set; }
}
