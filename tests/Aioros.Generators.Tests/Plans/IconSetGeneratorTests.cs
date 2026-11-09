using System.Collections.Immutable;
using Aioros.Generators.Tests.Pages;

namespace Aioros.Generators.Tests.Plans;

public sealed class IconSetGeneratorTests
{
    private const string Library = """
        using System.Collections.Generic;
        using Aioros.Icons;

        [assembly: IconSet<Shapes.Lucide>]

        namespace Shapes;

        public sealed class Lucide : IIconSet
        {
            public static IEnumerable<KeyValuePair<string, string>> Icons { get; } = [new("git-branch", "<path d=\"M6 3v12\"/>")];
        }
        """;

    private const string App = """
        using System.Collections.Generic;
        using Aioros.Icons;

        [assembly: IconSet<App.Brand>]

        namespace App;

        public sealed class Brand : IIconSet
        {
            public static IEnumerable<KeyValuePair<string, string>> Icons { get; } = [new("logo", "<circle cx=\"12\" cy=\"12\" r=\"4\"/>")];
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-115")]
    public void DeclaredIconSetsOfTheAppAndItsLibrariesAreRegistered()
    {
        var assembly = new GeneratorHarness().Library("Shapes", Library).Source(App).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var all = (ImmutableArray<KeyValuePair<string, string>>)assembly.GetType("Aioros.Generated.AiorosIcons")!.GetProperty("All")!.GetValue(null)!;

        Assert.Equal(["logo", "git-branch"], all.Select(icon => icon.Key));
        Assert.Equal("<path d=\"M6 3v12\"/>", all.Single(icon => icon.Key == "git-branch").Value);
    }

    private const string OwnDeclaration = """
        using System;
        using System.Collections.Generic;

        [assembly: Aioros.Icons.IconSetAttribute<Kit.Glyphs>]

        namespace Aioros.Icons
        {
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            internal sealed class IconSetAttribute<T> : Attribute;
        }

        namespace Kit
        {
            public sealed class Glyphs
            {
                public static IEnumerable<KeyValuePair<string, string>> Icons { get; } = [new("loader-circle", "<path d=\"M21 12a9 9 0 1 1-6.219-8.56\"/>")];
            }
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-116")]
    public void LibrariesWithoutTheRuntimeDeclareIconSets()
    {
        var assembly = new GeneratorHarness().Library("Shapes", Library, references: Libraries.Abstractions).Library("Kit", OwnDeclaration, references: Libraries.Framework).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var all = (ImmutableArray<KeyValuePair<string, string>>)assembly.GetType("Aioros.Generated.AiorosIcons")!.GetProperty("All")!.GetValue(null)!;

        Assert.Equal(["loader-circle", "git-branch"], all.Select(icon => icon.Key));
    }
}
