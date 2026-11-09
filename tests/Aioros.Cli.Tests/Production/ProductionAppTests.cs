using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace Aioros.Cli.Tests.Production;

public sealed partial class ProductionAppTests
{
    private static readonly string[] Avalonia = ["Aioros.Avalonia", "Avalonia.Base", "Avalonia.Controls", "SkiaSharp", "HarfBuzzSharp"];

    [Fact]
    [Trait("AC", "PRODUCTION-16")]
    public async Task APrecompiledAppServesPagesAndPostsWithoutLoadingAvaloniaAsync()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var repository = Root(AppContext.BaseDirectory);
        var work = Directory.CreateTempSubdirectory("aioros-production-");
        try
        {
            var app = Path.Combine(work.FullName, "app");
            var plans = Path.Combine(app, "aioros.plans.json");
            var published = await DotnetAsync(repository, "publish", Path.Combine(repository, "samples", "Lumen", "Lumen", "Lumen.csproj"), "-c", "Release", "-o", app, "-nologo", "--disable-build-servers");
            Assert.True(published.Exit == 0, published.Output);
            var precompiled = await DotnetAsync(app, Path.Combine(app, "Lumen.dll"), "--precompile=" + plans);
            Assert.True(precompiled.Exit == 0, precompiled.Output);

            using var server = Start(app);
            var origin = await ListeningAsync(server, cancellation);
            using var client = new HttpClient { BaseAddress = new Uri(origin) };
            using var issues = await client.GetAsync(new Uri("/issues", UriKind.Relative), cancellation);
            using var issue = await client.GetAsync(new Uri("/issues/LUM-105", UriKind.Relative), cancellation);
            using var dark = new HttpRequestMessage(HttpMethod.Get, "/inbox") { Headers = { { "Cookie", "theme=dark" } } };
            using var inbox = await client.SendAsync(dark, cancellation);
            var settings = await client.GetStringAsync(new Uri("/settings", UriKind.Relative), cancellation);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["f0"] = "Ada Lovelace",
                ["f1"] = "ada@lumen.dev",
                ["f2"] = "on",
                ["_btn"] = WebUtility.HtmlDecode(Button().Match(settings).Groups["value"].Value),
            });
            using var post = await client.PostAsync(new Uri("/settings", UriKind.Relative), form, cancellation);
            var saved = await post.Content.ReadAsStringAsync(cancellation);
            var loaded = (await client.GetStringAsync(new Uri("/_assemblies", UriKind.Relative), cancellation)).Split('\n');
            server.Kill(entireProcessTree: true);

            Assert.Contains("Precompiled 7 of 8 pages", precompiled.Output, StringComparison.Ordinal);
            Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (issues.StatusCode, issue.StatusCode, inbox.StatusCode, post.StatusCode));
            Assert.Contains("Changes saved.", saved, StringComparison.Ordinal);
            Assert.Contains("value=\"Ada Lovelace\"", saved, StringComparison.Ordinal);
            Assert.Contains("Aioros.Runtime", loaded);
            Assert.Empty(loaded.Intersect(Avalonia, StringComparer.Ordinal));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("AC", "CLI-09")]
    public async Task APrecompiledAppServesEachCultureWithoutLoadingAvaloniaAsync()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var repository = Root(AppContext.BaseDirectory);
        var work = Directory.CreateTempSubdirectory("aioros-cultures-");
        (string, string)[] cultures = [("Aioros__Cultures", "en-US,fr-FR")];
        try
        {
            var app = Path.Combine(work.FullName, "app");
            var plans = Path.Combine(app, "aioros.plans.json");
            var published = await DotnetAsync(repository, "publish", Path.Combine(repository, "samples", "Lumen", "Lumen", "Lumen.csproj"), "-c", "Release", "-o", app, "-nologo", "--disable-build-servers");
            Assert.True(published.Exit == 0, published.Output);
            var precompiled = await DotnetAsync(app, cultures, Path.Combine(app, "Lumen.dll"), "--precompile=" + plans);
            Assert.True(precompiled.Exit == 0, precompiled.Output);
            var json = await File.ReadAllTextAsync(plans, cancellation);

            using var server = Start(app, cultures);
            var origin = await ListeningAsync(server, cancellation);
            using var client = new HttpClient { BaseAddress = new Uri(origin) };
            var (french, frenchBody) = await IssuesAsync(client, "fr-FR", cancellation);
            var (english, englishBody) = await IssuesAsync(client, "en-US", cancellation);
            var loaded = (await client.GetStringAsync(new Uri("/_assemblies", UriKind.Relative), cancellation)).Split('\n');
            server.Kill(entireProcessTree: true);

            Assert.Contains("Cultures: en-US, fr-FR", precompiled.Output, StringComparison.Ordinal);
            Assert.Equal(Count(json, "\"route\""), Count(json, "\"culture\":\"en-US\"") + Count(json, "\"culture\":\"fr-FR\""));
            Assert.Equal(Count(json, "\"culture\":\"en-US\""), Count(json, "\"culture\":\"fr-FR\""));
            Assert.Equal(["fr-FR"], french.Content.Headers.ContentLanguage);
            Assert.Contains("<html lang=\"fr-FR\">", frenchBody, StringComparison.Ordinal);
            Assert.Equal(["en-US"], english.Content.Headers.ContentLanguage);
            Assert.Contains("<html lang=\"en-US\">", englishBody, StringComparison.Ordinal);
            Assert.Empty(loaded.Intersect(Avalonia, StringComparer.Ordinal));
            french.Dispose();
            english.Dispose();
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static int Count(string text, string part) => text.Split(part).Length - 1;

    private static async Task<(HttpResponseMessage Response, string Body)> IssuesAsync(HttpClient client, string language, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/issues") { Headers = { { "Accept-Language", language } } };
        var response = await client.SendAsync(request, cancellationToken);
        return (response, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [GeneratedRegex("name=\"_btn\" value=\"(?<value>[^\"]*)\"")]
    private static partial Regex Button();

    [GeneratedRegex(@"Now listening on: (?<origin>http://\S+)")]
    private static partial Regex Listening();

    private static Process Start(string app, params (string Key, string Value)[] settings) => Start(app, "Lumen.dll", settings);

    private static Process Start(string app, string assembly, params (string Key, string Value)[] settings)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = app,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { Path.Combine(app, assembly), "--urls", "http://127.0.0.1:0" },
        };
        Environment(start);
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        start.Environment["Lumen__Diagnostics"] = "true";
        foreach (var (key, value) in settings)
        {
            start.Environment[key] = value;
        }

        return Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
    }

    private static async Task<string> ListeningAsync(Process server, CancellationToken cancellationToken)
    {
        while (await server.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            if (Listening().Match(line) is { Success: true } match)
            {
                _ = server.StandardOutput.ReadToEndAsync(cancellationToken);
                return match.Groups["origin"].Value;
            }
        }

        throw new InvalidOperationException("The app ended before listening: " + await server.StandardError.ReadToEndAsync(cancellationToken));
    }

    private static string Root(string directory) =>
        File.Exists(Path.Combine(directory, "Aioros.slnx"))
            ? directory
            : Root(Directory.GetParent(directory)?.FullName ?? throw new DirectoryNotFoundException("Aioros.slnx not found"));

    private static void Environment(ProcessStartInfo start)
    {
        foreach (var inherited in start.Environment.Keys.Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || key.StartsWith("DOTNET_HOST", StringComparison.Ordinal)).ToList())
        {
            start.Environment.Remove(inherited);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    }

    private static Task<(int Exit, string Output)> DotnetAsync(string directory, params string[] arguments) => DotnetAsync(directory, [], arguments);

    private static async Task<(int Exit, string Output)> DotnetAsync(string directory, (string Key, string Value)[] settings, params string[] arguments)
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

        Environment(start);
        foreach (var (key, value) in settings)
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
