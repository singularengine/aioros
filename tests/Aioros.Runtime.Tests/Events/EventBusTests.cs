using Aioros.Runtime.Events;

namespace Aioros.Runtime.Tests.Events;

public sealed class EventBusTests
{
    private sealed record Saved(string Path);

    private sealed record Other(int Value);

    [Fact]
    [Trait("AC", "EVENTS-01")]
    public async Task ASubscriberReceivesTheEventsOfItsTypeInOrderAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var bus = new EventBus();
        var events = bus.SubscribeAsync<Saved>(token);

        await bus.PublishAsync(new Saved("a.axaml"), token);
        await bus.PublishAsync(new Other(1), token);
        await bus.PublishAsync(new Saved("b.axaml"), token);

        var received = await events.Take(2).ToListAsync(token);

        Assert.Equal([new Saved("a.axaml"), new Saved("b.axaml")], received);
    }

    [Fact]
    [Trait("AC", "EVENTS-02")]
    public async Task EverySubscriberReceivesEveryEventAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var bus = new EventBus();
        var first = bus.SubscribeAsync<Saved>(token);
        var second = bus.SubscribeAsync<Saved>(token);

        await bus.PublishAsync(new Saved("page.axaml"), token);

        Assert.Equal(new Saved("page.axaml"), await first.FirstAsync(token));
        Assert.Equal(new Saved("page.axaml"), await second.FirstAsync(token));
    }
}
