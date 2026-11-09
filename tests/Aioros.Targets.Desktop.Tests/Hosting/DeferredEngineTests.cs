using System.Collections.Immutable;
using Aioros.Hosting;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Documents;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Aioros.Targets.Ssr.Themes;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class DeferredEngineTests
{
    [Fact]
    [Trait("AC", "ENGINE-290")]
    public async Task PagesWhoseModelsUseAvaloniaStartTheEngineBeforeTheirModelsAsync()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var engine = new ThreadEngine();
        var starts = 0;
        var deferred = new DeferredPageEngine(() =>
        {
            starts++;
            return Task.FromResult<IPageEngine>(engine);
        });
        var plans = new PlanCache();
        var options = new SsrOptions();
        var handler = new PageHandler(deferred, plans, options);
        var created = new List<(string Route, bool Started, bool OnEngine)>();
        PageDefinition Page(string route, bool avalonia) => new(route, "Pages/Page.axaml", _ =>
        {
            created.Add((route, deferred.Started, engine.Running));
            return null;
        }, [])
        {
            ModelsUseAvalonia = avalonia,
        };
        var compiled = new CompiledPage(new RenderPlan([], [], []), string.Empty, "#fff", "#000", Reusable: true);
        foreach (var route in (string[])["/plain", "/marked"])
        {
            plans.Store(route, ThemeSwitch.Light, new CachedPage(compiled, DocumentParts.For(compiled, options, live: false)));
        }

        await using var services = new ServiceCollection().BuildServiceProvider();
        await handler.LoadAsync(services, Page("/plain", avalonia: false), PageRequest.For("/plain"), ThemeSwitch.Light, cancellation);
        var plainStarts = starts;
        await handler.LoadAsync(services, Page("/marked", avalonia: true), PageRequest.For("/marked"), ThemeSwitch.Light, cancellation);
        await handler.LoadAsync(services, Page("/drawn", avalonia: true), PageRequest.For("/drawn"), ThemeSwitch.Light, cancellation);

        Assert.Equal(0, plainStarts);
        Assert.Equal([("/plain", false, false), ("/marked", true, false), ("/drawn", true, true)], created);
        Assert.Equal(1, starts);
    }

    private sealed class ThreadEngine : IPageEngine, IModelThread
    {
        public bool Running { get; private set; }

        public ValueTask<CompiledPage> RenderAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async ValueTask<T> LoadAsync<T>(PageDefinition page, Func<ValueTask<T>> load, CancellationToken cancellationToken)
        {
            Running = true;
            try
            {
                return await load();
            }
            finally
            {
                Running = false;
            }
        }
    }
}
