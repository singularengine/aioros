using System.Collections.Immutable;
using System.Reflection;
using System.Security.Claims;
using Aioros.CodeFixes.Caching;
using Aioros.Functions;
using Aioros.Generators.Tests.Pages;
using Aioros.Pages;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Functions;
using Microsoft.Extensions.Time.Testing;

namespace Aioros.Generators.Tests.Functions;

public sealed class FunctionCacheGeneratorTests
{
    private const string Issues = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Aioros.Functions;

        namespace Track;

        public sealed record Summary(int Id, string Title);

        public interface IIssues
        {
            Task<Summary> FindAsync(int id);

            Task<IReadOnlyList<Summary>> ListAsync();
        }

        public sealed class Issues : IIssues
        {
            public int Calls { get; private set; }

            [Cached(30)]
            [Reads("issue:{id}")]
            public Task<Summary> FindAsync(int id)
            {
                Calls++;
                return Task.FromResult(new Summary(id, "issue " + id));
            }

            [Cached(30, Shared = true)]
            [Reads("issues")]
            public Task<IReadOnlyList<Summary>> ListAsync()
            {
                Calls++;
                return Task.FromResult<IReadOnlyList<Summary>>([new Summary(1, "one")]);
            }
        }

        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IIssues, Issues>(builder);
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-53")]
    public async Task CachedFunctionsCallThroughTheDataCacheAsync()
    {
        var result = Build(Issues);
        var assembly = result.Load();
        var group = Group(assembly, "Track.IssuesFunctions");
        var issues = assembly.GetType("Track.IIssues")!;
        var implementation = Activator.CreateInstance(assembly.GetType("Track.Issues")!)!;
        int Calls() => (int)implementation.GetType().GetProperty("Calls")!.GetValue(implementation)!;
        var time = new FakeTimeProvider();
        var cache = new DataCache(new DataVersions(), time);
        object Caller(ClaimsPrincipal user)
        {
            var caller = group.InProcess!(implementation);
            ((IKeyedCalls)caller).Keys = new Load(user);
            ((ICachedCalls)caller).Cache = cache;
            return caller;
        }

        var (ada, grace) = (Caller(SignedIn("ada")), Caller(SignedIn("grace")));
        var first = await CallAsync(issues, ada, "FindAsync", 7);
        var again = await CallAsync(issues, ada, "FindAsync", 7);
        var hers = await CallAsync(issues, grace, "FindAsync", 7);
        var list = await CallAsync(issues, ada, "ListAsync");
        var sharedList = await CallAsync(issues, grace, "ListAsync");
        var cachedCalls = Calls();
        cache.Invalidate("issue:7");
        await CallAsync(issues, ada, "FindAsync", 7);
        await CallAsync(issues, ada, "ListAsync");
        var afterKey = Calls() - cachedCalls;
        time.Advance(TimeSpan.FromSeconds(30));
        await CallAsync(issues, ada, "ListAsync");
        var afterLifetime = Calls() - cachedCalls - afterKey;
        var flagged = Build(Flagged("[Cached(30, Shared = true)]", "int[]", "[1]"));
        var reading = Build(Flagged("[Cached(30, Shared = true)]", "int", "1", "System.Security.Claims.ClaimsPrincipal user"));

        Assert.Empty(result.Diagnostics);
        Assert.Same(first, again);
        Assert.NotSame(first, hers);
        Assert.Same(list, sharedList);
        Assert.Equal(3, cachedCalls);
        Assert.Equal(1, afterKey);
        Assert.Equal(1, afterLifetime);
        var mutable = Assert.Single(flagged.Diagnostics);
        Assert.Equal(("AIOROS023", "'[Cached]' on 'Track.IFlagged.GetAsync' shares its result with every load that asks until it is evicted, but its result 'int[]' is an array, which a loader could change"), (mutable.Id, mutable.GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
        var user = Assert.Single(reading.Diagnostics);
        Assert.Equal(("AIOROS024", Microsoft.CodeAnalysis.DiagnosticSeverity.Error), (user.Id, user.Severity));
        Assert.Contains("'System.Security.Claims.ClaimsPrincipal'", user.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain(reading.Sources, source => source.Contains(".CallAsync<int>(", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-54")]
    public void CachedLoadersCarryTheirLifetime()
    {
        const string Models = """
            namespace App.Models;
            public sealed record IssueData(string Id);
            public sealed class IssueModel(IssueData data) { public IssueData Data { get; } = data; }
            """;
        static string Loader(string name, string body) => $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Functions;
            using Aioros.Pages;
            namespace App.Loaders;
            [Cached(60)]
            public sealed class {{name}} : ILoader<App.Models.IssueData>
            {
                public ValueTask<LoaderResult<App.Models.IssueData>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult<LoaderResult<App.Models.IssueData>>(new App.Models.IssueData({{body}}));
            }
            """;

        var result = new GeneratorHarness()
            .Source(Models)
            .Source(Loader("IssuesLoader", "request.Path"), "Pages/issues/Page.Loader.cs")
            .Source(Loader("MineLoader", "request.User.Identity?.Name ?? \"\""), "Pages/mine/Page.Loader.cs")
            .Source(Loader("StateLoader", "request.Cookies.ContainsKey(\"sidebar_state\") ? \"kept\" : \"\""), "Pages/state/Page.Loader.cs")
            .Source("using Aioros.Functions; namespace App.Loaders; [Cached(60)] public static class OwnLoader { public static App.Models.IssueData Load(System.Security.Claims.ClaimsPrincipal user) => new(user.Identity?.Name ?? \"\"); }", "Pages/own/Page.Loader.cs")
            .Source("using Aioros.Functions; namespace App.Loaders; [Cached(60)] public static class ItemLoader { public static App.Models.IssueData Load(string id) => new(id); }", "Pages/items/[id]/Page.Loader.cs")
            .Xaml("Pages/issues/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/mine/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/state/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/own/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/items/[id]/Page.axaml", "<TextBlock />", "IssueModel")
            .Run();
        var pages = result.Pages(out _);

        Assert.Equal(TimeSpan.FromSeconds(60), pages.Single(page => page.Route == "/issues").Loader!.Lifetime);
        Assert.Null(pages.Single(page => page.Route == "/mine").Loader!.Lifetime);
        Assert.Null(pages.Single(page => page.Route == "/own").Loader!.Lifetime);
        Assert.Null(pages.Single(page => page.Route == "/state").Loader!.Lifetime);
        Assert.Equal(TimeSpan.FromSeconds(60), pages.Single(page => page.Route == "/items/{id}").Loader!.Lifetime);
        Assert.Equal(
            [("AIOROS024", "App.Loaders.MineLoader"), ("AIOROS024", "App.Loaders.OwnLoader"), ("AIOROS024", "App.Loaders.StateLoader")],
            result.Diagnostics.Select(user => (user.Id, user.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Split('\'')[1])).Order());
    }

    [Fact]
    [Trait("AC", "GENERATOR-123")]
    public async Task CachesThatCouldDependOnTheUserAreFixedAsync()
    {
        const string Mine = """
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Functions;
            using Aioros.Pages;
            namespace App.Loaders;
            [Cached(60)]
            public sealed class MineLoader : ILoader<App.Models.IssueData>
            {
                public ValueTask<LoaderResult<App.Models.IssueData>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult<LoaderResult<App.Models.IssueData>>(new App.Models.IssueData(request.User.Identity?.Name ?? ""));
            }
            """;
        GeneratorResult Loaders(string loader) => new GeneratorHarness()
            .Source("namespace App.Models; public sealed record IssueData(string Id); public sealed class IssueModel(IssueData data) { public IssueData Data { get; } = data; }")
            .Source(loader, "Pages/mine/Page.Loader.cs")
            .Xaml("Pages/mine/Page.axaml", "<TextBlock />", "IssueModel")
            .Run();
        var fix = new UserCacheFix();
        var function = Flagged("[Cached(30, Shared = true)]", "int", "1", "System.Security.Claims.ClaimsPrincipal user");

        var shared = await CodeFixRunner.ApplyAsync(fix, Assert.Single(Build(function).Diagnostics, static diagnostic => diagnostic.Id == "AIOROS024"));
        var loader = await CodeFixRunner.ApplyAsync(fix, Assert.Single(Loaders(Mine).Diagnostics, static diagnostic => diagnostic.Id == "AIOROS024"));

        Assert.Equal(function.Replace("[Cached(30, Shared = true)]", "[Cached(30)]", StringComparison.Ordinal), shared);
        Assert.Empty(Build(shared).Diagnostics);
        Assert.Equal(Mine.Replace("[Cached(60)]\n", string.Empty, StringComparison.Ordinal), loader);
        Assert.DoesNotContain(Loaders(loader).Diagnostics, static diagnostic => diagnostic.Id == "AIOROS024");
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-24")]
    public async Task SavingALumenIssueEvictsTheCachedListAndDetailAsync()
    {
        var data = Path.Combine(Root(), "samples", "Lumen", "Lumen", "Data");
        var harness = new GeneratorHarness().Source("global using System; global using System.Linq; global using System.Collections.Generic; global using System.Threading; global using System.Threading.Tasks;");
        foreach (var file in new[] { "Store.cs", "IIssueFunctions.cs", "IProfileFunctions.cs", "IRecentFunctions.cs", "LumenFunctions.cs" })
        {
            harness.Source(await File.ReadAllTextAsync(Path.Combine(data, file), TestContext.Current.CancellationToken));
        }

        var result = harness
            .Source(ServerFunctionGeneratorTests.Hosting)
            .Source("namespace Lumen.Data; public static class Setup { public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IIssueFunctions, IssueFunctions>(builder); }")
            .Run();
        var assembly = result.Load();
        var contract = assembly.GetType("Lumen.Data.IIssueFunctions")!;
        var store = Activator.CreateInstance(assembly.GetType("Lumen.Data.Store")!)!;
        var implementation = Activator.CreateInstance(assembly.GetType("Lumen.Data.IssueFunctions")!, store)!;
        var group = Group(assembly, "Lumen.Data.IssueFunctionsFunctions");
        var cache = new DataCache(new DataVersions(), TimeProvider.System);
        object Caller(IDataKeys? keys)
        {
            var caller = group.InProcess!(implementation);
            ((IKeyedCalls)caller).Keys = keys;
            ((ICachedCalls)caller).Cache = cache;
            return caller;
        }

        var load = Caller(new Load(new ClaimsPrincipal()));
        var list = await CallAsync(contract, load, "ListAsync");
        var id = Id(((System.Collections.IEnumerable)list).Cast<object>().First());
        var issue = await CallAsync(contract, load, "FindAsync", id);
        var direct = issue.GetType().GetMethod("<Clone>$")!.Invoke(issue, null)!;
        direct.GetType().GetProperty("Title")!.SetValue(direct, "Changed behind the functions' back");
        store.GetType().GetMethod("Update", [direct.GetType()])!.Invoke(store, [direct]);
        var cached = (await CallAsync(contract, Caller(new Load(new ClaimsPrincipal())), "ListAsync"), await CallAsync(contract, Caller(new Load(new ClaimsPrincipal())), "FindAsync", id));
        var changes = Activator.CreateInstance(assembly.GetType("Lumen.Data.IssueChanges")!, id, "Saved title", "", "todo", "high", "Ada Lovelace")!;
        await (Task)contract.GetMethod("SaveAsync")!.Invoke(Caller(null), [changes])!;
        var fresh = (await CallAsync(contract, Caller(new Load(new ClaimsPrincipal())), "ListAsync"), await CallAsync(contract, Caller(new Load(new ClaimsPrincipal())), "FindAsync", id));
        static string Id(object issue) => (string)issue.GetType().GetProperty("Id")!.GetValue(issue)!;
        static string Title(object issue) => (string)issue.GetType().GetProperty("Title")!.GetValue(issue)!;
        static object First(object list, string id) => ((System.Collections.IEnumerable)list).Cast<object>().First(issue => Id(issue) == id);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity >= Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.True(list.Equals(cached.Item1));
        Assert.Same(issue, cached.Item2);
        Assert.NotEqual("Changed behind the functions' back", Title(First(cached.Item1, id)));
        Assert.False(list.Equals(fresh.Item1));
        Assert.Equal(("Saved title", "Saved title"), (Title(First(fresh.Item1, id)), Title(fresh.Item2)));
    }

    private static string Flagged(string attribute, string result, string value, string parameters = "") => $$"""
        using System.Threading.Tasks;
        using Aioros.Functions;

        namespace Track;

        public interface IFlagged
        {
            Task<{{result}}> GetAsync();
        }

        public sealed class Flagged({{parameters}}) : IFlagged
        {
            {{attribute}}
            public Task<{{result}}> GetAsync() => Task.FromResult<{{result}}>({{value}});
        }

        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IFlagged, Flagged>(builder);
        }
        """;

    private static GeneratorResult Build(string source) => new GeneratorHarness().Source(ServerFunctionGeneratorTests.Hosting).Source(source).Run();

    private static ServerFunctionGroup Group(Assembly assembly, string type) => (ServerFunctionGroup)assembly.GetType(type)!.GetProperty("Group")!.GetValue(null)!;

    private static ClaimsPrincipal SignedIn(string id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "cookie"));

    private static async Task<object> CallAsync(Type contract, object caller, string method, params object[] arguments)
    {
        var task = (Task)contract.GetMethod(method)!.Invoke(caller, arguments)!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aioros.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private sealed class Load(ClaimsPrincipal user) : IDataKeys, ICallerIdentity
    {
        public ClaimsPrincipal User { get; } = user;

        public void Read(string key)
        {
        }

        public void Invalidate(string key)
        {
        }
    }
}
