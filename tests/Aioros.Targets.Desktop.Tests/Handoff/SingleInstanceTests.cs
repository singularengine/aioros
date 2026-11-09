using System.Net.Sockets;
using Aioros.Targets.Desktop.Handoff;

namespace Aioros.Targets.Desktop.Tests.Handoff;

public sealed class SingleInstanceTests
{
    [Fact(Timeout = 30_000)]
    [Trait("AC", "INSTALL-03")]
    public async Task ASecondInstanceForwardsItsArgumentsToTheRunningOneAsync()
    {
        var bound = TestContext.Current.CancellationToken;
        var name = "aioros-test-" + Guid.NewGuid().ToString("N");
        var running = SingleInstance.Claim(name, []);
        Assert.NotNull(running);

        var second = SingleInstance.Claim(name, ["shop://open?path=/orders/42"]);
        var forwarded = await running.Forwarded.ReadAsync(bound);
        var third = SingleInstance.Claim(name, []);
        var empty = await running.Forwarded.ReadAsync(bound);
        await running.DisposeAsync();
        await using var next = SingleInstance.Claim(name, []);

        Assert.Null(second);
        Assert.Equal(["shop://open?path=/orders/42"], forwarded);
        Assert.Null(third);
        Assert.Empty(empty);
        Assert.NotNull(next);
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "INSTALL-04")]
    public async Task AStaleSocketIsReplacedAsync()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows removes a pipe with its last handle; only Unix sockets outlive their process.");
        var name = "aioros-test-" + Guid.NewGuid().ToString("N");
        var bound = SingleInstance.SocketPath(name) + ".bound";
        using (var stale = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            stale.Bind(new UnixDomainSocketEndPoint(bound));
            File.Move(bound, SingleInstance.SocketPath(name));
        }

        var left = File.Exists(SingleInstance.SocketPath(name));
        await using var running = SingleInstance.Claim(name, []);
        var second = SingleInstance.Claim(name, ["shop://open?path=/"]);
        var forwarded = await running!.Forwarded.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(left);
        Assert.NotNull(running);
        Assert.Null(second);
        Assert.Equal(["shop://open?path=/"], forwarded);
    }
}
