using Avalonia.Controls;

namespace Aioros.Avalonia.Html;

internal interface IControlWriter
{
    bool Handles(Control control);

    /// <summary>Whether the writer opens the control's element with <see cref="HtmlContext.Root"/>, so its bound box values are written in its inline style.</summary>
    bool Rooted { get; }

    void Write(HtmlContext context, Control control, ParentKind parent);
}

internal sealed class ControlWriter<T>(Action<HtmlContext, T, ParentKind> write, Func<T, bool>? when = null) : IControlWriter
    where T : Control
{
    public bool Rooted { get; init; }

    public bool Handles(Control control) => control is T typed && (when is null || when(typed));

    public void Write(HtmlContext context, Control control, ParentKind parent) => write(context, (T)control, parent);
}
