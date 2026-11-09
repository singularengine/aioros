using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.ReactiveUI.Tests.Models;
using Aioros.Runtime.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.ReactiveUI.Tests.Navigation;

internal sealed class ReactiveSite : IAsyncDisposable
{
    private readonly ServiceProvider _root;

    public ReactiveSite(Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection().AddPageNavigator();
        collection.AddSingleton<Activations>();
        collection.AddTransient<StoreViewModel>();
        collection.AddTransient<ShellViewModel>();
        collection.AddTransient<ActivatedViewModel>();
        collection.AddReactiveRoutes(routes => routes
            .Map<SearchViewModel>("/search")
            .Map<AlbumViewModel>(album => $"/albums/{album.Id}")
            .Map<StoreViewModel>("/store"));
        services?.Invoke(collection);
        _root = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public Activations Activations => _root.GetRequiredService<Activations>();

    public static PageDefinition Page<TModel>(string route)
        where TModel : class =>
        new(route, "Pages" + route + "/Page.axaml", Model<TModel>, []);

    public static PageDefinition Page<TModel, TLayout>(string route)
        where TModel : class
        where TLayout : class =>
        new(route, "Pages" + route + "/Page.axaml", Model<TModel>, [new LayoutDefinition("Pages/Layout.axaml", Model<TLayout>)]);

    public async Task<(AsyncServiceScope Scope, ImmutableArray<object?> Models)> OpenAsync(PageDefinition page)
    {
        var scope = _root.CreateAsyncScope();
        var loaded = await PageLoader.LoadAsync(scope.ServiceProvider, page, PageRequest.For(page.Route), TestContext.Current.CancellationToken);
        return (scope, loaded.Models);
    }

    public ValueTask DisposeAsync() => _root.DisposeAsync();

    private static object? Model<TModel>(IServiceProvider services)
        where TModel : class => services.GetRequiredService<TModel>();
}
