using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Aioros.Generators.Tests.Pages;
using Aioros.Runtime.Functions;

namespace Aioros.Generators.Tests.Functions;

public sealed class ServerFunctionGeneratorTests
{
    internal const string Hosting = """
        namespace Microsoft.AspNetCore.Authorization
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class AuthorizeAttribute : System.Attribute
            {
                public AuthorizeAttribute() { }
                public AuthorizeAttribute(string policy) => Policy = policy;
                public string? Policy { get; set; }
                public string? Roles { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public sealed class AllowAnonymousAttribute : System.Attribute { }
        }

        namespace Aioros.Hosting
        {
            public sealed class Builder { }

            public sealed record AiorosApp(
                System.Collections.Immutable.ImmutableArray<Aioros.Pages.PageDefinition> Pages, System.Func<System.Type>? ApplicationType, System.Reflection.Assembly Assembly,
                string ProjectDirectory, Aioros.Pages.PageDefinition? NotFound = null, Aioros.Pages.PageDefinition? Error = null)
            {
                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Functions.ServerFunctionGroup> Functions { get; init; } = [];

                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Pages.NavigationInterface> Navigators { get; init; } = [];


                public System.Collections.Immutable.ImmutableArray<Aioros.Runtime.Rendering.MemberAccessor> Accessors { get; init; } = [];
            }

            public static class AiorosHost
            {
                public static Builder CreateBuilder(string[] args, AiorosApp app) => new();
            }
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public static class AiorosFunctionRegistration
            {
                public static Aioros.Hosting.Builder AddServerFunction<TInterface, TImplementation>(this Aioros.Hosting.Builder builder, string? group = null)
                    where TInterface : class
                    where TImplementation : class, TInterface => builder;
            }
        }
        """;

    private const string Orders = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Functions;

        namespace Shop;

        public interface IOrders
        {
            Task<Total> CreateAsync(string product, int quantity, CancellationToken cancellationToken);

            [ServerFunction("wipe")]
            Task ClearAsync();
        }

        public sealed record Total(string Product, int Quantity);

        public sealed class Orders : IOrders
        {
            public List<string> Log { get; } = [];

            public Task<Total> CreateAsync(string product, int quantity, CancellationToken cancellationToken) => Task.FromResult(new Total(product, quantity * 2));

            public Task ClearAsync()
            {
                Log.Add("cleared");
                return Task.CompletedTask;
            }
        }
        """;

    private const string Registered = """
        namespace Shop;

