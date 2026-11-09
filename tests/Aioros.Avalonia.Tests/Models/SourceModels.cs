using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Labels(string title, bool flag)
{
    public static Labels Current { get; } = new("Static title", true);

    public string Title => title;

    public bool Flag => flag;
}

public sealed class Phrase(string value) : INotifyPropertyChanged
{
    private static readonly Dictionary<string, Phrase> Shared = new(StringComparer.Ordinal)
    {
        ["greeting"] = new("Hello from a phrase"),
    };

    public string Value => value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Phrase Get(string key) => Shared[key];

    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}

public sealed class Motto
{
    public string Words { get; set; } = string.Empty;
}

public sealed class TExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding { Path = nameof(Phrase.Value), Source = Phrase.Get(key), Mode = BindingMode.OneWay };
}

public sealed class RootExtension(string path) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(path) { Source = ((IRootObjectProvider)serviceProvider.GetService(typeof(IRootObjectProvider))!).RootObject };
}

public static class OuterText
{
    public static readonly AttachedProperty<string?> PathProperty = AvaloniaProperty.RegisterAttached<TextBlock, string?>("Path", typeof(OuterText));

    public static IDisposable Handler { get; } = PathProperty.Changed.AddClassHandler<TextBlock>((block, _) =>
    {
        block.AttachedToLogicalTree += (_, _) => Bind(block);
        block.DataContextChanged += (_, _) => Bind(block);
        Bind(block);
    });

    public static string? GetPath(TextBlock block) => block.GetValue(PathProperty);

    public static void SetPath(TextBlock block, string? value) => block.SetValue(PathProperty, value);

    private static void Bind(TextBlock block)
    {
        if (block.FindLogicalAncestorOfType<ItemsControl>() is { DataContext: { } outer } && GetPath(block) is { } path)
        {
            block.Bind(TextBlock.TextProperty, new Binding(path) { Source = outer });
        }
    }
}
