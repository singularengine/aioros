using System.Net;
using System.Text.RegularExpressions;

namespace Aioros.Cli.Tests.Production;

public sealed partial class ProductionAppTests
{
    [Fact]
    [Trait("AC", "PRODUCTION-32")]
    public async Task ModelsThatCreateAvaloniaObjectsServeAndPostInProductionAsync()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var work = Directory.CreateTempSubdirectory("aioros-model-threads-");
        try
        {
            var app = await ModelThreadsAsync(work.FullName);
            var precompiled = await DotnetAsync(app, Path.Combine(app, "ModelThreads.dll"), "--precompile=" + Path.Combine(app, "aioros.plans.json"));
            Assert.True(precompiled.Exit == 0, precompiled.Output);

            using var server = Start(app, "ModelThreads.dll");
            var origin = await ListeningAsync(server, cancellation);
            using var client = new HttpClient { BaseAddress = new Uri(origin) };
            var statuses = new List<(string Route, HttpStatusCode Status)>();
            foreach (var route in (string[])["/account", "/geometry", "/brush", "/"])
            {
                using var answer = await client.GetAsync(new Uri(route, UriKind.Relative), cancellation);
                statuses.Add((route, answer.StatusCode));
            }

            var home = await client.GetStringAsync(new Uri("/", UriKind.Relative), cancellation);
            var fields = Inputs().Matches(home).ToDictionary(input => input.Groups["name"].Value, input => WebUtility.HtmlDecode(input.Groups["value"].Value), StringComparer.Ordinal);
            var name = fields.Single(field => field.Value == "Ada").Key;
            fields[name] = "Grace";
            using var form = new FormUrlEncodedContent(fields);
            using var post = await client.PostAsync(new Uri("/", UriKind.Relative), form, cancellation);
            var posted = await post.Content.ReadAsStringAsync(cancellation);
            server.Kill(entireProcessTree: true);

            Assert.Equal([("/account", HttpStatusCode.OK), ("/geometry", HttpStatusCode.OK), ("/brush", HttpStatusCode.OK), ("/", HttpStatusCode.OK)], statuses);
            Assert.Contains(fields.Keys, key => key.StartsWith("_s", StringComparison.Ordinal));
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            Assert.Contains("Grace", posted, StringComparison.Ordinal);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("AC", "PRODUCTION-33")]
    public async Task APrecompilationLoadsModelsAsProductionDoesAsync()
    {
        var work = Directory.CreateTempSubdirectory("aioros-model-threads-");
        try
        {
            var app = await ModelThreadsAsync(work.FullName);
            var plans = "--precompile=" + Path.Combine(app, "aioros.plans.json");
            var geometry = await DotnetAsync(app, [("MODEL_THREADS_ICON", "geometry")], Path.Combine(app, "ModelThreads.dll"), plans);
            var brush = await DotnetAsync(app, [("MODEL_THREADS_ICON", "brush")], Path.Combine(app, "ModelThreads.dll"), plans);
            var clean = await DotnetAsync(app, Path.Combine(app, "ModelThreads.dll"), plans);

            Assert.Equal((1, 1, 0), (geometry.Exit, brush.Exit, clean.Exit));
            Assert.Contains("error AIOROS032: /account: its models failed to load before Avalonia started, as production loads them: ", geometry.Output, StringComparison.Ordinal);
            Assert.Contains("error AIOROS032: /account: its models created an Avalonia object on a request's thread before Avalonia started", brush.Output, StringComparison.Ordinal);
            Assert.Contains("error AIOROS032: /account: the pages after it were not checked", brush.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("AIOROS032: /geometry", geometry.Output + brush.Output, StringComparison.Ordinal);
            Assert.Matches(@"Production models: (\d+) of \1 pages load their models as production does", clean.Output);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static async Task<string> ModelThreadsAsync(string work)
    {
        var app = Path.Combine(work, "app");
        var published = await DotnetAsync(Root(AppContext.BaseDirectory), "publish", Path.Combine(Root(AppContext.BaseDirectory), "tests", "Apps", "ModelThreads", "ModelThreads.csproj"), "-c", "Release", "-o", app, "-nologo", "--disable-build-servers");
        Assert.True(published.Exit == 0, published.Output);
        return app;
    }

    [GeneratedRegex("<input[^>]*?name=\"(?<name>[^\"]+)\"[^>]*?value=\"(?<value>[^\"]*)\"")]
    private static partial Regex Inputs();
}
