using System.Collections.Immutable;

namespace Aioros.Targets.Ssr.Development;

/// <summary>The lines of an app's source file around a line, as an error overlay shows them, and its path relative to the content root.</summary>
public static class SourceLines
{
    private const int Around = 2;

    public static async Task<ImmutableArray<FaultLine>> AroundAsync(string path, int line, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var first = Math.Max(1, line - Around);
        var last = Math.Min(lines.Length, line + Around);
        return [.. Enumerable.Range(first, Math.Max(0, last - first + 1)).Select(number => new FaultLine(number, lines[number - 1]))];
    }

    /// <summary>The content root with its trailing separator.</summary>
    public static string Root(string contentRoot) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot)) + Path.DirectorySeparatorChar;

    /// <summary>The file's path relative to the root, with forward slashes, or the path as it is when it is outside the root.</summary>
    public static string Relative(string root, string path)
    {
        var full = Path.GetFullPath(path);
        return full.StartsWith(root, StringComparison.Ordinal) ? Path.GetRelativePath(root, full).Replace('\\', '/') : path;
    }
}
