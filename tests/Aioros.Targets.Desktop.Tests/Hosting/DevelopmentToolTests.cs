using System.Diagnostics;
using Aioros.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class DevelopmentToolTests
{
    [Fact(Timeout = 60_000)]
    [Trait("AC", "CLI-45")]
    public async Task AnAppStopsWhenTheToolThatStartedItIsGoneAsync()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The stand-in tool is Linux's tail.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var tool = Process.Start(new ProcessStartInfo("tail") { ArgumentList = { "-f", "/dev/null" } })!;
        string[] setting = ["--" + AiorosHost.DevelopmentToolSetting + "=" + tool.Id];
        var development = AiorosHost.CreateBuilder(["--environment=Development", .. setting], App());
        var production = AiorosHost.CreateBuilder(["--environment=Production", .. setting], App());

        var running = new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance);
        var stopping = StoppingAsync(running);
        using var watch = new DevelopmentToolWatch(new SsrOptions { DevelopmentTool = tool.Id }, running);
        await watch.StartAsync(cancellationToken);
        var stoppedEarly = running.ApplicationStopping.IsCancellationRequested;
        tool.Kill();
        await tool.WaitForExitAsync(cancellationToken);
        await stopping.WaitAsync(cancellationToken);

        var late = new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance);
        var lateStopping = StoppingAsync(late);
        using var lateWatch = new DevelopmentToolWatch(new SsrOptions { DevelopmentTool = tool.Id }, late);
        await lateWatch.StartAsync(cancellationToken);
        await lateStopping.WaitAsync(cancellationToken);

        Assert.False(stoppedEarly);
        Assert.Contains(development.Services, static service => service.ImplementationType == typeof(DevelopmentToolWatch));
        Assert.DoesNotContain(production.Services, static service => service.ImplementationType == typeof(DevelopmentToolWatch));
    }

    private static Task StoppingAsync(ApplicationLifetime lifetime)
    {
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.ApplicationStopping.Register(stopping.SetResult);
        return stopping.Task;
    }

    private static AiorosApp App() => new([], static () => typeof(TestApp), typeof(DesktopFixture).Assembly, AppContext.BaseDirectory);
}
