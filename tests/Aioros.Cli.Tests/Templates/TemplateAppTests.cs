using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aioros.Cli.Tests.Templates;

public sealed partial class TemplateAppTests
{
    private static readonly string[] Framework =
    [
        "src/Aioros/Aioros.csproj",
        "src/Aioros.Abstractions/Aioros.Abstractions.csproj",
        "src/Aioros.Runtime/Aioros.Runtime.csproj",
        "src/Aioros.Routing/Aioros.Routing.csproj",
        "src/Adapters/Aioros.Avalonia/Aioros.Avalonia.csproj",
        "src/Targets/Aioros.Targets.Ssr/Aioros.Targets.Ssr.csproj",
        "src/Targets/Aioros.Targets.Desktop/Aioros.Targets.Desktop.csproj",
        "src/Platform/Aioros.Platform/Aioros.Platform.csproj",
        "src/Aioros.Markup/Aioros.Markup.csproj",
    ];

    [Fact]
    [Trait("AC", "CLI-06")]
    public async Task TheTemplateCreatesAnAppThatBuildsAndPrecompilesAsync()
    {
        var repository = Root(AppContext.BaseDirectory);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userCache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured ? configured : Path.Combine(home, ".nuget", "packages");
        var userHive = Path.Combine(home, ".templateengine");
        var before = (Stamp(userCache), Stamp(userHive));
        var work = Directory.CreateTempSubdirectory("aioros-template-");
        try
        {
            var feed = Path.Combine(work.FullName, "feed");
            var hive = Path.Combine(work.FullName, "hive");
            var cache = Path.Combine(work.FullName, "packages");
            var plans = Path.Combine(work.FullName, "plans.json");
            var solution = Path.Combine(work.FullName, "pack.slnx");
            await File.WriteAllTextAsync(solution, "<Solution>" + string.Concat(Framework.Select(project => $"<Project Path=\"{Path.Combine(repository, project)}\" />")) + "</Solution>", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(work.FullName, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <config><add key="globalPackagesFolder" value="{cache}" /></config>
                  <packageSources>
                    <clear />
                    <add key="local" value="{feed}" />
                    <add key="cache" value="{userCache}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="local"><package pattern="Aioros" /><package pattern="Aioros.*" /></packageSource>
                    <packageSource key="cache"><package pattern="*" /></packageSource>
                    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
                  </packageSourceMapping>
                </configuration>
                """, TestContext.Current.CancellationToken);
            var isolated = new Dictionary<string, string> { ["NUGET_PACKAGES"] = cache };
            var repositoryCache = new Dictionary<string, string>();

            var framework = await DotnetAsync(repository, repositoryCache, "pack", solution, "-c", "Debug", "--no-restore", "-p:PackageOutputPath=" + feed);
            var templates = await DotnetAsync(repository, repositoryCache, "pack", Path.Combine(repository, "templates", "Aioros.Templates.csproj"), "-p:PackageOutputPath=" + feed);
            var package = Directory.GetFiles(feed, "Aioros.Templates.*.nupkg").Single();
            var installed = await DotnetAsync(work.FullName, isolated, "new", "install", package, "--debug:custom-hive", hive);
            var created = await DotnetAsync(work.FullName, isolated, "new", "aioros", "-n", "Starter", "-o", "Starter", "--debug:custom-hive", hive);
            var project = Path.Combine(work.FullName, "Starter", "Starter", "Starter.csproj");
            var built = await DotnetAsync(work.FullName, isolated, "build", project, "-tl:off", "--disable-build-servers");
            var referencing = Directory.CreateDirectory(Path.Combine(work.FullName, "Starter", "Starter.Tests"));
            await File.WriteAllTextAsync(Path.Combine(referencing.FullName, "Starter.Tests.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{project}" />
                  </ItemGroup>
                </Project>
                """, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(referencing.FullName, "Probe.cs"), "public static class Probe { public static System.Type Functions => typeof(Aioros.Generated.AiorosFunctions); }", TestContext.Current.CancellationToken);
            var referenced = await DotnetAsync(work.FullName, isolated, "build", referencing.FullName, "-tl:off", "--disable-build-servers");
            var items = await DotnetAsync(work.FullName, isolated, "msbuild", project, "-t:GenerateMSBuildEditorConfigFileShouldRun", "-getItem:AdditionalFiles", "-nologo");
            var xaml = JsonDocument.Parse(items.Output[(items.Output.IndexOf('{', StringComparison.Ordinal))..]).RootElement.GetProperty("Items").GetProperty("AdditionalFiles")
                .EnumerateArray().Select(item => item.GetProperty("FullPath").GetString()!).Where(path => path.EndsWith(".axaml", StringComparison.Ordinal)).ToList();
            var positions = await DotnetAsync(work.FullName, isolated, "msbuild", project, "-getProperty:AvaloniaXamlCreateSourceInfo", "-p:Configuration=Release", "-nologo");
            var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(work.FullName, "Starter", "Starter", "appsettings.json"), TestContext.Current.CancellationToken)).RootElement;
            var launch = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(work.FullName, "Starter", "Starter", "Properties", "launchSettings.json"), TestContext.Current.CancellationToken)).RootElement;
            var ran = await DotnetAsync(work.FullName, isolated, "run", "--no-build", "--no-launch-profile", "--project", project, "--", "--precompile=" + plans);
            var precompiled = Precompiled().Match(ran.Output);

            Assert.True(framework.Exit == 0, framework.Output);
            Assert.True(templates.Exit == 0, templates.Output);
            Assert.True(installed.Exit == 0, installed.Output);
            Assert.True(created.Exit == 0, created.Output);
            Assert.True(built.Exit == 0, built.Output);
            Assert.Matches(@"\b0 Warning\(s\)", built.Output);
            Assert.True(referenced.Exit == 0, referenced.Output);
            Assert.True(items.Exit == 0, items.Output);
            Assert.NotEmpty(xaml);
            Assert.Equal(xaml.Distinct(StringComparer.Ordinal).Count(), xaml.Count);
            Assert.Equal("true", positions.Output.Split('\n')[^2].Trim());
            Assert.Equal("Warning", settings.GetProperty("Logging").GetProperty("LogLevel").GetProperty("Microsoft.AspNetCore").GetString());
            Assert.All(launch.GetProperty("profiles").EnumerateObject(), profile => Assert.False(profile.Value.GetProperty("launchBrowser").GetBoolean()));
            Assert.True(ran.Exit == 0, ran.Output);
            Assert.True(precompiled.Success, ran.Output);
            Assert.Equal(precompiled.Groups["planned"].Value, precompiled.Groups["pages"].Value);
            Assert.NotEqual("0", precompiled.Groups["pages"].Value);
            Assert.True(File.Exists(plans));
            Assert.True(Directory.Exists(Path.Combine(cache, "aioros")));
            Assert.Equal(before, (Stamp(userCache), Stamp(userHive)));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [GeneratedRegex(@"Precompiled (?<planned>\d+) of (?<pages>\d+) pages")]
    private static partial Regex Precompiled();

    private static DateTime? Stamp(string directory) => Directory.Exists(directory) ? Directory.GetLastWriteTimeUtc(directory) : null;

    private static string Root(string directory) =>
        File.Exists(Path.Combine(directory, "Aioros.slnx"))
            ? directory
            : Root(Directory.GetParent(directory)?.FullName ?? throw new DirectoryNotFoundException("Aioros.slnx not found"));

    private static async Task<(int Exit, string Output)> DotnetAsync(string directory, IReadOnlyDictionary<string, string> environment, params string[] arguments)
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

        foreach (var inherited in start.Environment.Keys.Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || key.StartsWith("DOTNET_HOST", StringComparison.Ordinal)).ToList())
        {
            start.Environment.Remove(inherited);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var (key, value) in environment)
        {
            start.Environment[key] = value;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, $"dotnet {string.Join(' ', arguments)}\n{await output}{await error}");
    }
}