        public static class Composition
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IOrders, Orders>(builder, "orders");
        }
        """;

    [Fact]
    [Trait("AC", "FUNCTIONS-01")]
    public async Task ClientsCallTheServerOverHttpAsync()
    {
        var assembly = Build(Orders, Registered).Load();
        var server = new Recorder();
        var client = Activator.CreateInstance(assembly.GetType("Shop.OrdersClient")!, new HttpClient(server) { BaseAddress = new Uri("http://app.test") })!;
        var contract = (string)assembly.GetType("Shop.OrdersClient")!.GetField("Contract", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var orders = assembly.GetType("Shop.IOrders")!;

        var created = (Task)orders.GetMethod("CreateAsync")!.Invoke(client, ["pen", 2, CancellationToken.None])!;
        await created;
        var total = created.GetType().GetProperty("Result")!.GetValue(created)!;
        await (Task)orders.GetMethod("ClearAsync")!.Invoke(client, [])!;

        Assert.Equal(("POST", "/_sf/orders/create", "1", contract), server.Requests[0].Head);
        Assert.Equal("{\"product\":\"pen\",\"quantity\":2}", server.Requests[0].Body);
        Assert.Equal("Total { Product = pen, Quantity = 4 }", total.ToString());
        Assert.Equal(("POST", "/_sf/orders/wipe", "1", contract), server.Requests[1].Head);
        Assert.Equal("{}", server.Requests[1].Body);
        Assert.Matches("^[0-9a-f]{16}$", contract);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-02")]
    public async Task GroupsRunTheImplementationInProcessAsync()
    {
        var result = Build(Orders, Registered);
        var assembly = result.Load();
        var group = Group(assembly, "Shop.OrdersFunctions");
        var service = Activator.CreateInstance(assembly.GetType("Shop.Orders")!)!;
        using var arguments = JsonDocument.Parse("{\"product\":\"ink\",\"quantity\":3}");

        var created = await group.Routes.Single(route => route.Name == "create").Invoke(service, arguments.RootElement, CancellationToken.None);
        var cleared = await group.Routes.Single(route => route.Name == "wipe").Invoke(service, arguments.RootElement, CancellationToken.None);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(("orders", "Shop.IOrders"), (group.Group, group.Interface.FullName));
        Assert.Equal("Total { Product = ink, Quantity = 6 }", created?.ToString());
        Assert.Null(cleared);
        Assert.Equal(["cleared"], (List<string>)service.GetType().GetProperty("Log")!.GetValue(service)!);
        Assert.Equal(group.Contract, Group(Build(Orders, Registered).Load(), "Shop.OrdersFunctions").Contract);
        Assert.NotEqual(group.Contract, Group(Build(Orders.Replace("int quantity,", "int quantity, bool gift,", StringComparison.Ordinal), Registered).Load(), "Shop.OrdersFunctions").Contract);
        Assert.NotEqual(group.Contract, Group(Build(Orders.Replace("record Total(string Product, int Quantity)", "record Total(string Product, int Count)", StringComparison.Ordinal), Registered).Load(), "Shop.OrdersFunctions").Contract);
        Assert.NotEqual(group.Contract, Group(Build(Orders.Replace("record Total(string Product, int Quantity)", "record Total(string Product, long Quantity)", StringComparison.Ordinal), Registered).Load(), "Shop.OrdersFunctions").Contract);
        Assert.Single(Registry(assembly));
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-07")]
    public void TheServerFunctionsAttributeStillDeclaresAGroup()
    {
        var marked = Orders
            .Replace("public interface IOrders", "[ServerFunctions(\"orders\")]\npublic interface IOrders", StringComparison.Ordinal)
            .Replace("[ServerFunction(\"wipe\")]", "[ServerFunction(\"wipe\")]\n    [Microsoft.AspNetCore.Authorization.Authorize(\"admins\")]", StringComparison.Ordinal);
        var attributed = Build(marked);
        var legacy = Group(attributed.Load(), "Shop.OrdersFunctions");
        var registered = Group(Build(Orders, Registered).Load(), "Shop.OrdersFunctions");
        var both = Group(Build(marked.Replace("[ServerFunctions(\"orders\")]", "[ServerFunctions(\"legacy\")]", StringComparison.Ordinal), Registered).Load(), "Shop.OrdersFunctions");

        Assert.Empty(attributed.Diagnostics);
        Assert.Equal((registered.Group, registered.Contract), (legacy.Group, legacy.Contract));
        Assert.Equal(registered.Routes.Select(route => route.Name), legacy.Routes.Select(route => route.Name));
        Assert.Empty(legacy.Routes.Single(route => route.Name == "create").Access);
        Assert.Equal([new ServerFunctionAccess("admins", null)], legacy.Routes.Single(route => route.Name == "wipe").Access);
        Assert.Equal(("orders", registered.Contract), (both.Group, both.Contract));
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-08")]
    public void GroupsDefaultToTheInterfaceName()
    {
        var result = Build("""
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            namespace Board;
            public interface IBoardFunctions { Task<int> MoveAsync(string item); }
            public sealed class BoardFunctions : IBoardFunctions { public Task<int> MoveAsync(string item) => Task.FromResult(item.Length); }
            public interface IOrderBook { Task OpenAsync(); }
            public sealed class OrderBook : IOrderBook { public Task OpenAsync() => Task.CompletedTask; }
            public interface INotRegistered { Task OpenAsync(); }
            public sealed class NotRegistered : INotRegistered { public Task OpenAsync() => Task.CompletedTask; }
            public static class Other { public static void AddServerFunction<TInterface, TImplementation>(this string text) { } }
            public static class BoardSetup
            {
                public static Aioros.Hosting.Builder AddBoard(this Aioros.Hosting.Builder builder)
                {
                    builder.AddServerFunction<IBoardFunctions, BoardFunctions>();
                    "text".AddServerFunction<INotRegistered, NotRegistered>();
                    return builder.AddServerFunction<IBoardFunctions, BoardFunctions>();
                }
            }
            """, """
            using Microsoft.AspNetCore.Builder;
            namespace Board;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = AiorosApplication.CreateBuilder(args);
                    builder.AddBoard();
                    builder.AddServerFunction<IOrderBook, OrderBook>();
                }
            }
            """);
        var assembly = result.Load();

        Assert.Empty(result.Diagnostics);
        Assert.Equal(["board", "orderBook"], Registry(assembly).Select(group => group.Group).Order(StringComparer.Ordinal));
        Assert.Equal("/_sf/board/move", ServerFunctionGroup.Path(Group(assembly, "Board.BoardFunctionsFunctions").Group, "move"));
        Assert.Null(assembly.GetType("Board.NotRegisteredFunctions"));
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-09")]
    public void AccessRulesComeFromTheImplementation()
    {
        var result = Build("""
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Authorization;
            namespace Shop;
            public interface IStore { Task BrowseAsync(); Task BuyAsync(int id); Task RefundAsync(int id); }
            public interface IPublicStore { Task BrowseAsync(); Task BuyAsync(int id); }
            public sealed class Store : IStore
            {
                public Task BrowseAsync() => Task.CompletedTask;
                [Authorize("admins", Roles = "owner")] public Task BuyAsync(int id) => Task.CompletedTask;
                [Authorize] Task IStore.RefundAsync(int id) => Task.CompletedTask;
            }
            [Authorize]
            public sealed class PublicStore : IPublicStore
            {
                [AllowAnonymous] public Task BrowseAsync() => Task.CompletedTask;
                [Authorize(Roles = "buyer")] public Task BuyAsync(int id) => Task.CompletedTask;
            }
            public static class Setup
            {
                public static void Compose(Aioros.Hosting.Builder builder)
                {
                    Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IStore, Store>(builder);
                    Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IPublicStore, PublicStore>(builder);
                }
            }
            """);
        var assembly = result.Load();
        var store = Group(assembly, "Shop.StoreFunctions").Routes.ToDictionary(route => route.Name, route => route.Access);
        var open = Group(assembly, "Shop.PublicStoreFunctions").Routes.ToDictionary(route => route.Name, route => route.Access);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(store["browse"]);
        Assert.Equal([new ServerFunctionAccess("admins", "owner")], store["buy"]);
        Assert.Equal([new ServerFunctionAccess(null, null)], store["refund"]);
        Assert.Empty(open["browse"]);
        Assert.Equal([new ServerFunctionAccess(null, null), new ServerFunctionAccess(null, "buyer")], open["buy"]);
    }

    [Fact]
    [Trait("AC", "GENERATOR-25")]
    public void RegistrationsOfUnusableTypesAreReported()
    {
        var result = Build("""
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            namespace Shop;
            public class Plain { public Task RunAsync() => Task.CompletedTask; }
            public interface IRepository<T> { Task<T> FindAsync(int id); }
            public sealed class Repository : IRepository<int> { public Task<int> FindAsync(int id) => Task.FromResult(id); }
            public interface IConverter { Task<T> ConvertAsync<T>(string text); }
            public sealed class Converter : IConverter { public Task<T> ConvertAsync<T>(string text) => Task.FromResult(default(T)!); }
            public static class Setup
            {
                public static void Compose(Aioros.Hosting.Builder builder)
                {
                    builder.AddServerFunction<Plain, Plain>();
                    builder.AddServerFunction<IRepository<int>, Repository>();
                    builder.AddServerFunction<IConverter, Converter>();
                }
            }
            """);

        var reported = result.Diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS009").Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(3, reported.Count);
        Assert.Contains(reported, message => message.Contains("'Shop.Plain'", StringComparison.Ordinal) && message.Contains("not an interface", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Shop.IRepository<int>'", StringComparison.Ordinal) && message.Contains("generic", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Shop.IConverter'", StringComparison.Ordinal) && message.Contains("ConvertAsync", StringComparison.Ordinal));
        Assert.Empty(Registry(result.Load()));
    }

    [Fact]
    [Trait("AC", "GENERATOR-26")]
    public void GroupsThatAreNotConstantOrCollideAreReported()
    {
        var result = Build("""
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            namespace Shop;
            public interface IOrders { Task RunAsync(); }
            public sealed class Orders : IOrders { public Task RunAsync() => Task.CompletedTask; }
            public interface ICarts { Task RunAsync(); }
            public sealed class Carts : ICarts { public Task RunAsync() => Task.CompletedTask; }
            public interface IStock { Task RunAsync(); }
            public sealed class Stock : IStock { public Task RunAsync() => Task.CompletedTask; }
            public static class Setup
            {
                public static void Compose(Aioros.Hosting.Builder builder, string name)
                {
                    builder.AddServerFunction<IOrders, Orders>("orders");
                    builder.AddServerFunction<IOrders, Orders>("sales");
                    builder.AddServerFunction<ICarts, Carts>("stock");
                    builder.AddServerFunction<IStock, Stock>();
                    builder.AddServerFunction<IStock, Stock>(name);
                }
            }
            """);

        var reported = result.Diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS009").Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(3, reported.Count);
        Assert.Contains(reported, message => message.Contains("'Shop.IOrders'", StringComparison.Ordinal) && message.Contains("'orders' and 'sales'", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Shop.IStock'", StringComparison.Ordinal) && message.Contains("constant", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'stock'", StringComparison.Ordinal) && message.Contains("'Shop.ICarts'", StringComparison.Ordinal) && message.Contains("'Shop.IStock'", StringComparison.Ordinal));
    }

    private static readonly int[] Values = [1, 2, 3];

    private const string Accounts = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;
        using System.Threading;
        using System.Threading.Tasks;
        namespace Shop;
        public interface IAccounts
        {
            Task<string> OpenAsync(string kind);
            Task CloseAsync(string kind, CancellationToken cancellationToken);
        }
        public sealed class Accounts : IAccounts
        {
            public Task<string> OpenAsync(string kind) => kind switch
            {
                "ok" => Task.FromResult("opened"),
                "validation" => throw new ValidationException(new ValidationResult("A name is required.", ["Name"]), null, null),
                "missing" => throw new KeyNotFoundException("No such account."),
                "refused" => throw new UnauthorizedAccessException("Owners only."),
                "cancelled" => throw new OperationCanceledException(),
                "late" => LateAsync(),
                _ => throw new ArgumentException("secret detail"),
            };
            public Task CloseAsync(string kind, CancellationToken cancellationToken) => OpenAsync(kind);
            private static async Task<string> LateAsync()
            {
                await Task.Yield();
                throw new InvalidOperationException("secret detail");
            }
        }
        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IAccounts, Accounts>(builder);
        }
        """;

    private const string Catalog = """
        using System.Collections.Generic;
        using System.Text.Json.Serialization;
        using System.Threading.Tasks;
        namespace Shop;
        public enum Size { Small, Large = 3 }
        [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
        [JsonDerivedType(typeof(Pen), "pen")]
        [JsonDerivedType(typeof(Ink), "ink")]
        public abstract record Product(string Name);
        public sealed record Pen(string Name, string Color) : Product(Name);
        public sealed record Ink(string Name, int Milliliters) : Product(Name);
        public sealed record Basket(IReadOnlyList<Product> Items, Dictionary<string, int> Counts, Size Size, decimal? Discount, string? Note);
        public interface ICatalog
        {
            Task<Basket> FillAsync(Basket basket, Size size, int? limit, string? coupon);
            Task<Product?> FindAsync(string name);
            Task<int> CountAsync(int[] values);
        }
        public sealed class Catalog : ICatalog
        {
            public Task<Basket> FillAsync(Basket basket, Size size, int? limit, string? coupon) =>
                Task.FromResult(basket with { Items = [.. basket.Items, new Ink("blue", limit ?? 30)], Size = size, Note = coupon ?? "none" });
            public Task<Product?> FindAsync(string name) => Task.FromResult<Product?>(name == "pen" ? new Pen("pen", "red") : null);
            public Task<int> CountAsync(int[] values) => Task.FromResult(values.Length);
        }
        public static class Setup
        {
            public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<ICatalog, Catalog>(builder);
        }
        """;

    [Fact]
    [Trait("AC", "FUNCTIONS-12")]
    public async Task TheInProcessCallerFailsAsTheClientWouldAsync()
    {
        var result = Build(Accounts);
        var assembly = result.Load();
        var group = Group(assembly, "Shop.AccountsFunctions");
        var accounts = assembly.GetType("Shop.IAccounts")!;
        var caller = group.InProcess!(Activator.CreateInstance(assembly.GetType("Shop.Accounts")!)!);
        async Task<Exception?> OpenAsync(string kind) => await Record.ExceptionAsync(() => (Task)accounts.GetMethod("OpenAsync")!.Invoke(caller, [kind])!);
        async Task<Exception?> CloseAsync(string kind) => await Record.ExceptionAsync(() => (Task)accounts.GetMethod("CloseAsync")!.Invoke(caller, [kind, CancellationToken.None])!);

        var opened = (Task<string>)accounts.GetMethod("OpenAsync")!.Invoke(caller, ["ok"])!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal(("AccountsInProcess", "opened"), (caller.GetType().Name, await opened));
        Assert.Equal(["Name"], Assert.IsType<System.ComponentModel.DataAnnotations.ValidationException>(await OpenAsync("validation")).ValidationResult.MemberNames);
        Assert.Equal("No such account.", Assert.IsType<KeyNotFoundException>(await OpenAsync("missing")).Message);
        Assert.Equal("Owners only.", Assert.IsType<UnauthorizedAccessException>(await CloseAsync("refused")).Message);
        Assert.IsType<OperationCanceledException>(await OpenAsync("cancelled"));
        foreach (var failure in new[] { await OpenAsync("other"), await OpenAsync("late"), await CloseAsync("other") })
        {
            var failed = Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal("The server function failed.", failed.Message);
            Assert.Contains("secret detail", failed.InnerException!.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-14")]
    public async Task ArgumentsAndResultsUseEmittedCodeAsync()
    {
        var result = Build(Catalog);
        var assembly = result.Load();
        var group = Group(assembly, "Shop.CatalogFunctions");
        var service = Activator.CreateInstance(assembly.GetType("Shop.Catalog")!)!;
        var routes = group.Routes.ToDictionary(route => route.Name);
        using var fill = JsonDocument.Parse("""{"basket":{"items":[{"kind":"pen","name":"pen","color":"red"}],"counts":{"pen":1},"size":0,"discount":null,"note":null},"size":3}""");
        using var find = JsonDocument.Parse("""{"name":"nothing"}""");

        var filled = await routes["fill"].Invoke(service, fill.RootElement, CancellationToken.None);
        var missing = await routes["find"].Invoke(service, find.RootElement, CancellationToken.None);
        var server = new Recorder();
        var client = group.Client(new HttpClient(server) { BaseAddress = new Uri("http://app.test") });
        var catalog = assembly.GetType("Shop.ICatalog")!;
        var sent = (Task)catalog.GetMethod("FillAsync")!.Invoke(client, [filled, Enum.ToObject(assembly.GetType("Shop.Size")!, 3), 5, null])!;
        await sent;
        var found = (Task)catalog.GetMethod("FindAsync")!.Invoke(client, ["nothing"])!;
        await found;
        var counted = (Task<int>)catalog.GetMethod("CountAsync")!.Invoke(client, [Values])!;

        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            """{"items":[{"kind":"pen","name":"pen","color":"red"},{"kind":"ink","name":"blue","milliliters":30}],"counts":{"pen":1},"size":3,"discount":null,"note":"none"}""",
            Json(routes["fill"].Write!, filled));
        Assert.Null(missing);
        Assert.Equal("null", Json(routes["find"].Write!, missing));
        Assert.Equal(
            """{"basket":{"items":[{"kind":"pen","name":"pen","color":"red"},{"kind":"ink","name":"blue","milliliters":30}],"counts":{"pen":1},"size":3,"discount":null,"note":"none"},"size":3,"limit":5,"coupon":null}""",
            server.Requests[0].Body);
        Assert.Equal(Json(routes["fill"].Write!, filled), Json(routes["fill"].Write!, sent.GetType().GetProperty("Result")!.GetValue(sent)));
        Assert.Null(found.GetType().GetProperty("Result")!.GetValue(found));
        Assert.Equal(("""{"values":[1,2,3]}""", 3), (server.Requests[2].Body, await counted));
        var generated = string.Join("\n", result.Sources.Where(source => source.StartsWith("// <auto-generated/>", StringComparison.Ordinal)));
        Assert.Contains("AiorosFunctionData.R", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFromJson", generated, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-15")]
    public void FunctionDataTheGeneratedCodeCannotSendIsReported()
    {
        var result = Build("""
            using System.Threading.Tasks;
            namespace Shop;
            public abstract record Shape(string Name);
            public sealed record Drawing(Shape Shape);
            public interface IDrawings { Task<Drawing> DrawAsync(string name); Task SaveAsync(object drawing); Task<int> CountAsync(); }
            public sealed class Drawings : IDrawings
            {
                public Task<Drawing> DrawAsync(string name) => Task.FromResult(new Drawing(null!));
                public Task SaveAsync(object drawing) => Task.CompletedTask;
                public Task<int> CountAsync() => Task.FromResult(0);
            }
            public static class Setup
            {
                public static void Compose(Aioros.Hosting.Builder builder) => Microsoft.AspNetCore.Builder.AiorosFunctionRegistration.AddServerFunction<IDrawings, Drawings>(builder);
            }
            """);

        var reported = result.Diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS015").Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(2, reported.Count);
        Assert.Contains(reported, message => message.Contains("'Shop.IDrawings.DrawAsync'", StringComparison.Ordinal) && message.Contains("'Drawing.Shape' is 'Shop.Shape', an abstract type", StringComparison.Ordinal) && message.Contains("[JsonDerivedType", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Shop.IDrawings.SaveAsync'", StringComparison.Ordinal) && message.Contains("'object'", StringComparison.Ordinal));
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("AIOROS015", diagnostic.Id));
    }

    private static string Json(Action<Utf8JsonWriter, object?> write, object? value)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer, value);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static GeneratorResult Build(params string[] sources)
    {
        var harness = new GeneratorHarness().Source(Hosting);
        foreach (var source in sources)
        {
            harness.Source(source);
        }

        return harness.Run();
    }

    private static ServerFunctionGroup Group(Assembly assembly, string type) =>
        (ServerFunctionGroup)assembly.GetType(type)!.GetProperty("Group")!.GetValue(null)!;

    private static ImmutableArray<ServerFunctionGroup> Registry(Assembly assembly) =>
        (ImmutableArray<ServerFunctionGroup>)assembly.GetType("Aioros.Generated.AiorosFunctions")!.GetProperty("All")!.GetValue(null)!;

    private sealed class Recorder : HttpMessageHandler
    {
        private static string Fill(string body)
        {
            using var sent = JsonDocument.Parse(body);
            return sent.RootElement.GetProperty("basket").GetRawText();
        }

        public List<((string, string, string, string) Head, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(((request.Method.Method, request.RequestUri!.AbsolutePath, request.Headers.GetValues("X-Aioros-Fn").Single(), request.Headers.GetValues("X-Aioros-Contract").Single()), body));
            var path = request.RequestUri.AbsolutePath;
            var answer = path.EndsWith("/create", StringComparison.Ordinal) ? "{\"product\":\"pen\",\"quantity\":4}"
                : path.EndsWith("/fill", StringComparison.Ordinal) ? Fill(body)
                : path.EndsWith("/find", StringComparison.Ordinal) ? "null"
                : path.EndsWith("/count", StringComparison.Ordinal) ? "3"
                : null;
            return answer is null
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }
}
