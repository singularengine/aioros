using ModelThreads.Models;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton<IIcons, Icons>();
var app = builder.Build();
app.MapAioros();
return app.RunAioros();
