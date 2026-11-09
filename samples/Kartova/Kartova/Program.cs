using Kartova.App.Cli;
using Kartova.App.Services;
using Kartova.App.ViewModels;

if (CommandLine.WantsHeadless(args)) return CommandLine.Run(args);

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => SettingsService.Load());
builder.Services.AddScoped<MainViewModel>();

var app = builder.Build();
app.MapAioros();
return app.RunAioros();
