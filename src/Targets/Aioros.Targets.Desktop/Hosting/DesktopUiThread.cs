using Aioros.Pages;
using Avalonia.Threading;

namespace Aioros.Targets.Desktop.Hosting;

public sealed class DesktopUiThread : IUiThread
{
    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken) =>
        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Default, cancellationToken);
}
