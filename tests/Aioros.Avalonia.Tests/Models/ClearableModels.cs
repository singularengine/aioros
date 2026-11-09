using Avalonia;
using Avalonia.Controls.Primitives;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Clearable : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Clearable, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public void Clear() => SetCurrentValue(TextProperty, null);
}

public sealed class Sections : global::Avalonia.Controls.ItemsControl
{
    protected override Type StyleKeyOverride => typeof(global::Avalonia.Controls.ItemsControl);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<global::Avalonia.Controls.Primitives.HeaderedContentControl>(item, out recycleKey);

    protected override global::Avalonia.Controls.Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new global::Avalonia.Controls.Primitives.HeaderedContentControl();
}

public sealed class Resettable : TemplatedControl
{
    public bool Done { get; private set; }

    public void Reset() => Done = true;

    public bool CanReset(object? parameter) => !Done;
}
