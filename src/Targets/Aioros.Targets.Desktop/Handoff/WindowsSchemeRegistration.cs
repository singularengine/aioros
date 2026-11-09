using System.Collections.Immutable;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Aioros.Targets.Desktop.Handoff;

/// <summary>A registry value under the current user's hive; an empty name is the key's default value.</summary>
public sealed record RegistryValue(string Key, string Name, string Value);

/// <summary>
/// Registers the app as the handler of its scheme on Windows at run time, under <c>HKCU\Software\Classes</c> as Tauri's
/// deep-link plugin does (Microsoft's "Registering an Application to a URI Scheme"), so no administrator is needed.
/// Idempotent: only the values that differ are written.
/// </summary>
public static class WindowsSchemeRegistration
{
    public static ImmutableArray<RegistryValue> Entries(string scheme, string title, string executable)
    {
        var key = @"Software\Classes\" + scheme.ToLowerInvariant();
        return
        [
            new RegistryValue(key, string.Empty, "URL:" + title),
            new RegistryValue(key, "URL Protocol", string.Empty),
            new RegistryValue(key + @"\shell\open\command", string.Empty, "\"" + executable + "\" \"%1\""),
        ];
    }

    /// <summary>The values whose current content, as <paramref name="current"/> reads it, differs.</summary>
    public static ImmutableArray<RegistryValue> Changes(IEnumerable<RegistryValue> entries, Func<RegistryValue, string?> current) =>
        [.. entries.Where(entry => current(entry) != entry.Value)];

    /// <summary>Writes the values that differ; <see langword="true"/> when one did.</summary>
    [SupportedOSPlatform("windows")]
    public static bool Register(string scheme, string title, string executable)
    {
        var changes = Changes(Entries(scheme, title, executable), Read);
        foreach (var change in changes)
        {
            using var key = Registry.CurrentUser.CreateSubKey(change.Key);
            key.SetValue(change.Name, change.Value);
        }

        return changes.Length > 0;
    }

    [SupportedOSPlatform("windows")]
    private static string? Read(RegistryValue entry)
    {
        using var key = Registry.CurrentUser.OpenSubKey(entry.Key);
        return key?.GetValue(entry.Name) as string;
    }
}
