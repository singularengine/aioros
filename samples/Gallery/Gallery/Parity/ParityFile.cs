using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;

namespace Gallery.Parity;

public sealed record ParityVariant(string Theme, string State, double Texts, double Layout, double Colors, double Pixels);

public sealed record ParityRoute(string Route, string Slug, ImmutableArray<ParityVariant> Variants);

public sealed record ParityData(string Measured, int Width, int Height, string Shown, ImmutableArray<ParityRoute> Routes);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ParityData))]
internal sealed partial class ParityJson : JsonSerializerContext;

/// <summary>The parity report the compatibility bench writes into the web root (<c>bench/compat/run.sh gallery --report</c>).</summary>
public static class ParityFile
{
    public const string Folder = "compat";

    public static string Image(string slug, string kind) => $"/{Folder}/{slug}.{kind}.png";

    public static async Task<ParityData?> ReadAsync(IFileProvider webRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webRoot);
        var file = webRoot.GetFileInfo($"{Folder}/parity.json");
        if (!file.Exists)
        {
            return null;
        }

        await using var stream = file.CreateReadStream();
        return await JsonSerializer.DeserializeAsync(stream, ParityJson.Default.ParityData, cancellationToken);
    }
}
