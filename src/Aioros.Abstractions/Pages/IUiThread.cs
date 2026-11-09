namespace Aioros.Pages;

public interface IUiThread
{
    ValueTask InvokeAsync(Action action, CancellationToken cancellationToken);
}
