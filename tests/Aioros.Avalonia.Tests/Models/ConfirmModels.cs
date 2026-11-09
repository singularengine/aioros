using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Confirm : ContentControl
{
    public static readonly StyledProperty<ICommand?> ConfirmCommandProperty = AvaloniaProperty.Register<Confirm, ICommand?>(nameof(ConfirmCommand));

    public static readonly StyledProperty<object?> ConfirmCommandParameterProperty = AvaloniaProperty.Register<Confirm, object?>(nameof(ConfirmCommandParameter));

    private TextBlock? _state;

    public Confirm() => AddHandler(Button.ClickEvent, OnClicked);

    public ICommand? ConfirmCommand
    {
        get => GetValue(ConfirmCommandProperty);
        set => SetValue(ConfirmCommandProperty, value);
    }

    public object? ConfirmCommandParameter
    {
        get => GetValue(ConfirmCommandParameterProperty);
        set => SetValue(ConfirmCommandParameterProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Confirm);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _state = e.NameScope.Find<TextBlock>("PART_State");
        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConfirmCommandProperty || change.Property == ConfirmCommandParameterProperty)
        {
            Show();
        }
    }

    private void OnClicked(object? sender, RoutedEventArgs e)
    {
        if (ConfirmCommand?.CanExecute(ConfirmCommandParameter) == true)
        {
            ConfirmCommand.Execute(ConfirmCommandParameter);
        }
    }

    private void Show()
    {
        if (_state is not null)
        {
            _state.Text = ConfirmCommand is null ? "none" : ConfirmCommand.CanExecute(ConfirmCommandParameter) ? "ready" : "blocked";
        }
    }
}

public sealed class Gate(bool open) : ICommand
{
    public List<object?> Ran { get; } = [];

    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => open && parameter is not "stop";

    public void Execute(object? parameter) => Ran.Add(parameter);
}

public sealed partial class Guarded(string name, Gate? save) : ObservableObject
{
    [ObservableProperty]
    public partial string Label { get; set; } = name;

    public Gate? Save { get; } = save;
}
