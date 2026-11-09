using System.Diagnostics;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.Extensions.Hosting;

namespace Aioros.Targets.Ssr.Development;

/// <summary>Stops the app when the tool that started it is gone (a language server's <c>--hostPID</c>), so an app whose tool was killed outright does not stay on its port.</summary>
public sealed class DevelopmentToolWatch(SsrOptions options, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.DevelopmentTool is not { } tool)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(tool);
            await process.WaitForExitAsync(stoppingToken);
        }
        catch (ArgumentException)
        {
            lifetime.StopApplication();
            return;
        }

        lifetime.StopApplication();
    }
}
