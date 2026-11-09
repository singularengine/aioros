using Aioros.Functions;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Caching;

/// <summary>
/// The <see cref="IDataKeys"/> of the app's services: what they read and invalidate is recorded into the command running
/// in the caller's flow, and what they invalidate also evicts what the caches kept from those keys, whether a command
/// runs or not.
/// </summary>
public sealed class EvictingKeys(DataVersions versions) : IDataKeys
{
    public void Read(string key) => DataKeys.Ambient.Read(key);

    public void Invalidate(string key)
    {
        DataKeys.Ambient.Invalidate(key);
        versions.Invalidate(key);
    }
}
