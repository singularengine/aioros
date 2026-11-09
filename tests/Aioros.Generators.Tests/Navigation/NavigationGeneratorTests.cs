using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Aioros.Generators.Tests.Pages;
using Aioros.Navigation;
using Aioros.Runtime.Pages;

namespace Aioros.Generators.Tests.Navigation;

public sealed class NavigationGeneratorTests
{
    private const string Hosting = """
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
            public static class AiorosNavigationRegistration
            {
                public static Aioros.Hosting.Builder AddNavigation<TInterface>(this Aioros.Hosting.Builder builder)
                    where TInterface : class => builder;
            }
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-35")]
    public async Task RegisteredInterfacesNavigateThroughTheNavigatorAsync()
    {
        var result = Build("""
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Hosting;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            using Aioros.Runtime.Pages;
            namespace Shop;
            public interface IHome { Task HomeAsync(string path); }
            public interface IShopNavigator : IHome
            {
                Task GoToAsync(string path);
                ValueTask OpenAsync(string target, CancellationToken cancellationToken);
                void Show(string path);
            }
            public interface IAccountNavigator { Task GoToAsync(string path); }
            public interface IModuleNavigator { ValueTask GoToAsync(string path); }
            public interface INotRegistered { Task GoToAsync(string path); }
            public static class Setup
            {
                public static Builder AddShop(this Builder builder)
                {
                    builder.AddNavigation<IShopNavigator>();
                    return builder.AddNavigation<IShopNavigator>();
                }

                public static IServiceCollection AddModule(IServiceCollection services) =>
                    services.AddNavigation<IModuleNavigator>(Aioros.Generated.AiorosNavigators.All);
            }
            """, """
            using Aioros.Hosting;
            using Microsoft.AspNetCore.Builder;
            namespace Shop;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = AiorosApplication.CreateBuilder(args);
                    builder.AddShop();
                    builder.AddNavigation<IAccountNavigator>();
                }
            }
            """);
        var assembly = result.Load();
        var all = Registry(assembly);
        var shop = all.Single(entry => entry.Interface.Name == "IShopNavigator");
        var navigator = new Recorder();
        var instance = shop.Create(navigator);
        using var source = new CancellationTokenSource();

        await (Task)Method(assembly, "Shop.IHome", "HomeAsync").Invoke(instance, ["/"])!;
        await (Task)Method(assembly, "Shop.IShopNavigator", "GoToAsync").Invoke(instance, ["/games/7"])!;
        await (ValueTask)Method(assembly, "Shop.IShopNavigator", "OpenAsync").Invoke(instance, ["cart", source.Token])!;
        Method(assembly, "Shop.IShopNavigator", "Show").Invoke(instance, ["/login"]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(["IAccountNavigator", "IModuleNavigator", "IShopNavigator"], all.Select(entry => entry.Interface.Name).Order(StringComparer.Ordinal));
        Assert.True(instance.GetType().IsSealed);
        Assert.Equal([("/", CancellationToken.None), ("/games/7", CancellationToken.None), ("cart", source.Token), ("/login", CancellationToken.None)], navigator.Requests);
        Assert.IsAssignableFrom(assembly.GetType("Shop.IModuleNavigator")!, all.Single(entry => entry.Interface.Name == "IModuleNavigator").Create(navigator));
        Assert.DoesNotContain(assembly.GetTypes(), type => type.GetInterfaces().Any(face => face.Name == "INotRegistered"));
    }

    [Fact]
    [Trait("AC", "GENERATOR-36")]
    public void InterfacesThatCannotNavigateAreReported()
    {
        var result = Build("""
            using System.Threading;
            using System.Threading.Tasks;
            using Aioros.Hosting;
            using Microsoft.AspNetCore.Builder;
            namespace Shop;
            public class Plain { public Task GoToAsync(string path) => Task.CompletedTask; }
            public interface IRouter<T> { Task GoToAsync(string path); }
            public static class Outer { private interface IHidden { Task GoToAsync(string path); } public static void Compose(Builder builder) => builder.AddNavigation<IHidden>(); }
            public interface IWide
            {
                string Current { get; }
                event System.EventHandler Moved;
                Task GoToAsync<T>(string path);
                Task BackAsync();
                Task OpenAsync(int id);
                Task MoveAsync(string path, string query);
                Task<bool> TryAsync(string path);
                int Count(string path);
                Task FineAsync(string path, CancellationToken cancellationToken);
            }
            public static class Setup
            {
                public static void Compose(Builder builder)
                {
                    builder.AddNavigation<Plain>();
                    builder.AddNavigation<IRouter<int>>();
                    builder.AddNavigation<IWide>();
                }
            }
            """);

        var reported = result.Diagnostics.Where(diagnostic => diagnostic.Id == "AIOROS016").Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(11, reported.Count);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("AIOROS016", diagnostic.Id));
        Assert.Contains(reported, message => message.Contains("'Shop.Plain'", StringComparison.Ordinal) && message.Contains("not an interface", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Shop.IRouter<int>'", StringComparison.Ordinal) && message.Contains("generic interface", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Shop.Outer.IHidden'", StringComparison.Ordinal) && message.Contains("public or internal", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Current'", StringComparison.Ordinal) && message.Contains("only methods", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'Moved'", StringComparison.Ordinal) && message.Contains("only methods", StringComparison.Ordinal));
        Assert.Contains(reported, message => message.Contains("'GoToAsync'", StringComparison.Ordinal) && message.Contains("generic", StringComparison.Ordinal));
        Assert.Equal(3, reported.Count(message => message.Contains("must take the path as a string", StringComparison.Ordinal)));
        Assert.Contains(reported, message => message.Contains("'BackAsync'", StringComparison.Ordinal) && message.Contains("must take the path as a string", StringComparison.Ordinal));
        Assert.Equal(2, reported.Count(message => message.Contains("must return Task, ValueTask or void", StringComparison.Ordinal)));
        Assert.Contains(reported, message => message.Contains("'TryAsync'", StringComparison.Ordinal) && message.Contains("must return Task, ValueTask or void", StringComparison.Ordinal));
        Assert.DoesNotContain(reported, message => message.Contains("'FineAsync'", StringComparison.Ordinal));
        Assert.Empty(Registry(result.Load()));
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

    private static MethodInfo Method(Assembly assembly, string type, string name) => assembly.GetType(type)!.GetMethod(name)!;

    private static ImmutableArray<NavigationInterface> Registry(Assembly assembly) =>
        (ImmutableArray<NavigationInterface>)assembly.GetType("Aioros.Generated.AiorosNavigators")!.GetProperty("All")!.GetValue(null)!;

    private sealed class Recorder : INavigator
    {
        public List<(string Path, CancellationToken Token)> Requests { get; } = [];

        public ValueTask NavigateAsync(NavigationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Path, cancellationToken));
            return ValueTask.CompletedTask;
        }
    }
}
