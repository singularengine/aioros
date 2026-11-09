using System.Collections.Immutable;
using System.Text.Json;

namespace Aioros.Cli.Commands;

public sealed record DotnetStep(ImmutableArray<string> Arguments, ImmutableDictionary<string, string> Environment)
{
    /// <summary>Where the step runs, relative to the app folder; the app folder itself when null.</summary>
    public string? WorkingDirectory { get; init; }

    public static DotnetStep Of(params string[] arguments) => new([.. arguments], ImmutableDictionary<string, string>.Empty);
}

public sealed record CommandPlan(int ExitCode, string Output, ImmutableArray<DotnetStep> Steps)
{
    public static CommandPlan Print(string output, int exitCode = 0) => new(exitCode, output, []);

    public static CommandPlan Run(params DotnetStep[] steps) => new(0, string.Empty, [.. steps]);
}

/// <summary>
/// The app the command acts on: its project, relative to the folder the tool runs in, and its page files, relative to
/// the project. <see cref="Problem"/> says why there is no project.
/// </summary>
public sealed record AppFolder(string? Project, IReadOnlyList<string> Files)
{
    public string? Problem { get; init; }

    /// <summary>The port of the project's launch profile, which <c>aioros dev</c> listens on unless it is told another.</summary>
    public int? Port { get; init; }

    /// <summary>
    /// The port of the first <c>Project</c> profile's first <c>http</c> <c>applicationUrl</c> in a <c>launchSettings.json</c>,
    /// the address <c>dotnet run</c> would listen on; null when there is none or the file does not parse.
    /// </summary>
    public static int? LaunchPort(string launchSettings)
    {
        try
        {
            using var settings = JsonDocument.Parse(launchSettings);
            return settings.RootElement.TryGetProperty("profiles", out var profiles)
                ? profiles.EnumerateObject()
                    .Where(static profile => profile.Value.TryGetProperty("commandName", out var command) && command.GetString() == "Project")
                    .SelectMany(static profile => profile.Value.TryGetProperty("applicationUrl", out var urls) ? (urls.GetString() ?? string.Empty).Split(';') : [])
                    .Select(static url => Uri.TryCreate(url.Trim(), UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttp ? address.Port : (int?)null)
                    .FirstOrDefault(static port => port is not null)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The app among the projects at the root and one folder down: the one with a <c>Pages</c> folder, as the solution
    /// folder <c>dotnet new aioros</c> creates holds the app next to other projects (an Aspire app host, tests).
    /// </summary>
    public static AppFolder Find(IReadOnlyList<string> projects, Func<string, bool> hasPages)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(hasPages);
        var apps = projects.Where(hasPages).ToList();
        return apps switch
        {
            [var app] => new AppFolder(app, []),
            [] => new AppFolder(null, []) { Problem = "No Aioros app found: no project here or one folder down has a Pages folder.\n" },
            _ => new AppFolder(null, []) { Problem = "Several Aioros apps found; run the command in one of their folders:\n" + string.Concat(apps.Select(app => $"  {app}\n")) },
        };
    }
}
