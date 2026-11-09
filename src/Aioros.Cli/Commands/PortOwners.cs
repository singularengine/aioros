using System.Diagnostics;
using System.Globalization;

namespace Aioros.Cli.Commands;

/// <summary>Which process listens on a local port, read from Linux's <c>/proc</c>; other systems have no such file and report nothing.</summary>
public static class PortOwners
{
    private const string Listening = "0A";

    private static readonly string[] Tables = ["/proc/net/tcp", "/proc/net/tcp6"];

    /// <summary>The process listening on <paramref name="port"/> as <c>name (pid n)</c>, or null when the machine cannot tell.</summary>
    public static string? Of(int port)
    {
        if (!OperatingSystem.IsLinux() || Socket(port) is not { } socket)
        {
            return null;
        }

        var owner = Directory.EnumerateDirectories("/proc")
            .Select(static folder => int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (int?)null)
            .FirstOrDefault(id => id is { } process && Holds(process, socket));
        return owner is { } found ? $"{Process.GetProcessById(found).ProcessName} (pid {found})" : null;
    }

    private static string? Socket(int port) =>
        Tables.Where(File.Exists).SelectMany(File.ReadLines).Skip(1)
            .Select(static line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length > 9 && fields[3] == Listening && fields[1].EndsWith(":" + port.ToString("X4", CultureInfo.InvariantCulture), StringComparison.Ordinal))
            .Select(static fields => $"socket:[{fields[9]}]")
            .FirstOrDefault();

    private static bool Holds(int process, string socket)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries($"/proc/{process}/fd").Any(descriptor => new FileInfo(descriptor).LinkTarget == socket);
        }
        catch (Exception failure) when (failure is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
