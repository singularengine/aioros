using Avalonia;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// Refuses a page that holds an Avalonia object created on another thread before Avalonia draws it. An Avalonia object
/// belongs to the thread that created it (<c>AvaloniaObject.Dispatcher</c> is that thread's dispatcher) and
/// <c>VerifyAccess</c> throws on any other; drawn, such a brush throws inside the compositor's commit, which leaves the
/// commit it had started pending forever, so no later frame is ever rendered. Checked here, only that page fails.
/// </summary>
internal static class ForeignObjects
{
    public static void Refuse(Visual root)
    {
        foreach (var visual in root.GetSelfAndVisualDescendants())
        {
            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(visual))
            {
                if (visual.GetValue(property) is AvaloniaObject value && !value.CheckAccess())
                {
                    throw new InvalidOperationException(
                        $"{visual.GetType().Name}.{property.Name} holds a {value.GetType().Name} created on another thread: an Avalonia object belongs to the thread that created it (VerifyAccess), so the page cannot be drawn. Create it on Avalonia's thread, where the page's models are created.");
                }
            }
        }
    }
}
