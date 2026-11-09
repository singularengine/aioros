using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Themes;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// The precompilation's check that every page's models load as production loads them, before Avalonia starts: in the
/// warm-up's order, each on a thread of its own as a request's, a page marked <see cref="PageDefinition.ModelsUseAvalonia"/>
/// starting the engine first. A model that throws there, or creates an Avalonia object on its request's thread when the
/// generator did not see it do so (through a service), fails the precompilation instead of failing in production, where
/// Avalonia's platform is not up and its UI dispatcher would go to that thread.
/// </summary>
public static class ProductionModels
{
    public const string Code = "AIOROS032";

    private const string Advice = "Create Avalonia objects in the model's own code (a field or property initializer, a constructor), where the generator sees them and starts Avalonia first, or keep them out of the models (a path's data as a string, an image as its URI).";

    public static async Task<ModelCheck> CheckAsync(IEnumerable<PageDefinition> pages, IServiceProvider services, IPageEngine engine, Func<Thread, bool> holdsAvalonia, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(holdsAvalonia);
        var list = pages.ToList();
        var samples = await WarmupRequests.SamplesAsync(list, services, cancellationToken);
        var errors = ImmutableArray.CreateBuilder<string>();
        var loaded = 0;
        foreach (var page in list)
        {
            if ((page.Route.Contains('{', StringComparison.Ordinal) ? samples.GetValueOrDefault(page.Route) : PageRequest.For(page.Route)) is not { } request)
            {
                continue;
            }

            loaded++;
            var (error, taken) = await ErrorAsync(page, request, services, engine, holdsAvalonia, cancellationToken);
            if (error is not null)
            {
                errors.Add(error);
            }

            if (taken)
            {
                errors.Add($"error {Code}: {page.Route}: the pages after it were not checked, since Avalonia's UI dispatcher now belongs to that page's request thread.");
                break;
            }
        }

        return new ModelCheck(loaded, errors.ToImmutable());
    }

    /// <summary>What loading the page's models as production does found wrong, and whether its request's thread took Avalonia's UI dispatcher before the engine started.</summary>
    private static async Task<(string? Error, bool Taken)> ErrorAsync(PageDefinition page, PageRequest request, IServiceProvider services, IPageEngine engine, Func<Thread, bool> holdsAvalonia, CancellationToken cancellationToken)
    {
        Thread? thread = null;
        var before = engine is IEngineStart { Started: false };
        try
        {
            if (page.ModelsUseAvalonia && engine is IEngineStart start)
            {
                await start.StartAsync(cancellationToken);
            }

            await Task.Factory.StartNew(
                () =>
                {
                    thread = Thread.CurrentThread;
                    return PageModels.LoadAsync(services, page, request, cancellationToken).AsTask();
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            var cause = error.GetBaseException();
            return ($"error {Code}: {page.Route}: its models failed to load before Avalonia started, as production loads them: {cause.GetType().Name}: {cause.Message} {Advice}", Took(thread));
        }

        return !page.ModelsUseAvalonia && thread is not null && holdsAvalonia(thread)
            ? ($"error {Code}: {page.Route}: its models created an Avalonia object on a request's thread before Avalonia started, so production could not start Avalonia afterwards. {Advice}", Took(thread))
            : (null, false);

        bool Took(Thread? loading) => before && engine is IEngineStart { Started: false } && loading is not null && holdsAvalonia(loading);
    }
}

/// <summary>How many pages' models were loaded as production loads them, and the errors found.</summary>
public sealed record ModelCheck(int Loaded, ImmutableArray<string> Errors)
{
    public bool Failed => !Errors.IsEmpty;

    public string Text =>
        $"Production models: {Loaded - Errors.Length} of {Loaded} pages load their models as production does\n" + string.Concat(Errors.Select(error => "  " + error + "\n"));
}
