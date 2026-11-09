using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Aioros.Cli.Commands;

namespace Aioros.Cli.Tests.Commands;

public sealed class DevProcessTests
{
    private const string FakeDotnet = """
        #!/bin/sh
        tail -f /dev/null > "$AIOROS_TEST_FIFO" &
        echo "tool $Aioros__DevelopmentTool"
        wait

        """;

    [Fact(Timeout = 60_000)]
    [Trait("AC", "CLI-43")]
    public async Task StoppingTheToolStopsEverythingItStartedAsync()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals and FIFOs of the test's fake dotnet are Linux's.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var port = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();
        var work = await FolderAsync(port, cancellationToken);
        try
        {
            var fifo = Path.Combine(work.FullName, "held");
            await RunAsync("mkfifo", fifo);
            using var held = Start("cat", fifo);
            using var tool = Tool(work.FullName, fifo);

            var started = await tool.StandardOutput.ReadLineAsync(cancellationToken);
            await RunAsync("kill", "-TERM", tool.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await tool.WaitForExitAsync(cancellationToken);
            await held.WaitForExitAsync(cancellationToken);

            Assert.Equal($"tool {tool.Id}", started);
            Assert.True(held.HasExited);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact(Timeout = 60_000)]
    [Trait("AC", "CLI-44")]
    public async Task ATakenPortIsReportedFirstWithItsProcessAsync()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The owner of a port is read from Linux's /proc.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        var port = ((IPEndPoint)taken.LocalEndpoint).Port;
        var work = await FolderAsync(port, cancellationToken);
        try
        {
            var fifo = Path.Combine(work.FullName, "held");
            await RunAsync("mkfifo", fifo);
            using var held = Start("cat", fifo);
            using var tool = Tool(work.FullName, fifo);

            var first = await tool.StandardOutput.ReadLineAsync(cancellationToken);
            var second = await tool.StandardOutput.ReadLineAsync(cancellationToken);
            tool.Kill(entireProcessTree: true);
            await tool.WaitForExitAsync(cancellationToken);

            Assert.StartsWith($"Port {port} is in use by {Process.GetCurrentProcess().ProcessName} (pid {Environment.ProcessId}), using ", first, StringComparison.Ordinal);
            Assert.Equal($"tool {tool.Id}", second);
            Assert.Equal(Process.GetCurrentProcess().ProcessName + $" (pid {Environment.ProcessId})", PortOwners.Of(port));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static async Task<DirectoryInfo> FolderAsync(int port, CancellationToken cancellationToken)
    {
        var work = Directory.CreateTempSubdirectory("aioros-dev-");
        var project = Directory.CreateDirectory(Path.Combine(work.FullName, "Shop"));
        project.CreateSubdirectory("Pages");
        var properties = project.CreateSubdirectory("Properties");
        await File.WriteAllTextAsync(Path.Combine(project.FullName, "Shop.csproj"), "<Project />", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(properties.FullName, "launchSettings.json"), $$"""{ "profiles": { "Shop": { "commandName": "Project", "applicationUrl": "http://localhost:{{port}}" } } }""", cancellationToken);
        var host = Path.Combine(work.FullName, "fake-dotnet");
        await File.WriteAllTextAsync(host, FakeDotnet, cancellationToken);
        await RunAsync("chmod", "+x", host);
        return work;
    }

    private static Process Tool(string directory, string fifo)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(Planner).Assembly.Location);
        start.ArgumentList.Add("dev");
        start.Environment["DOTNET_HOST_PATH"] = Path.Combine(directory, "fake-dotnet");
        start.Environment["AIOROS_TEST_FIFO"] = fifo;
        return Process.Start(start)!;
    }

    private static Process Start(string file, params string[] arguments)
    {
        var start = new ProcessStartInfo(file) { RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start)!;
    }

    private static async Task RunAsync(string file, params string[] arguments)
    {
        using var process = Start(file, arguments);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, process.ExitCode);
    }
}
