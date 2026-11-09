using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aioros.Avalonia.Tests.Models;

public sealed class PlainPanel : StackPanel
{
    public PlainPanel() => Spacing = 4;
}

public sealed class HandledPanel : StackPanel
{
    public void OnChecked(object? sender, RoutedEventArgs e) => Tag = e.Source;
}

public static class Marks
{
    public static readonly AttachedProperty<string?> NoteProperty = AvaloniaProperty.RegisterAttached<Control, string?>("Note", typeof(Marks));

    public static string? GetNote(Control control) => control.GetValue(NoteProperty);

    public static void SetNote(Control control, string? value) => control.SetValue(NoteProperty, value);
}
