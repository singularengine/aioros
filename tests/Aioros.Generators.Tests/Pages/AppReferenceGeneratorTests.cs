namespace Aioros.Generators.Tests.Pages;

public sealed class AppReferenceGeneratorTests
{
    private const string App = """
        namespace Aioros.Hosting
        {
            public sealed record AiorosApp();

            public static class AiorosHost
            {
                public static AiorosApp CreateBuilder(string[] args, AiorosApp app) => app;
            }
        }

        namespace Aioros.Generated
        {
            internal static class AiorosPages;

            public static class AiorosFunctions;

            public static class AiorosNavigators;
        }
        """;

    private static readonly string[] Registries = ["class AiorosApplication", "class AiorosPages", "class AiorosFunctions", "class AiorosNavigators", "class AiorosAccessors"];

    [Fact]
    [Trait("AC", "GENERATOR-400")]
    public void AProjectThatOnlyReferencesAnAppGeneratesNoneOfItsRegistries()
    {
        var tests = new GeneratorHarness().Library("Shop", App).Source("public static class Probe { public static System.Type Functions => typeof(Aioros.Generated.AiorosFunctions); }").Run();
        var second = new GeneratorHarness().Library("Shop", App).Xaml("Pages/Page.axaml", "<TextBlock />").Run();
        var library = new GeneratorHarness().Source("public static class Probe;").Run();

        Assert.Empty(tests.WarningsAsErrors());
        Assert.All(Registries, registry => Assert.DoesNotContain(tests.Sources, source => source.Contains(registry, StringComparison.Ordinal)));
        Assert.Contains(second.Sources, source => source.Contains("class AiorosPages", StringComparison.Ordinal));
        Assert.Contains(library.Sources, source => source.Contains("class AiorosPages", StringComparison.Ordinal));
    }
}
