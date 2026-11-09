using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Hosting;

public sealed class EngineCapacityTests
{
    [Fact]
    [Trait("AC", "SSR-270")]
    public async Task AFullEngineAnswersServiceUnavailableAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEngineCapacity();
        await using var app = builder.Build();
        app.MapGet("/busy", (HttpContext _) => Task.FromException(new EngineBusyException()));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()) };

        using var answer = await client.GetAsync(new Uri("/busy", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(503, (int)answer.StatusCode);
        Assert.Equal("1", Assert.Single(answer.Headers.GetValues("Retry-After")));
    }
}
