using Aioros.Pages;
using Avalonia.Controls;
using Stylet.Avalonia.Primitive;
using Stylet.Samples.HelloDialog;
using Stylet.Samples.NavigationController;
using StyletSamples.Composition;
using NavigationPages = Stylet.Samples.NavigationController.Pages;
using Tabs = Stylet.Samples.TabNavigation;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddStylet(new Dictionary<Type, Func<Control>>
{
    [typeof(MessageBoxViewModel)] = () => new MessageBoxView(),
    [typeof(Dialog1ViewModel)] = () => new Dialog1View(),
});
builder.Services
    .AddScoped<IModelLifecycle, ScreenLifecycle>()
    .AddTransient<Dialog1ViewModel>()
    .AddSingleton<IDialogFactory, StyletHosting.DialogFactory>()
    .AddScoped<INavigationController, RouteNavigationController>()
    .AddTransient<NavigationPages.HeaderViewModel>()
    .AddTransient<Tabs.Page1ViewModel>()
    .AddTransient<Tabs.Page2ViewModel>();

var app = builder.Build();
app.Services.UseStylet();
app.MapAioros();
return app.RunAioros();
