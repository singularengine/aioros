using Showcase;
using Showcase.Board;
using Showcase.Games;
using Showcase.Images;
using Showcase.Models;
using Showcase.Uploads;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddNavigation<IAppNavigator>();
builder.AddServerFunction<IFinanceFunctions, FinanceFunctions>();
builder.Services.AddSingleton<CustomerStore>();
builder.AddServerFunction<ICustomerFunctions, CustomerFunctions>("customers");
builder.Services.AddSingleton<Aioros.Pages.IPrecompileSource, Samples>();
builder.Server(services => services.AddSingleton<Market>().AddHostedService<MarketFeed>());
builder.Services.AddSingleton<BoardStore>();
builder.AddServerFunction<IBoardFunctions, BoardFunctions>();
builder.AddGames();
builder.Services.AddHealthChecks();
if (builder.Role == Aioros.Hosting.AiorosRole.Server)
{
    builder.Services.AddSingleton<IFileOrigin>(new UploadedFileOrigin(builder.Configuration.GetValue("Aioros:UploadLimit", 10L * 1024 * 1024)));
}
else
{
    builder.Services.AddSingleton<IFileOrigin, LocalFileOrigin>();
}

var app = builder.Build();
if (Environment.GetEnvironmentVariable("SHOWCASE_ISOLATE") == "1")
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
        await next();
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapHealthChecks("/health");
}

app.ShowImagesOnDesktop();
app.MapGameArt();
app.MapAioros();
return app.RunAioros();
