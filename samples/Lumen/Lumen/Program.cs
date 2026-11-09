using Lumen.Data;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton<Store>();
builder.AddServerFunction<IIssueFunctions, IssueFunctions>();
builder.AddServerFunction<IProfileFunctions, ProfileFunctions>();
builder.AddServerFunction<IRecentFunctions, RecentFunctions>();
builder.AddServerFunction<IDashboardFunctions, DashboardFunctions>();
builder.Services.AddSingleton<Aioros.Pages.ISitemapSource, IssueSitemap>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Server(services => services.AddSingleton<Sales>().AddHostedService<DashboardFeed>());

var app = builder.Build();
app.MapAioros();
if (app.Configuration.GetValue<bool>("Lumen:Diagnostics"))
{
    app.MapGet("/_stats", (RequestDelegate)(http => Results.Text(
        $"{{\"allocatedBytes\":{GC.GetTotalAllocatedBytes(true)},\"workingSet\":{Environment.WorkingSet}}}", "application/json").ExecuteAsync(http)));
    app.MapGet("/_assemblies", (RequestDelegate)(http => Results.Text(string.Join('\n', AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name).Order(StringComparer.Ordinal))).ExecuteAsync(http)));
}
return app.RunAioros();
