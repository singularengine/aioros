// Lumen built the idiomatic Blazor way, in one of two render modes (Lumen__Render):
//  - Interactive (default): a Blazor Web App with global Interactive Server rendering (prerendered, then a SignalR
//    circuit per browser tab);
//  - Static: static server-side rendering with enhanced navigation and enhanced forms, the fair counterpart of an SSR
//    framework; only the live dashboard is an Interactive Server island.
// https://learn.microsoft.com/aspnet/core/blazor/components/render-modes
using LumenBlazor;
using LumenBlazor.Components;
using LumenBlazor.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(new LumenRender(builder.Configuration["Lumen:Render"] == "Static"));
builder.Services.AddLumenCompression();
builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<DashboardData>();
builder.Services.AddHostedService<DashboardTicker>();

var app = builder.Build();
app.UseResponseCompression();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapLumenEndpoints();
app.Run();

/// <summary>The render mode the app runs in: static SSR when <c>Lumen:Render</c> is <c>Static</c>.</summary>
public sealed record LumenRender(bool Static);
