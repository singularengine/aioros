using Aioros.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class HostRoleTests
{
    private const string Remote = "--Aioros:Server=http://server.test";

    [Fact]
    [Trait("AC", "DESKTOP-41")]
    public void TheRoleFollowsHowTheAppStarts()
    {
        var none = new ConfigurationBuilder().Build();
        var remote = new ConfigurationBuilder().AddInMemoryCollection([new(AiorosFunctionRegistration.ServerSetting, "http://server.test")]).Build();
        var inProcess = AiorosHost.CreateBuilder([AiorosMode.DesktopFlag], App());
        var client = AiorosHost.CreateBuilder([Remote, AiorosMode.DesktopFlag], App());

        Assert.Equal(AiorosRole.Server, AiorosMode.From([], none).Role);
        Assert.Equal(AiorosRole.Server, AiorosMode.From([], remote).Role);
        Assert.Equal(AiorosRole.Server, AiorosMode.From([AiorosMode.PrecompileOption + "/tmp/plans.json"], none).Role);
        Assert.Equal(AiorosRole.InProcess, AiorosMode.From([AiorosMode.DesktopFlag], none).Role);
        Assert.Equal(AiorosRole.Client, AiorosMode.From([AiorosMode.DesktopFlag], remote).Role);
        Assert.Equal(AiorosRole.InProcess, inProcess.Role);
        Assert.Equal(AiorosRole.Client, client.Role);
        Assert.Equal(new Uri("http://server.test"), Mode(client).Server);
        Assert.Null(Mode(inProcess).Server);
    }

    [Fact]
    [Trait("AC", "DESKTOP-42")]
    public async Task ServerRegistrationsAreSkippedInTheClientAsync()
    {
        var web = WebApplication.CreateBuilder();
        web.WebHost.UseTestServer();
        web.Services.AddSingleton(AiorosMode.From([], web.Configuration));

        var ran = new List<(AiorosRole, bool, string)>();
        foreach (var builder in (WebApplicationBuilder[])[web, AiorosHost.CreateBuilder([AiorosMode.DesktopFlag], App()), AiorosHost.CreateBuilder([Remote, AiorosMode.DesktopFlag], App())])
        {
            var signals = new Signals();
            builder.Services.AddSingleton(signals);
            builder.Server(services => services.AddSingleton<Store>().AddHostedService<Seeder>());
            await using var app = builder.Build();
            await app.StartAsync(TestContext.Current.CancellationToken);
            ran.Add((builder.Role, app.Services.GetService<Store>() is not null, string.Join(",", signals.Started)));
            await app.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal((AiorosRole.Server, true, "seeder"), ran[0]);
        Assert.Equal((AiorosRole.InProcess, true, "seeder"), ran[1]);
        Assert.Equal((AiorosRole.Client, false, string.Empty), ran[2]);
    }

    [Fact]
    [Trait("AC", "DESKTOP-43")]
    public async Task HostedServicesOutsideTheServerRunInTheClientAsync()
    {
        var builder = AiorosHost.CreateBuilder([Remote, AiorosMode.DesktopFlag], App());
        var signals = new Signals();
        builder.Services.AddSingleton(signals);
        builder.Services.AddHostedService<Feed>();
        builder.Server(services => services.AddHostedService<Seeder>());
        await using var app = builder.Build();

        await app.StartAsync(TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AiorosRole.Client, builder.Role);
        Assert.Equal(["feed"], signals.Started);
    }

    private static AiorosMode Mode(WebApplicationBuilder builder) =>
        (AiorosMode)builder.Services.Last(service => service.ServiceType == typeof(AiorosMode)).ImplementationInstance!;

    private static AiorosApp App() => new([], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory);

    internal sealed class Signals
    {
        public List<string> Started { get; } = [];
    }

    internal sealed class Store
    {
        public string Name { get; } = "games";
    }

    internal sealed class Seeder(Signals signals) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            signals.Started.Add("seeder");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal sealed class Feed(Signals signals) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            signals.Started.Add("feed");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
