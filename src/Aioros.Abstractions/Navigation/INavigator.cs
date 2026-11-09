namespace Aioros.Navigation;

public interface INavigator
{
    ValueTask NavigateAsync(NavigationRequest request, CancellationToken cancellationToken);
}
