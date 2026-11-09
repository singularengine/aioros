using ReactiveUI.Samples.Suspension.ViewModels;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddReactiveRoutes(routes => routes
    .Map<SearchViewModel>("/search")
    .Map<LoginViewModel>("/login"));

var app = builder.Build();
app.MapAioros();
return app.RunAioros();
