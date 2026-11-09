using Aioros.Functions;
using Aioros.Generators.Tests.Pages;
using Aioros.Runtime.Functions;

namespace Aioros.Generators.Tests.Functions;

public sealed class FunctionMemoGeneratorTests
{
    private const string Issues = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Functions;

        namespace Track;

        public sealed record Summary(int Id, string Title);

        public interface IIssues
        {
            Task<Summary> SummaryAsync(int id, CancellationToken cancellationToken);

            Task<Summary> PlainAsync(int id);
        }

        public sealed class Issues : IIssues
        {
            public int Calls { get; private set; }

            [Memo]
            [Reads("issue:{id}")]
            public Task<Summary> SummaryAsync(int id, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new Summary(id, "issue " + id));
            }

            public Task<Summary> PlainAsync(int id)
            {
                Calls++;
                return Task.FromResult(new Summary(id, "plain " + id));
            }
        }

        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IIssues, Issues>(builder);
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-50")]
    public async Task MemoizedFunctionsRunThroughTheLoadsMemoAsync()
    {
        var result = Build(Issues);
        var assembly = result.Load();
        var group = (ServerFunctionGroup)assembly.GetType("Track.IssuesFunctions")!.GetProperty("Group")!.GetValue(null)!;
        var issues = assembly.GetType("Track.IIssues")!;
        var implementation = Activator.CreateInstance(assembly.GetType("Track.Issues")!)!;
        int Calls() => (int)implementation.GetType().GetProperty("Calls")!.GetValue(implementation)!;
        var memo = new Memo();
        var loading = group.InProcess!(implementation);
        ((IKeyedCalls)loading).Keys = memo;
        Task<object> CallAsync(object caller, string method, params object[] arguments) => AwaitedAsync((Task)issues.GetMethod(method)!.Invoke(caller, arguments)!);

        var first = await CallAsync(loading, "SummaryAsync", 7, CancellationToken.None);
        var second = await CallAsync(loading, "SummaryAsync", 7, CancellationToken.None);
        var other = await CallAsync(loading, "SummaryAsync", 8, CancellationToken.None);
        var memoized = Calls();
        await CallAsync(loading, "PlainAsync", 7);
        await CallAsync(loading, "PlainAsync", 7);
        var plain = Calls() - memoized;
        var command = group.InProcess(implementation);
        await CallAsync(command, "SummaryAsync", 7, CancellationToken.None);
        await CallAsync(command, "SummaryAsync", 7, CancellationToken.None);
        var commanded = Calls() - memoized - plain;
        var flagged = Cases.Select(flag => Build(Flagged(flag.Result, flag.Value))).ToList();

        Assert.Empty(result.Diagnostics);
        Assert.Same(first, second);
        Assert.NotSame(first, other);
        Assert.Equal(2, memoized);
        Assert.Equal([("issues/summary", (object)ValueTuple.Create(7)), ("issues/summary", ValueTuple.Create(8))], memo.Asked.Distinct());
        Assert.Equal(["issue:7", "issue:7", "issue:8"], memo.Reads);
        Assert.Equal(2, plain);
        Assert.Equal(2, commanded);
        Assert.All(flagged, flag => Assert.Equal(("AIOROS023", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning), (Assert.Single(flag.Diagnostics).Id, flag.Diagnostics[0].Severity)));
        Assert.Equal(
            [
                "'[Memo]' on 'Track.IFlagged.GetAsync' shares its result with every loader of a load that asks, but its result 'int[]' is an array, which a loader could change",
                "'[Memo]' on 'Track.IFlagged.GetAsync' shares its result with every loader of a load that asks, but its result 'System.Collections.Generic.List<int>' is a mutable collection, which a loader could change",
                "'[Memo]' on 'Track.IFlagged.GetAsync' shares its result with every loader of a load that asks, but its result 'Track.Draft' has the public setter 'Title', which a loader could change",
                "'[Memo]' on 'Track.IFlagged.GetAsync' shares its result with every loader of a load that asks, but it returns no result",
            ],
            flagged.Select(flag => flag.Diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static readonly (string? Result, string Value)[] Cases =
    [
        ("int[]", "[1]"),
        ("List<int>", "[1]"),
        ("Draft", "new Draft()"),
        (null, string.Empty),
    ];

    private static string Flagged(string? result, string value) => $$"""
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Aioros.Functions;

        namespace Track;

        public sealed class Draft
        {
            public string Title { get; set; } = "";
        }

        public interface IFlagged
        {
            {{(result is null ? "Task" : "Task<" + result + ">")}} GetAsync();
        }

        public sealed class Flagged : IFlagged
        {
            [Memo]
            public {{(result is null ? "Task" : "Task<" + result + ">")}} GetAsync() => {{(result is null ? "Task.CompletedTask" : "Task.FromResult<" + result + ">(" + value + ")")}};
        }

        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IFlagged, Flagged>(builder);
        }
        """;

    private static GeneratorResult Build(string source) => new GeneratorHarness().Source(ServerFunctionGeneratorTests.Hosting).Source(source).Run();

    private static async Task<object> AwaitedAsync(Task task)
    {
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private sealed class Memo : IDataKeys, ICallMemo
    {
        private readonly Dictionary<(string, object), Task> _calls = [];

        public List<(string Function, object Arguments)> Asked { get; } = [];

        public List<string> Reads { get; } = [];

        public void Read(string key) => Reads.Add(key);

        public void Invalidate(string key)
        {
        }

        public Task<TResult> CallAsync<TResult>(string function, object arguments, Func<Task<TResult>> call)
        {
            Asked.Add((function, arguments));
            if (!_calls.TryGetValue((function, arguments), out var known))
            {
                known = call();
                _calls[(function, arguments)] = known;
            }

            return (Task<TResult>)known;
        }
    }
}
