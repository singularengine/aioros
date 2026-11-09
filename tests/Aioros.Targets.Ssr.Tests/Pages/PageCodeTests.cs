using System.Diagnostics.CodeAnalysis;
using System.Net;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class PageCodeTests
{
    private const string Now = "The model's async void threw while it was created.";
    private const string AfterAnswer = "The model's async void threw after the answer.";

    [Fact(Timeout = 60_000)]
    [Trait("AC", "SSR-114")]
    public async Task AsyncVoidModelCodeOnAPlannedPageFailsOnlyItsRequestAsync()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new RecordedLogs();
        var engine = new FakeEngine();
        var options = new SsrOptions();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddAiorosPages(options).AddSingleton<IPageEngine>(engine);
        await using var app = builder.Build();
        app.UseExceptionHandler(static errors => errors.Run(static http => http.RequestServices.GetRequiredService<PageHandler>().ErrorAsync(http)));
        app.MapAiorosPages([
            new PageDefinition("/now", "Pages/now/Page.axaml", static _ => new Starting(static () => throw new InvalidOperationException(Now)), []),
            new PageDefinition("/later", "Pages/later/Page.axaml", _ => new Starting(async () =>
            {
                await release.Task;
                throw new InvalidOperationException(AfterAnswer);
            }), []),
            new PageDefinition("/fine", "Pages/fine/Page.axaml", static _ => new Model("Fine"), []),
        ]);
        var cache = app.Services.GetRequiredService<PlanCache>();
        foreach (var route in (string[])["/now", "/later", "/fine"])
        {
            cache.Store(route, "light", cache.Document(FakeEngine.Page(reusable: true), options, live: false));
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var failed = await client.GetAsync(new Uri("/now", UriKind.Relative), TestContext.Current.CancellationToken);
        var duringLoad = await logs.WaitForAsync(static entry => entry.Exception?.InnerException?.Message == Now, TestContext.Current.CancellationToken);
        using var fine = await client.GetAsync(new Uri("/fine", UriKind.Relative), TestContext.Current.CancellationToken);
        using var answered = await client.GetAsync(new Uri("/later", UriKind.Relative), TestContext.Current.CancellationToken);
        release.TrySetResult();
        var afterAnswer = await logs.WaitForAsync(static entry => entry.Exception?.Message == AfterAnswer, TestContext.Current.CancellationToken);
        using var still = await client.GetAsync(new Uri("/fine", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Contains("/now", duringLoad.Exception!.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
        Assert.Contains("<p>Fine</p>", await fine.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Contains("/later", afterAnswer.Message, StringComparison.Ordinal);
        Assert.Contains("after its answer", afterAnswer.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, still.StatusCode);
        Assert.Empty(engine.Renders);
    }
}

internal sealed class Starting
{
    public Starting(Func<Task> work) => Start(work);

    public string Title { get; } = "starting";

    [SuppressMessage("Usage", "VSTHRD100", Justification = "The model stands for a view model's fire-and-forget code whose exception the server must contain.")]
    [SuppressMessage("Usage", "S3168", Justification = "The model stands for a view model's fire-and-forget code whose exception the server must contain.")]
    private static async void Start(Func<Task> work) => await work();
}
