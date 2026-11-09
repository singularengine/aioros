using System.Reflection;
using Aioros.Hosting;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Aioros.Targets.Desktop.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class InstalledAppHostTests(DesktopFixture fixture)
{
    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-52")]
    public async Task TheDesktopLifetimeShutsDownWhenTheHostStopsAsync()
    {
        using var stopping = new CancellationTokenSource();
        var lifetime = new Lifetime();

        var shutDown = await fixture.OnUiAsync(async () =>
        {
            using var registration = DesktopHost.ShutDownWhenStopping(lifetime.Shutdown, stopping.Token);
            var before = lifetime.ShutDown.Task.IsCompleted;
            await stopping.CancelAsync();
            await lifetime.ShutDown.Task.WaitAsync(TestContext.Current.CancellationToken);
            return (before, Environment.CurrentManagedThreadId == lifetime.Thread);
        });

        Assert.Equal((false, true), shutDown);
    }

    [Fact]
    [Trait("AC", "DESKTOP-53")]
    public void TheBakedOriginIsTheInstalledAppsServerOutsideDevelopment()
    {
        var none = new ConfigurationBuilder().Build();
        var configured = new ConfigurationBuilder().AddInMemoryCollection([new(AiorosFunctionRegistration.ServerSetting, "http://dev.test:5317")]).Build();
        const string Baked = "https://shop.example";

        var installed = AiorosMode.From([AiorosMode.DesktopFlag], none, Baked);
        var overridden = AiorosMode.From([AiorosMode.DesktopFlag], configured, Baked);
        var development = AiorosMode.From([AiorosMode.DesktopFlag], none, Baked, development: true);
        var devServer = AiorosMode.From([AiorosMode.DesktopFlag], configured, Baked, development: true);
        var website = AiorosMode.From([], none, Baked);
        var built = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag, "--environment=Production"], App() with { Server = Baked });
        var developing = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag, "--environment=Development"], App() with { Server = Baked });

        Assert.Equal((AiorosRole.Client, new Uri(Baked)), (installed.Role, installed.Server));
        Assert.Equal(new Uri("http://dev.test:5317"), overridden.Server);
        Assert.Equal((AiorosRole.InProcess, null), (development.Role, development.Server));
        Assert.Equal(new Uri("http://dev.test:5317"), devServer.Server);
        Assert.Equal((AiorosRole.Server, null), (website.Role, website.Server));
        Assert.Equal(AiorosRole.Client, built.Role);
        Assert.Equal(AiorosRole.InProcess, developing.Role);
    }

    [Fact]
    [Trait("AC", "SIGNIN-12")]
    public void TheSignInIsComposedForTheRoleOfTheHost()
    {
        var web = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        web.Services.AddSingleton(AiorosMode.From([], web.Configuration));
        var inProcess = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag], App());
        var client = AiorosHost.CreateBuilder(["--Aioros:Server=http://server.test", AiorosMode.DesktopFlag], App());
        foreach (var builder in (WebApplicationBuilder[])[web, inProcess, client])
        {
            builder.AddInstalledAppSignIn(static core => core.SetDefaultApplicationEntity<Application>());
        }

        using var clientServices = client.Services.BuildServiceProvider();
        var functions = new FakeHandlerBuilder(clientServices);
        foreach (var configure in clientServices.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get("games").HttpMessageHandlerBuilderActions)
        {
            configure(functions);
        }

        var data = (RemoteLoaderResults)clientServices.GetRequiredService<ILoaderResults>();
        var handler = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(data.Http);

        Assert.True(Registered<IOpenIddictApplicationManager>(web));
        Assert.True(Registered<InstalledAppSignInOptions>(web));
        Assert.Contains(web.Services, service => service.ImplementationType?.Name == "ClientRegistration");
        Assert.False(Registered<SignInClient>(web));
        Assert.False(Registered<IOpenIddictApplicationManager>(inProcess));
        Assert.False(Registered<SignInClient>(inProcess));
        Assert.False(Registered<InstalledAppSignInOptions>(inProcess));
        Assert.True(Registered<Aioros.Targets.Ssr.SignIn.InstalledAppSessions>(web));
        Assert.True(Registered<Aioros.Targets.Ssr.SignIn.InstalledAppSessions>(inProcess));
        Assert.False(Registered<Aioros.Targets.Ssr.SignIn.InstalledAppSessions>(client));
        Assert.False(Registered<IOpenIddictApplicationManager>(client));
        Assert.Equal("/login", clientServices.GetRequiredService<SignInClient>().Options.SignInPath);
        Assert.Contains(functions.AdditionalHandlers, static added => added is SignedInHandler);
        Assert.IsType<SignedInHandler>(handler);
        Assert.Equal(new Uri("http://server.test"), data.Http.BaseAddress);
    }

    private static bool Registered<T>(WebApplicationBuilder builder) => builder.Services.Any(static service => service.ServiceType == typeof(T));

    private static AiorosApp App() => new([], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory);

    private sealed class Application
    {
        public string Name { get; } = "app";
    }

    private sealed class FakeHandlerBuilder(IServiceProvider services) : HttpMessageHandlerBuilder
    {
        public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

        public override string? Name { get; set; }

        public override HttpMessageHandler PrimaryHandler { get; set; } = new HttpClientHandler();

        public override IServiceProvider Services => services;

        public override HttpMessageHandler Build() => PrimaryHandler;
    }

    private sealed class Lifetime
    {
        public TaskCompletionSource ShutDown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Thread { get; private set; }

        public void Shutdown()
        {
            Thread = Environment.CurrentManagedThreadId;
            ShutDown.TrySetResult();
        }
    }
}
