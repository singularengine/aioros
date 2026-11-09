using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class TypedLoaderGeneratorTests
{
    private const string Models = """
        using System;
        using System.Collections.Generic;
        namespace App.Models;
        public enum Sort { Top, New, Price }
        public sealed class Calls { public List<int> Ids { get; } = []; }
        public sealed record ShopData(int Id, string? Q, int Page, Sort Sort, Guid? Owner, string? User, string Path, bool Token);
        public sealed record ListData(string Q);
        public sealed record ShellData(string Path);
        public sealed class ShopModel(ShopData data) { public ShopData Data { get; } = data; }
        public sealed class ListModel(ListData data) { public ListData Data { get; } = data; }
        public sealed class ShellModel(ShellData data) { public ShellData Data { get; } = data; }
        """;

    private const string ShopLoader = """
        using System;
        using System.Security.Claims;
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        using App.Models;
        namespace App.Loaders;
        internal sealed class ShopLoader(Calls calls)
        {
            public ValueTask<LoaderResult<ShopData>> LoadAsync(int id, string? q, CancellationToken cancellationToken, ClaimsPrincipal user, PageRequest request, int page = 1, Sort sort = Sort.Top, Guid? owner = null)
            {
                calls.Ids.Add(id);
                return ValueTask.FromResult(new LoaderResult<ShopData>(new ShopData(id, q, page, sort, owner, user.Identity?.Name, request.Path, cancellationToken.CanBeCanceled)));
            }
        }
        """;

    private const string ShellLoader = """
        using Aioros.Pages;
        namespace App.Loaders;
        public static class ShellLoader
        {
            public static App.Models.ShellData Load(PageRequest request) => new(request.Path);
        }
        """;

    private const string ListLoader = """
        using System.Threading.Tasks;
        namespace App.Loaders;
        public sealed class ListLoader
        {
            public async Task<App.Models.ListData> LoadAsync(string? q)
            {
                await Task.Yield();
                return new App.Models.ListData(q ?? "all");
            }
        }
        """;

    private const string OldLoader = """
        using Aioros.Pages;
        namespace App.Loaders;
        public static class OldLoader
        {
            public static LoaderOutcome Load() => LoaderResult.Redirect("/shop");
        }
        """;

    private static readonly Guid Owner = Guid.Parse("8f14e45f-ceea-467e-9a2b-1a2b3c4d5e6f");

    private static PageRequest Request(string id, params (string Name, string Value)[] query) =>
        new("/shop/" + id, ImmutableDictionary<string, string>.Empty.Add("id", id), query.ToImmutableDictionary(pair => pair.Name, pair => pair.Value))
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ada")], "test")),
        };

    private static GeneratorResult Shop() => new GeneratorHarness()
        .Source(Models)
        .Source(ShopLoader, "Pages/shop/[id]/Page.Loader.cs")
        .Source(ShellLoader, "Pages/Layout.Loader.cs")
        .Source(ListLoader, "Pages/shop/Page.Loader.cs")
        .Source(OldLoader, "Pages/old/Page.Loader.cs")
        .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel")
        .Xaml("Pages/shop/[id]/Page.axaml", "<TextBlock />", "ShopModel")
        .Xaml("Pages/shop/Page.axaml", "<TextBlock />", "ListModel")
        .Xaml("Pages/old/Page.axaml", "<TextBlock />")
        .Run();

    [Fact]
    [Trait("AC", "GENERATOR-61")]
    public async Task TypedLoaderParametersAreBoundByNameAsync()
    {
        var result = Shop();
        var pages = result.Pages(out var assembly);
        var shop = pages.Single(page => page.Route == "/shop/{id}");
        var list = pages.Single(page => page.Route == "/shop");
        var old = pages.Single(page => page.Route == "/old");
        var calls = Activator.CreateInstance(assembly.GetType("App.Models.Calls")!)!;
        using var cancellation = new CancellationTokenSource();

        var full = await shop.Loader!.LoadAsync(new Services(calls), Request("7", ("q", "red"), ("page", "3"), ("sort", "NEW"), ("owner", Owner.ToString())), cancellation.Token);
        var bare = await shop.Loader.LoadAsync(new Services(calls), Request("8"), TestContext.Current.CancellationToken);
        var shell = await shop.Layouts[0].Loader!.LoadAsync(new Services(), Request("8"), TestContext.Current.CancellationToken);
        var listed = await list.Loader!.LoadAsync(new Services(), Request("1", ("q", "blue")), TestContext.Current.CancellationToken);
        var moved = await old.Loader!.LoadAsync(new Services(), Request("1"), TestContext.Current.CancellationToken);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.WarningsAsErrors());
        Assert.Equal($"ShopData {{ Id = 7, Q = red, Page = 3, Sort = New, Owner = {Owner}, User = ada, Path = /shop/7, Token = True }}", full.Data!.ToString());
        Assert.Equal("ShopData { Id = 8, Q = , Page = 1, Sort = Top, Owner = , User = ada, Path = /shop/8, Token = True }", bare.Data!.ToString());
        Assert.Equal("ShopModel", shop.Loader.CreateModel(new Services(), full.Data)!.GetType().Name);
        Assert.Equal("ShellData { Path = /shop/8 }", shell.Data!.ToString());
        Assert.Equal("ListData { Q = blue }", listed.Data!.ToString());
        Assert.Equal(LoaderResult.Redirect("/shop"), moved);
        Assert.Null(old.Loader.CreateModel(new Services(), null));
        Assert.DoesNotContain(result.Sources, source => source.Contains("GetMethod(", StringComparison.Ordinal) || source.Contains("Invoke(", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-62")]
    public async Task UnconvertibleValuesAnswerNotFoundOrTheirDefaultAsync()
    {
        var shop = Shop().Pages(out var assembly).Single(page => page.Route == "/shop/{id}");
        var calls = Activator.CreateInstance(assembly.GetType("App.Models.Calls")!)!;

        var text = await shop.Loader!.LoadAsync(new Services(calls), Request("abc"), TestContext.Current.CancellationToken);
        var huge = await shop.Loader.LoadAsync(new Services(calls), Request("99999999999"), TestContext.Current.CancellationToken);
        var malformed = await shop.Loader.LoadAsync(new Services(calls), Request("5", ("q", ""), ("page", "abc"), ("sort", "3"), ("owner", "x")), TestContext.Current.CancellationToken);
        var unknown = await shop.Loader.LoadAsync(new Services(calls), Request("6", ("page", ""), ("sort", "nope")), TestContext.Current.CancellationToken);

        Assert.True(text.NotFound);
        Assert.True(huge.NotFound);
        Assert.Equal("ShopData { Id = 5, Q = , Page = 1, Sort = Top, Owner = , User = ada, Path = /shop/5, Token = True }", malformed.Data!.ToString());
        Assert.Equal("ShopData { Id = 6, Q = , Page = 1, Sort = Top, Owner = , User = ada, Path = /shop/6, Token = True }", unknown.Data!.ToString());
        Assert.Equal([5, 6], (List<int>)calls.GetType().GetProperty("Ids")!.GetValue(calls)!);
    }

    [Fact]
    [Trait("AC", "GENERATOR-63")]
    public void LoaderParametersThatCannotBeBoundAreBuildErrors()
    {
        static string Loader(string name, string members) => $$"""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            namespace App.Loaders;
            public sealed class {{name}}
            {
                {{members}}
            }
            """;
        var harness = new GeneratorHarness().Source("namespace App.Models; public sealed record Data(int Id);");
        var loaders = new (string Folder, string Members)[]
        {
            ("required", "public App.Models.Data Load(int id) => new(id);"),
            ("list", "public App.Models.Data Load(List<int>? ids) => new(0);"),
            ("byref", "public App.Models.Data Load(ref int page) => new(page);"),
            ("twice", "public App.Models.Data Load() => new(0); public Task<App.Models.Data> LoadAsync() => Task.FromResult(new App.Models.Data(1));"),
            ("nothing", "public Task LoadAsync() => Task.CompletedTask;"),
            ("generic", "public App.Models.Data Load<T>() => new(0);"),
        };
        foreach (var (folder, members) in loaders)
        {
            harness.Source(Loader(char.ToUpperInvariant(folder[0]) + folder[1..] + "Loader", members), $"Pages/{folder}/Page.Loader.cs").Xaml($"Pages/{folder}/Page.axaml", "<TextBlock />");
        }

        var result = harness.Run();
        var messages = result.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(Enumerable.Repeat("AIOROS025", 6), result.Diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.Severity));
        Assert.Contains(messages, message => message.Contains("'App.Loaders.RequiredLoader'", StringComparison.Ordinal) && message.Contains("'id'", StringComparison.Ordinal) && message.Contains("'int? id'", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'App.Loaders.ListLoader'", StringComparison.Ordinal) && message.Contains("converted from text", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'App.Loaders.ByrefLoader'", StringComparison.Ordinal) && message.Contains("'page'", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'App.Loaders.TwiceLoader'", StringComparison.Ordinal) && message.Contains("keep one", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'App.Loaders.NothingLoader'", StringComparison.Ordinal) && message.Contains("no result", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'App.Loaders.GenericLoader'", StringComparison.Ordinal) && message.Contains("generic", StringComparison.Ordinal));
        Assert.Empty(result.WarningsAsErrors());
        Assert.All(result.Pages(out _), page => Assert.Null(page.Loader));
    }

    [Fact]
    [Trait("AC", "GENERATOR-64")]
    public void LoaderResultsWithATitleAreOneCall()
    {
        var metadata = new PageMetadata("A shop.");

        var titled = LoaderResult.With(42, "Shop", metadata);
        var found = metadata.Description is null ? LoaderResult.NotFound() : LoaderResult.With("shoes", "Shoes");

        Assert.Equal(new LoaderResult<int>(42) { Title = "Shop", Metadata = metadata }, titled);
        Assert.Equal(new LoaderResult<string>("shoes") { Title = "Shoes" }, found);
        Assert.False(found.NotFound);
        Assert.Null(found.RedirectTo);
    }
}
