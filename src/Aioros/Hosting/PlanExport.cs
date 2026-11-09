using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Aioros.Targets.Ssr.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Hosting;

internal sealed class PlanExport(string target, AiorosApp app, IServiceProvider services, PageHandler handler, IHostApplicationLifetime lifetime) : BackgroundService
{
    public static ImmutableArray<PageDefinition> Pages(AiorosApp app) => Warmup.Planned(app.Pages, app.NotFound, app.Error, app.NativeOnly);

    public static string PlansPath { get; } = Path.Combine(AppContext.BaseDirectory, PlansFile.Name);

    public static async Task<ImmutableArray<PlannedPage>> ReadAsync(CancellationToken cancellationToken)
    {
        await using var file = new FileStream(PlansPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await PlansFile.ReadAsync(file, new PlanSlotResolver(EngineFallback.Instance), cancellationToken);
    }

    /// <summary>
    /// Renders every saved page and theme for the models it was compiled with, from the plans read with generated accessors
    /// and with runtime delegates only, and looks up the setters its two-way fields would write through.
    /// </summary>
    private static ImmutableArray<PlanCheck> Check(string json, IReadOnlyList<WarmupOutcome> outcomes)
    {
        var models = outcomes.Where(outcome => !outcome.Models.IsEmpty).ToDictionary(outcome => outcome.Route, outcome => outcome.Models, StringComparer.Ordinal);
        var fresh = outcomes.Where(outcome => !outcome.FreshModels.IsEmpty).ToDictionary(outcome => outcome.Route, outcome => outcome.FreshModels, StringComparer.Ordinal);
        var resolver = new PlanSlotResolver(EngineFallback.Instance);
        var typed = PlansFile.Read(json, resolver);
        var runtime = PlansFile.Read(json, new PlanSlotResolver(EngineFallback.Instance, generated: false));
        var checks = typed.Zip(runtime)
            .Where(pair => models.ContainsKey(pair.First.Route))
            .Select(pair => new PlanCheck(pair.First.Route, pair.First.Theme, PlanComparison.FirstDifference(pair.First.Page.Plan, pair.Second.Page.Plan, models[pair.First.Route], pair.First.Route))
            {
                Miss = fresh.TryGetValue(pair.First.Route, out var loaded) ? PlanComparison.Miss(pair.First.Page.Plan, loaded, pair.First.Route) : null,
            })
            .ToImmutableArray();
        foreach (var page in typed.Where(page => models.ContainsKey(page.Route)))
        {
            resolver.LookUpSetters(page.Page.Plan, models[page.Route]);
        }

        return checks;
    }

    /// <summary>The plans file of the pages warmed up, each with the fields whose posts the engine answers.</summary>
    private PlansFileWrite Planned(IReadOnlyList<WarmupOutcome> outcomes) =>
        PlansFile.Write(outcomes.SelectMany(outcome => outcome.Entries.Select(entry => new PlannedPage(outcome.Route, entry.Theme, entry.Entry.Page)
        {
            Culture = entry.Culture,
            EngineFields = [.. handler.Plans.EngineFields(outcome.Route)],
            ProvenFields = [.. handler.Plans.ProvenFields(outcome.Route)],
        })));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var models = await ProductionModels.CheckAsync(Pages(app), scope.ServiceProvider, services.GetRequiredService<IPageEngine>(), AvaloniaEngine.HoldsAvalonia, stoppingToken);
            if (models.Failed)
            {
                await Console.Out.WriteAsync(models.Text);
                Environment.ExitCode = 1;
                return;
            }

            var outcomes = await Warmup.RunAsync(Pages(app), scope.ServiceProvider, handler, posts: false, verify: true, stoppingToken);
            var write = Planned(outcomes);
            await File.WriteAllTextAsync(target, write.Json, stoppingToken);
            var report = PrecompileReport.Of(outcomes, write.Skipped).Checked(Check(write.Json, outcomes), MemberAccessors.Coverage()).WithModels(models);
            await Console.Out.WriteAsync(report.Text);
            if (report.Failed)
            {
                Environment.ExitCode = 1;
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Precompilation failed: " + error.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
