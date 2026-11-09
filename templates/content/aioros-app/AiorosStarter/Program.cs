using AiorosStarter.Models;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton<ClickCounter>();

var app = builder.Build();
app.MapAioros();
return app.RunAioros();
