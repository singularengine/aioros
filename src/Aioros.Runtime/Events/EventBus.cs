using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aioros.Events;

namespace Aioros.Runtime.Events;

public sealed class EventBus : IEventBus
{
    private ImmutableArray<ChannelWriter<object>> _subscribers = [];

    public async ValueTask PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken)
        where TEvent : notnull
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber.WriteAsync(message, cancellationToken);
        }
    }

    public IAsyncEnumerable<TEvent> SubscribeAsync<TEvent>(CancellationToken cancellationToken)
        where TEvent : notnull
    {
        var channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
        ImmutableInterlocked.Update(ref _subscribers, subscribers => subscribers.Add(channel.Writer));
        return ReadAsync<TEvent>(channel, cancellationToken);
    }

    private async IAsyncEnumerable<TEvent> ReadAsync<TEvent>(
        Channel<object> channel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TEvent : notnull
    {
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (item is TEvent message)
                {
                    yield return message;
                }
            }
        }
        finally
        {
            ImmutableInterlocked.Update(ref _subscribers, subscribers => subscribers.Remove(channel.Writer));
            channel.Writer.TryComplete();
        }
    }
}
