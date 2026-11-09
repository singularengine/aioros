using System.Reflection.Metadata;
using System.Threading.Channels;
using Aioros.Development;
using Aioros.Events;

[assembly: MetadataUpdateHandler(typeof(CodeUpdates))]

namespace Aioros.Development;

/// <summary>The C# edits the runtime applies while the app runs in development (<c>dotnet watch</c>), published to the app's event bus.</summary>
public static class CodeUpdates
{
    private static readonly Channel<CodeReloaded> Applied = Channel.CreateUnbounded<CodeReloaded>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Called by the runtime's hot reload agent once it has applied an edit, with the types it updated.</summary>
    public static void UpdateApplication(Type[]? types) =>
        Applied.Writer.TryWrite(new CodeReloaded([.. (types ?? []).Select(type => type.FullName ?? type.Name)]));

    /// <summary>Publishes each applied edit as a <see cref="CodeReloaded"/> on the bus until cancelled.</summary>
    public static async Task ForwardAsync(IEventBus bus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);
        await foreach (var update in Applied.Reader.ReadAllAsync(cancellationToken))
        {
            await bus.PublishAsync(update, cancellationToken);
        }
    }
}
