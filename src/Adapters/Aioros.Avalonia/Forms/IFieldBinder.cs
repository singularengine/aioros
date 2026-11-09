using Avalonia.Controls;

namespace Aioros.Avalonia.Forms;

internal interface IFieldBinder
{
    bool Handles(Control control);

    void Bind(Control control, string? value);
}

internal sealed class FieldBinder<T>(Action<T, string?> bind) : IFieldBinder
    where T : Control
{
    public bool Handles(Control control) => control is T;

    public void Bind(Control control, string? value) => bind((T)control, value);
}

/// <summary>Binds the check box of an overlay owner marked <c>a:Web.Modal</c> whose open property is bound two way.</summary>
internal sealed class ModalBinder : IFieldBinder
{
    public bool Handles(Control control) => Html.Variants.BrowserModals.Property(control) is not null;

    public void Bind(Control control, string? value) => Html.Variants.BrowserModals.Bind(control, value);
}
