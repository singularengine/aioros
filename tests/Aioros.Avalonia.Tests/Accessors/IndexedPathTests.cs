using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;

namespace Aioros.Avalonia.Tests.Accessors;

public sealed class IndexedPathTests(EngineFixture fixture)
{
    private static readonly string[] Paths =
    [
        "Items[1].Name", "Array[1].Name", "Frozen[1].Name", "Ranking[1].Name", "Ranking[0]", "Grid[1][0]", "Items[5].Name", "Items[2]", "Label[0]", "Label.Length",
    ];

    private static void Register() =>
        MemberAccessors.Register(
        [
            new(typeof(Ranking), "[0]", static target => ((Ranking)target).Count > 0 ? ((Ranking)target)[0] : null),
            new(typeof(Ranking), "[1]", static target => ((Ranking)target).Count > 1 ? ((Ranking)target)[1] : null),
        ]);

    [Fact]
    [Trait("AC", "RENDERING-29")]
    public void IndexedMemberPathsReadAndWriteTheElementAtTheirIndex()
    {
        Register();
        var generated = new MemberPaths(AdapterSlotResolver.Fallback);
        var runtime = new MemberPaths(AdapterSlotResolver.Fallback, generated: false);
        var shelf = new Bookshelf("Dune", "Emma");

        var read = Paths.Select(path => (generated.Getter(path)(shelf), runtime.Getter(path)(shelf))).ToList();
        var outer = OuterScopes.Swap(ImmutableStack.Create<object?>(shelf));
        var fromOuter = generated.Getter("^1.Items[0].Name")(null);
        OuterScopes.Swap(outer);
        generated.Setter("Items[1].Name")(shelf, "Persuasion");
        runtime.Setter("Array[0].Name")(shelf, "Beloved");
        generated.Setter("Items[0]")(shelf, new Book("Middlemarch"));
        runtime.Setter("Items[7].Name")(shelf, "Nowhere");
        generated.Setter("Items[7]")(shelf, new Book("Nowhere"));

        Assert.All(read, pair => Assert.Equal(pair.Item1 is Book book ? book.Name : pair.Item1, pair.Item2 is Book other ? other.Name : pair.Item2));
        Assert.Equal(["Emma", "Emma (array)", "Emma (frozen)", "Emma (ranked)", "Dune (ranked)", 3, null, null, null, 5], read.Select(pair => pair.Item1 is Book book ? book.Name : pair.Item1));
        Assert.Equal("Dune", fromOuter);
        Assert.Equal(["Middlemarch", "Persuasion"], shelf.Items.Select(book => book.Name));
        Assert.Equal("Beloved", shelf.Array[0].Name);
    }

    [Fact]
    [Trait("AC", "RENDERING-30")]
    public async Task IndexedPlansReadTheSameThroughGeneratedAccessorsAndRuntimeDelegatesAsync()
    {
        Register();
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <TextBlock Text="{Binding Items[1].Name}" />
              <TextBlock Text="{Binding Ranking[0].Name}" />
            </StackPanel>
            """, new Bookshelf("Dune", "Emma"));
        var json = PlansFile.Write([new PlannedPage("/", "light", page)]).Json;

        var typed = PlansFile.Read(json, new PlanSlotResolver(AdapterSlotResolver.Fallback)).Single().Page.Plan;
        var runtime = PlansFile.Read(json, new PlanSlotResolver(AdapterSlotResolver.Fallback, generated: false)).Single().Page.Plan;
        var shelf = new Bookshelf("Ulysses", "Walden");
        var difference = PlanComparison.FirstDifference(typed, runtime, [shelf]);
        var body = EngineFixture.Body(page with { Plan = typed }, shelf);

        Assert.True(page.Reusable, page.Failure);
        Assert.Null(difference);
        Assert.Contains(">Walden<", body, StringComparison.Ordinal);
        Assert.Contains(">Ulysses (ranked)<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(MemberAccessors.Coverage().Runtime, member => member.StartsWith("System.Collections.Generic.List`1", StringComparison.Ordinal) && member.Contains(".[", StringComparison.Ordinal));
    }
}
