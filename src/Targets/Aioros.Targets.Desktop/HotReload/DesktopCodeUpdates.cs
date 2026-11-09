using System.Reflection.Metadata;
using System.Threading.Channels;
using Aioros.Targets.Desktop.HotReload;

[assembly: MetadataUpdateHandler(typeof(DesktopCodeUpdates))]

namespace Aioros.Targets.Desktop.HotReload;

/// <summary>The C# edits the runtime applies to the desktop app in development (<c>dotnet watch</c>), for <see cref="DesktopHotReload"/> to rebuild the window's views.</summary>
public static class DesktopCodeUpdates
{
    private static readonly Channel<bool> Applied = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true });

    public static ChannelReader<bool> Updates => Applied.Reader;

    /// <summary>Called by the runtime's hot reload agent once it has applied an edit.</summary>
    public static void UpdateApplication(Type[]? types) => Applied.Writer.TryWrite(true);
}
