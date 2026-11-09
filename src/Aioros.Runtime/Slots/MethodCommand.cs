using System.Globalization;
using System.Windows.Input;

namespace Aioros.Runtime.Slots;

/// <summary>
/// A view model method run as a command, as Avalonia runs a <c>Command</c> bound to a method (its
/// <c>MethodToCommandConverter</c>): the method takes the command parameter when it has one, and a
/// <c>Can&lt;Method&gt;(object)</c> method, when there is one, says whether it can run; a command action of an MVVM
/// framework (Stylet's <c>{s:Action Method}</c>) is guarded by a <c>Can&lt;Method&gt;</c> property instead. A plan reads
/// one through the member <c>Method()</c>, or <c>Method(Guard)</c> for a guard property, of the method's owner.
/// </summary>
public sealed class MethodCommand(Func<object?, ValueTask> execute, Func<object?, bool>? canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _ = ExecuteAsync(parameter).AsTask();

    /// <summary>Runs the method; the task ends when an asynchronous method has ended.</summary>
    public ValueTask ExecuteAsync(object? parameter) => execute(parameter);

    /// <summary>Whether a member a plan reads names a method command (<c>Save()</c>, <c>Save(CanSave)</c>), with its method and guard.</summary>
    public static bool TryParse(string member, out string method, out string? guard)
    {
        ArgumentNullException.ThrowIfNull(member);
        var open = member.IndexOf('(', StringComparison.Ordinal);
        method = open > 0 ? member[..open] : member;
        guard = open > 0 && member.Length - open > 2 ? member[(open + 1)..^1] : null;
        return open > 0 && member.EndsWith(')');
    }

    /// <summary>The command parameter as the method's parameter type, converted as Avalonia converts it, or its default.</summary>
    public static T Argument<T>(object? parameter) => Argument(parameter, typeof(T)) is T value ? value : default!;

    public static object? Argument(object? parameter, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (parameter is null || target.IsInstanceOfType(parameter))
        {
            return parameter;
        }

        if (parameter is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
        {
            try
            {
                return Convert.ChangeType(parameter, target, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                return null;
            }
        }

        return null;
    }
}
