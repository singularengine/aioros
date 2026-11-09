using System.Diagnostics;

namespace Aioros.Cli.Tests.Packaging;

internal static class PackagedBuild
{
    public static string Repository { get; } = Root(AppContext.BaseDirectory);

    public static async Task<(int Exit, string Output)> DotnetAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var inherited in start.Environment.Keys.Where(static key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || key.StartsWith("DOTNET_HOST", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(inherited);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await output + await error);
    }

    public static async Task<string> PropertyAsync(string project, string property, params string[] arguments)
    {
        var (exit, output) = await DotnetAsync(Repository, ["msbuild", project, "-getProperty:" + property, "-nologo", .. arguments]);
        Assert.True(exit == 0, output);
        return output.Trim();
    }

    private static string Root(string directory) =>
        File.Exists(Path.Combine(directory, "Aioros.slnx"))
            ? directory
            : Root(Directory.GetParent(directory)?.FullName ?? throw new DirectoryNotFoundException("Aioros.slnx not found"));
}
