using Aioros.Avalonia.Commands;
using Aioros.ReactiveUI.Tests.Models;
using Aioros.Runtime.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.ReactiveUI.Tests.Navigation;

public sealed class ReactiveNavigationTests
{
    [Fact]
    [Trait("AC", "REACTIVE-02")]
    public async Task RouterNavigationsGoToTheMappedRouteAsync()
    {
        await using var site = new ReactiveSite();
        var (scope, models) = await site.OpenAsync(ReactiveSite.Page<StoreViewModel>("/store"));
        await using var _ = scope;
        var store = (StoreViewModel)models[^1]!;

        await AwaitedCommands.RunAsync(store.OpenAlbum, 7);
        var album = Navigator(scope).Requested;
        await AwaitedCommands.RunAsync(store.Search, null);

        Assert.Same(scope.ServiceProvider.GetRequiredService<global::ReactiveUI.IScreen>(), store.HostScreen);
        Assert.Equal(("/albums/7", "/search"), (album, Navigator(scope).Requested));
    }

    [Fact]
    [Trait("AC", "REACTIVE-03")]
    public async Task UnmappedViewModelsFailTheCommandAsync()
    {
        await using var site = new ReactiveSite();
        var (scope, models) = await site.OpenAsync(ReactiveSite.Page<StoreViewModel>("/store"));
        await using var _ = scope;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitedCommands.RunAsync(((StoreViewModel)models[^1]!).Lose, null).AsTask());

        Assert.Contains("LostViewModel", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Map<LostViewModel>", failure.Message, StringComparison.Ordinal);
        Assert.Null(Navigator(scope).Requested);
    }

    [Fact]
    [Trait("AC", "REACTIVE-05")]
    public async Task ScreensHoldThePageViewModelAsync()
    {
        await using var site = new ReactiveSite();
        var (scope, models) = await site.OpenAsync(ReactiveSite.Page<StoreViewModel, ShellViewModel>("/store"));
        await using var _ = scope;
        var shell = (ShellViewModel)models[0]!;
        var store = (StoreViewModel)models[1]!;

        var stack = shell.Router.NavigationStack.ToList();
        var loaded = Navigator(scope).Requested;
        await AwaitedCommands.RunAsync(shell.OpenAlbum, 5);

        Assert.Equal([store], stack);
        Assert.Equal([store], store.HostScreen.Router.NavigationStack.Take(1));
        Assert.Null(loaded);
        Assert.Equal("/albums/5", Navigator(scope).Requested);
    }

    private static PageNavigator Navigator(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<PageNavigator>();
}
