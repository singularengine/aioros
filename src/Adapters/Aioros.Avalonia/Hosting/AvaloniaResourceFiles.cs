using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The files an assembly embeds as Avalonia resources (<c>AvaloniaResource</c> items, read on the desktop through
/// <c>avares://</c> URIs), read from the assembly's <c>!AvaloniaResources</c> stream with no Avalonia type, so the web
/// host serves a library's fonts without starting Avalonia. The stream is Avalonia's
/// <c>AvaloniaResourcesIndexReaderWriter</c> layout: the index's size, the index (version 2, the entry count, and each
/// entry's path, offset and size) and then the files, each at its offset from the end of the index.
/// </summary>
public static class AvaloniaResourceFiles
{
    private const string Stream = "!AvaloniaResources";

    private const int Version = 2;

    private static ImmutableDictionary<string, ImmutableDictionary<string, (int Offset, int Size)>> _indexes =
        ImmutableDictionary<string, ImmutableDictionary<string, (int Offset, int Size)>>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>The paths of the assembly's Avalonia resources, from the assembly's root (<c>Assets/Inter-Regular.ttf</c>).</summary>
    public static IEnumerable<string> Paths(Assembly assembly) => Index(assembly).Keys;

    /// <summary>The bytes of one resource, or null when the assembly has no resource at that path.</summary>
    public static async ValueTask<byte[]?> ReadAsync(Assembly assembly, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!Index(assembly).TryGetValue(Key(path), out var entry) || assembly.GetManifestResourceStream(Stream) is not { } stream)
        {
            return null;
        }

        await using (stream)
        {
            var size = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(size, cancellationToken);
            stream.Position = sizeof(int) + BitConverter.ToInt32(size) + entry.Offset;
            var bytes = new byte[entry.Size];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            return bytes;
        }
    }

    /// <summary>
    /// A font file (<c>.ttf</c> or <c>.otf</c>) of the Avalonia resources of the app's assembly with that name, as the web
    /// host serves the <c>@font-face</c> sources a page declares; null for any other file, or an assembly that is neither
    /// loaded nor among the app's own files.
    /// </summary>
    public static async ValueTask<byte[]?> ReadFontAsync(string assembly, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!(path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)) || Find(assembly) is not { } owner)
        {
            return null;
        }

        return await ReadAsync(owner, path, cancellationToken);
    }

    private static string Key(string path) => path.TrimStart(Path.AltDirectorySeparatorChar);

    private static Assembly? Find(string name)
    {
        if (AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, name, StringComparison.Ordinal)) is { } loaded)
        {
            return loaded;
        }

        return name.AsSpan().IndexOfAny("/\\:") < 0 && File.Exists(Path.Combine(AppContext.BaseDirectory, name + ".dll")) ? Assembly.Load(new AssemblyName(name)) : null;
    }

    private static ImmutableDictionary<string, (int Offset, int Size)> Index(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return ImmutableInterlocked.GetOrAdd(ref _indexes, assembly.FullName ?? assembly.GetName().Name ?? string.Empty, _ => ReadIndex(assembly));
    }

    private static ImmutableDictionary<string, (int Offset, int Size)> ReadIndex(Assembly assembly)
    {
        var entries = ImmutableDictionary.CreateBuilder<string, (int Offset, int Size)>(StringComparer.Ordinal);
        if (assembly.GetManifestResourceStream(Stream) is not { } stream)
        {
            return entries.ToImmutable();
        }

        using (stream)
        using (var reader = new BinaryReader(stream, Encoding.UTF8))
        {
            reader.ReadInt32();
            if (reader.ReadInt32() != Version)
            {
                return entries.ToImmutable();
            }

            var count = reader.ReadInt32();
            for (var index = 0; index < count; index++)
            {
                var path = reader.ReadString();
                var offset = reader.ReadInt32();
                var size = reader.ReadInt32();
                entries[Key(path)] = (offset, size);
            }
        }

        return entries.ToImmutable();
    }
}
