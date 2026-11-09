using System.Net;
using System.Text.RegularExpressions;
using Aioros.Functions;
using Aioros.Generators.Tests.Pages;
using Aioros.Runtime.Functions;

namespace Aioros.Generators.Tests.Functions;

public sealed partial class FunctionKeyGeneratorTests
{
    private const string Issues = """
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Functions;

        namespace Track;

        public sealed record IssueChanges(string Id, string Title);

        public interface IIssues
        {
            Task<string> FindAsync(int id, CancellationToken cancellationToken);

            Task SaveAsync(IssueChanges changes);

            Task TouchAsync();

            Task FailAsync(int id);
        }

        public sealed class Issues : IIssues
        {
            [Reads("issues", "issue:{id}")]
            public Task<string> FindAsync(int id, CancellationToken cancellationToken) => Task.FromResult("issue " + id);

            [Invalidates("issues", "issue:{changes.Id}")]
            public Task SaveAsync(IssueChanges changes) => Task.CompletedTask;

            public Task TouchAsync() => Task.CompletedTask;

            [Invalidates("issue:{id}")]
            public Task FailAsync(int id) => throw new System.InvalidOperationException("The store is down.");
        }

        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IIssues, Issues>(builder);
        }
        """;

    [Fact]
    [Trait("AC", "FUNCTIONS-16")]
    public async Task ImplementationsDeclareTheKeysTheirCallsRecordAsync()
    {
        var result = Build(Issues);
        var assembly = result.Load();
        var group = (ServerFunctionGroup)assembly.GetType("Track.IssuesFunctions")!.GetProperty("Group")!.GetValue(null)!;
        var undeclared = Build(Attributes().Replace(Issues, string.Empty));
        var issues = assembly.GetType("Track.IIssues")!;
        var implementation = Activator.CreateInstance(assembly.GetType("Track.Issues")!)!;
        var loader = new Recorded();
        var loading = group.InProcess!(implementation);
        ((IKeyedCalls)loading).Keys = loader;
        var found = (Task<string>)issues.GetMethod("FindAsync")!.Invoke(loading, [7, CancellationToken.None])!;
        await found;
        var command = group.InProcess(implementation);
        var changes = DataKeys.Begin();
        await (Task)issues.GetMethod("SaveAsync")!.Invoke(command, [Activator.CreateInstance(assembly.GetType("Track.IssueChanges")!, "LUM-7", "Title")])!;
        var failed = await Record.ExceptionAsync(() => (Task)issues.GetMethod("FailAsync")!.Invoke(command, [9])!);
        var declaredOnly = (string.Join(",", changes.Invalidated.Order(StringComparer.Ordinal)), changes.Unknown);
        await (Task)issues.GetMethod("TouchAsync")!.Invoke(command, [])!;
        DataKeys.End();
        var server = new Answering();
        var client = group.Client(new HttpClient(server) { BaseAddress = new Uri("http://app.test") });
        var remote = DataKeys.Begin();
        await (Task)issues.GetMethod("SaveAsync")!.Invoke(client, [Activator.CreateInstance(assembly.GetType("Track.IssueChanges")!, "LUM-8", "Title")])!;
        var remoteDeclared = (string.Join(",", remote.Invalidated.Order(StringComparer.Ordinal)), remote.Unknown);
        await (Task)issues.GetMethod("TouchAsync")!.Invoke(client, [])!;
        DataKeys.End();

        Assert.Empty(result.Diagnostics);
        Assert.Empty(undeclared.Diagnostics);
        Assert.Equal(group.Contract, ((ServerFunctionGroup)undeclared.Load().GetType("Track.IssuesFunctions")!.GetProperty("Group")!.GetValue(null)!).Contract);
        Assert.Equal("issue 7", await found);
        Assert.Equal(["issues", "issue:7"], loader.Reads);
        Assert.Empty(loader.Invalidations);
        Assert.Equal("The server function failed.", failed?.Message);
        Assert.Equal(("issue:LUM-7,issues", false), declaredOnly);
        Assert.True(changes.Unknown);
        Assert.Equal([("find", true), ("save", true), ("touch", false), ("fail", true)], group.Routes.Select(route => (route.Name, route.Keyed)));
        Assert.Equal(("issue:LUM-8,issues", false), remoteDeclared);
        Assert.True(remote.Unknown);
    }

    [Fact]
    [Trait("AC", "GENERATOR-45")]
    public void KeysThatCannotBeWrittenAreReported()
    {
        var cases = Unwritable
            .Select(key => Build(Issues.Replace("\"issue:{changes.Id}\"", "\"" + key + "\"", StringComparison.Ordinal)))
            .ToList();

        Assert.All(cases, result => Assert.Equal("AIOROS019", Assert.Single(result.Diagnostics).Id));
        Assert.Equal(
            [
                "The key 'issue:{nope}' of 'Track.IIssues.SaveAsync' cannot be written: 'nope' is no parameter of 'SaveAsync'",
                "The key 'issue:{changes.Nope}' of 'Track.IIssues.SaveAsync' cannot be written: 'Track.IssueChanges' has no member 'Nope'",
                "The key 'issue:{id' of 'Track.IIssues.SaveAsync' cannot be written: a placeholder is left open",
                "The key 'issue:id}' of 'Track.IIssues.SaveAsync' cannot be written: it closes a placeholder it never opened",
                "The key ' ' of 'Track.IIssues.SaveAsync' cannot be written: a key cannot be empty",
            ],
            cases.Select(result => result.Diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.All(cases, result => Assert.Contains("Invalidates(", result.Diagnostics[0].Location.SourceTree!.ToString().Substring(result.Diagnostics[0].Location.SourceSpan.Start, result.Diagnostics[0].Location.SourceSpan.Length), StringComparison.Ordinal));
        Assert.All(cases, result => Assert.DoesNotContain(result.Sources, source => source.Contains("class IssuesInProcess", StringComparison.Ordinal)));
    }

    private static readonly string[] Unwritable = ["issue:{nope}", "issue:{changes.Nope}", "issue:{id", "issue:id}", " "];

    private static GeneratorResult Build(string source) => new GeneratorHarness().Source(ServerFunctionGeneratorTests.Hosting).Source(source).Run();

    [GeneratedRegex(@"\[(Reads|Invalidates)\([^\]]*\)\]")]
    private static partial Regex Attributes();

    private sealed class Recorded : IDataKeys
    {
        public List<string> Reads { get; } = [];

        public List<string> Invalidations { get; } = [];

        public void Read(string key) => Reads.Add(key);

        public void Invalidate(string key) => Invalidations.Add(key);
    }

    private sealed class Answering : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            if (request.RequestUri!.AbsolutePath.EndsWith("/save", StringComparison.Ordinal))
            {
                response.Headers.Add(ServerFunctionGroup.RevalidateHeader, "issues,issue%3ALUM-8");
            }

            return Task.FromResult(response);
        }
    }
}
