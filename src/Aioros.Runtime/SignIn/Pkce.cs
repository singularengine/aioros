using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Aioros.Runtime.SignIn;

/// <summary>Proof Key for Code Exchange (RFC 7636): a random verifier and its S256 challenge, and a random state.</summary>
public static class Pkce
{
    public static string Verifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string State() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    public static string Challenge(string verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    }
}
