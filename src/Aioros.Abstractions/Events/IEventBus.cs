namespace Aioros.Events;

public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken)
        where TEvent : notnull;

    IAsyncEnumerable<TEvent> SubscribeAsync<TEvent>(CancellationToken cancellationToken)
        where TEvent : notnull;
}
