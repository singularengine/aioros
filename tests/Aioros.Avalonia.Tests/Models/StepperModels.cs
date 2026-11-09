using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A step's mood, an enum a library control keeps.</summary>
public enum StepMode
{
    Calm,
    Busy,
}

/// <summary>
/// A library's multi-step control, as Aioros.Cosmo's questionnaire: its <see cref="Step"/> shown as text, its Next button
/// only at the first step and its Finish button only at the second, each moving the step in the control's own code.
/// </summary>
public sealed class Stepper : StackPanel
{
    public static readonly StyledProperty<int> StepProperty = AvaloniaProperty.Register<Stepper, int>(nameof(Step));

    public static readonly StyledProperty<StepMode> ModeProperty = AvaloniaProperty.Register<Stepper, StepMode>(nameof(Mode));

    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<Stepper, string?>(nameof(Note));

    private readonly TextBlock _shown = new();
    private readonly Button _next = new() { Content = "Next" };
    private readonly Button _finish = new() { Content = "Finish" };

    public Stepper()
    {
        Children.Add(_shown);
        Children.Add(_next);
        Children.Add(_finish);
        _next.Click += (_, _) => Step = 1;
        _finish.Click += (_, _) => Step = 2;
        Show();
    }

    public int Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public StepMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepProperty)
        {
            Show();
        }
    }

    private void Show()
    {
        _shown.Text = $"Step {Step}";
        _next.IsVisible = Step == 0;
        _finish.IsVisible = Step == 1;
    }
}

/// <summary>A library control holding a choice of its own, hidden while another step shows.</summary>
public sealed class StepPicker : Control
{
    public static readonly StyledProperty<bool?> PickedProperty = AvaloniaProperty.Register<StepPicker, bool?>(nameof(Picked));

    public bool? Picked
    {
        get => GetValue(PickedProperty);
        set => SetValue(PickedProperty, value);
    }
}
