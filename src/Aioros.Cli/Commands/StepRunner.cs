using System.Diagnostics;

namespace Aioros.Cli.Commands;

/// <summary>Runs a plan's <c>dotnet</c> steps in order from the app folder, stopping at the first that fails; <c>dotnet watch</c> runs as a <see cref="WatchedRun"/>.</summary>
public static class StepRunner
{
    public static async Task<int> RunAsync(CommandPlan plan, string directory, string token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var step in plan.Steps)
        {
            var start = new ProcessStartInfo(DotnetHost.Path) { WorkingDirectory = step.WorkingDirectory is { } relative ? Path.Combine(directory, relative) : directory };
            foreach (var argument in step.Arguments)
            {
                start.ArgumentList.Add(argument);
            }

            foreach (var (name, value) in step.Environment)
            {
                start.Environment[name] = value;
            }

            var exit = step.Arguments[0] == "watch" && step.Arguments[^1].StartsWith(UrlsOption, StringComparison.Ordinal)
                ? await WatchedRun.RunAsync(start, new Uri(step.Arguments[^1][UrlsOption.Length..]), token)
                : await RunAsync(start);
            if (exit != 0)
            {
                return exit;
            }
        }

        return plan.ExitCode;
    }

    private const string UrlsOption = "--urls=";

    private static async Task<int> RunAsync(ProcessStartInfo start)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet could not be started.");
        using var stop = new ToolStop(process);
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
