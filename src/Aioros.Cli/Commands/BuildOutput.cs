using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Aioros.Cli.Commands;

public sealed record BuildError(string File, int Line, int Column, string Code, string Message);

/// <summary>
/// Reads <c>dotnet watch</c>'s output line by line for the errors of an edit that does not compile, so <c>aioros dev</c> can
/// show them over the page: the list so far after each error line, and the cleared list once an edit applies.
/// </summary>
public sealed partial class BuildOutput
{
    private ImmutableArray<BuildError> _errors = [];

    private bool _shown;

    /// <summary>The errors to show after this line, an empty list once they are fixed, or null when nothing changes.</summary>
    public ImmutableArray<BuildError>? Read(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = Prefix().Replace(Colors().Replace(line, string.Empty), string.Empty);
        if (text.StartsWith("File updated:", StringComparison.Ordinal) || text.StartsWith("Files updated:", StringComparison.Ordinal))
        {
            _errors = [];
            return null;
        }

        if (Applied().IsMatch(text))
        {
            _errors = [];
            return _shown ? Shown(false) : null;
        }

        if (Error().Match(text) is not { Success: true } error)
        {
            return null;
        }

        _errors = _errors.Add(new BuildError(
            error.Groups["file"].Value,
            int.Parse(error.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(error.Groups["column"].Value, System.Globalization.CultureInfo.InvariantCulture),
            error.Groups["code"].Value,
            error.Groups["message"].Value));
        return Shown(true);
    }

    private ImmutableArray<BuildError> Shown(bool shown)
    {
        _shown = shown;
        return _errors;
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*m")]
    private static partial Regex Colors();

    [GeneratedRegex(@"^(dotnet watch\s*\S*\s*:\s*)?(\[[^\]]+\]\s*)?")]
    private static partial Regex Prefix();

    [GeneratedRegex(@"changes applied|No managed code changes to apply|^Build succeeded")]
    private static partial Regex Applied();

    [GeneratedRegex(@"^(?<file>.+?)\((?<line>\d+),(?<column>\d+)(,\d+,\d+)?\): error (?<code>[A-Z]+\d+): (?<message>.+?)(\s+\[[^\]]+\])?$")]
    private static partial Regex Error();
}
