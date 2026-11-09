using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Aioros.Runtime.SignIn;

/// <summary>The installed app's tokens: a short-lived access token, the refresh token that replaces it, and when it expires.</summary>
public sealed record TokenSet(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    public string Write()
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("access_token", AccessToken);
            writer.WriteString("refresh_token", RefreshToken);
            writer.WriteNumber("expires_at", ExpiresAt.ToUnixTimeSeconds());
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Reads what <see cref="Write"/> wrote; anything else reads as no tokens.</summary>
    public static TokenSet? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.TryGetProperty("access_token", out var access) && root.TryGetProperty("refresh_token", out var refresh) && root.TryGetProperty("expires_at", out var expires)
                ? new TokenSet(access.GetString() ?? string.Empty, refresh.GetString() ?? string.Empty, DateTimeOffset.FromUnixTimeSeconds(expires.GetInt64()))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
