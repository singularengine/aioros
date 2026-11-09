using Aioros.Hosting;
using AdvancedToDoList;
using AdvancedToDoList.Desktop.Services;
using AdvancedToDoList.Helper;
using AdvancedToDoList.Properties;
using AdvancedToDoList.Services;
using CommunityToolkit.Mvvm.Messaging;

var builder = AiorosApplication.CreateBuilder(args);
builder.AddNavigation<IAppNavigator>();
builder.Services.AddScoped<IMessenger>(_ => new WeakReferenceMessenger());
builder.Services.AddSingleton<ISettingsStorageService, DefaultSettingsStorageService>();
builder.Services.AddSingleton<Aioros.Pages.IPrecompileSource, PrecompileUrls>();
builder.AddServerFunction<IToDoItemService, ToDoItemService>("todo");
builder.AddServerFunction<ICategoryService, CategoryService>("categories");
builder.AddServerFunction<IToDoListDataService, ToDoListDataService>("data");
builder.Server(services =>
{
    services.AddScoped<DatabaseHelper>();
    if (builder.Role == AiorosRole.InProcess)
    {
        services.AddSingleton<IDatabaseService, DesktopDbService>();
    }
    else
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<DemoDatabases>();
        services.AddSingleton<IDatabaseService, DemoDatabaseService>();
    }
});

var app = builder.Build();
if (builder.Role == AiorosRole.Server)
{
    app.Use(DemoDatabaseService.IdentifyVisitorAsync);
    app.MapGet("/export.json", async (IToDoListDataService data) =>
        Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(await data.ExportAsync(), JsonContextHelper.Default.DatabaseDto), "application/json", "ToDoList.json"));
}
else
{
    App.Services = app.Services;
    await Settings.Default.LoadSettingsAsync();
}

app.MapAioros();
return app.RunAioros();
