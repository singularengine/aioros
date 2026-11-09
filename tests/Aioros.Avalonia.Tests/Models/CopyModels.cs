using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A library's copy button, as Aioros.Cosmo's <c>c:CodeBlockCopyButton</c>: its code binds its <c>a:Web.Copies</c> to its own <see cref="Value"/>.</summary>
public sealed class CopyButton : Button
{
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<CopyButton, string?>(nameof(Value));

    public CopyButton() => Bind(Web.CopiesProperty, new Binding(nameof(Value)) { RelativeSource = new RelativeSource(RelativeSourceMode.Self) });

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Button);
}
