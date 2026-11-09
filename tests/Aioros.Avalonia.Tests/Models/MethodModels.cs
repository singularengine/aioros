using System.Windows.Input;
using Avalonia;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

public sealed partial class Greeter : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public List<string> Greeted { get; } = [];

    public bool CanWave => Name.Length > 0;

    public void SayHello() => Greeted.Add("hello " + Name);

    public bool CanSayHello(object? parameter) => Name.Length > 0;

    public async Task ArchiveAsync(int days)
    {
        await Task.Yield();
        Greeted.Add("archived " + days);
    }

    public void Wave() => Greeted.Add("wave " + Name);
}

/// <summary>An attached target, inherited, as an MVVM framework's views hand their actions the view model.</summary>
public static class Act
{
    public static readonly AttachedProperty<object?> TargetProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, object?>("Target", typeof(Act), inherits: true);

    public static object? GetTarget(AvaloniaObject element) => element.GetValue(TargetProperty);

    public static void SetTarget(AvaloniaObject element, object? value) => element.SetValue(TargetProperty, value);
}

/// <summary>A command action shaped as Stylet's: a method name, a target and a <c>Can&lt;Method&gt;</c> guard property, run by reflection.</summary>
public sealed class ShapedAction : AvaloniaObject, ICommand
{
    public static readonly StyledProperty<object?> TargetProperty = AvaloniaProperty.Register<ShapedAction, object?>(nameof(Target));

    public ShapedAction(StyledElement subject, string methodName)
    {
        MethodName = methodName;
        Bind(TargetProperty, subject.GetObservable(Act.TargetProperty));
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public string MethodName { get; }

    public object? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public bool CanExecute(object? parameter) => Target?.GetType().GetProperty("Can" + MethodName)?.GetValue(Target) is not false;

    public void Execute(object? parameter) => Target?.GetType().GetMethod(MethodName)?.Invoke(Target, []);
}

public sealed class ActExtension(string method) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget { TargetObject: StyledElement subject }
            ? new ShapedAction(subject, method)
            : this;
}

/// <summary>A command set where it is declared rather than bound to the model, as a library's own command is.</summary>
public static class KeyCommands
{
    public static ICommand Quit { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(static () => { });
}
