using Aioros.ReactiveUI.Tests.Models;
using Aioros.ReactiveUI.Tests.Navigation;

namespace Aioros.ReactiveUI.Tests.Activation;

public sealed class ReactiveActivationTests
{
    [Fact]
    [Trait("AC", "REACTIVE-06")]
    public async Task ViewModelsActivateOncePerPageAsync()
    {
        await using var site = new ReactiveSite();
        var page = ReactiveSite.Page<ActivatedViewModel>("/activated");

        var (first, _) = await site.OpenAsync(page);
        var loaded = (site.Activations.Started, site.Activations.Ended);
        await first.DisposeAsync();
        var ended = (site.Activations.Started, site.Activations.Ended);
        var (second, _) = await site.OpenAsync(page);
        await using var _ = second;

        Assert.Equal((1, 0), loaded);
        Assert.Equal((1, 1), ended);
        Assert.Equal((2, 1), (site.Activations.Started, site.Activations.Ended));
    }
}
