using System.Text;

namespace Aioros.Targets.Desktop.Handoff;

/// <summary>The XDG base directories a registration writes to: <c>$XDG_DATA_HOME</c> and <c>$XDG_CONFIG_HOME</c>.</summary>
public sealed record XdgPlaces(string DataHome, string ConfigHome)
{
    /// <summary>The current user's directories, with the XDG Base Directory specification's defaults.</summary>
    public static XdgPlaces Current
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new(
                Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } data ? data : Path.Combine(home, ".local", "share"),
                Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } config ? config : Path.Combine(home, ".config"));
        }
    }
}

/// <summary>
/// Registers the app as the handler of its scheme on Linux, as Tauri's deep-link plugin does: a hidden desktop entry
/// with <c>MimeType=x-scheme-handler/{scheme}</c> and the default for that type in <c>mimeapps.list</c>, which is what
/// <c>xdg-mime default</c> writes. Idempotent: a file is written only when its content changes.
/// </summary>
public static class LinuxSchemeRegistration
{
    private const string Defaults = "[Default Applications]";

    public static string EntryName(string scheme) => scheme.ToLowerInvariant() + "-handler.desktop";

    /// <summary>Registers <paramref name="executable"/> for <paramref name="scheme"/>; <see langword="true"/> when a file changed.</summary>
    public static async Task<bool> RegisterAsync(string scheme, string title, string executable, XdgPlaces places, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(places);
        var entry = Entry(scheme, title, executable);
        var applications = Path.Combine(places.DataHome, "applications");
        var wroteEntry = await WriteIfChangedAsync(Path.Combine(applications, EntryName(scheme)), entry, cancellationToken);
        var list = Path.Combine(places.ConfigHome, "mimeapps.list");
        var current = File.Exists(list) ? await File.ReadAllTextAsync(list, cancellationToken) : string.Empty;
        var wroteDefault = await WriteIfChangedAsync(list, WithDefault(current, "x-scheme-handler/" + scheme.ToLowerInvariant(), EntryName(scheme)), cancellationToken);
        return wroteEntry || wroteDefault;
    }

    /// <summary>The desktop entry: hidden from menus, opening the app with the URL as its argument.</summary>
    public static string Entry(string scheme, string title, string executable) => $"""
        [Desktop Entry]
        Type=Application
        Name={title}
        Exec={Quote(executable)} %u
        MimeType=x-scheme-handler/{scheme.ToLowerInvariant()};
        NoDisplay=true
        Terminal=false

        """;

    /// <summary><paramref name="list"/> with <c>{type}={entry}</c> under <c>[Default Applications]</c>, every other line kept.</summary>
    public static string WithDefault(string list, string type, string entry)
    {
        ArgumentNullException.ThrowIfNull(list);
        var lines = list.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines is [.., ""])
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var section = lines.FindIndex(line => line.Trim() == Defaults);
        if (section < 0)
        {
            lines.InsertRange(0, lines.Count == 0 ? [Defaults] : [Defaults, string.Empty]);
            section = 0;
        }

        var end = lines.FindIndex(section + 1, line => line.TrimStart().StartsWith('['));
        end = end < 0 ? lines.Count : end;
        var existing = lines.FindIndex(section + 1, end - section - 1, line => line.Split('=', 2)[0].Trim() == type);
        var setting = type + "=" + entry;
        if (existing >= 0)
        {
            lines[existing] = setting;
        }
        else
        {
            var last = end;
            while (last > section + 1 && lines[last - 1].Trim().Length == 0)
            {
                last--;
            }

            lines.Insert(last, setting);
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Quotes an argument of an <c>Exec</c> key: in double quotes with <c>"</c>, <c>`</c>, <c>$</c> and <c>\</c> escaped by a
    /// backslash, then the backslashes escaped again, as the Desktop Entry specification's string rule applies first.
    /// </summary>
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        var quoted = new StringBuilder("\"");
        foreach (var character in argument)
        {
            quoted.Append(character switch
            {
                '"' or '`' or '$' or '\\' => "\\" + character,
                '%' => "%%",
                _ => character.ToString(),
            });
        }

        return quoted.Append('"').ToString().Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static async Task<bool> WriteIfChangedAsync(string path, string content, CancellationToken cancellationToken)
    {
        if (File.Exists(path) && await File.ReadAllTextAsync(path, cancellationToken) == content)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".aioros-" + Environment.ProcessId;
        await File.WriteAllTextAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
        return true;
    }
}
