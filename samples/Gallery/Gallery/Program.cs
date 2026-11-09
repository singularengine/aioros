using Gallery.Parity;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton<Aioros.Pages.IPrecompileSource, ParitySamples>();
var app = builder.Build();
app.ShowParityCaptures();
app.MapAioros();
return app.RunAioros();
