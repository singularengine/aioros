using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class IndexerBindingTests(EngineFixture fixture)
{
    private const string Reflected = """
        <StackPanel>
          <Border DataContext="{Binding Items[0]}"><TextBlock Text="{Binding Name}" /></Border>
          <TextBlock Text="{ReflectionBinding Items[1].Name}" />
          <TextBlock Text="{Binding Array[1].Name}" />
          <TextBlock Text="{Binding Ranking[0].Name}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-241")]
    public async Task CompiledIntegerIndexersArePlannedAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("<m:Indexed />", new Bookshelf("Dune", "Emma"), new Bookshelf("Ulysses", "Walden", "Hamlet"));
        var json = PlansFile.Write([new PlannedPage("/", "light", page)]).Json;

        Assert.Contains("Items[0]", json, StringComparison.Ordinal);
        Assert.Contains("Items[1].Name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.Item", json, StringComparison.Ordinal);
        Assert.Contains(">Walden<", EngineFixture.Body(page, new Bookshelf("Ulysses", "Walden")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-307")]
    public async Task PrecompiledIndexersAreDescribedWithTheirIndexesAsync()
    {
        var page = await fixture.CompileAsync("<m:IndexedColumns />", new PaymentTable());
        var fields = PlanForms.Fields(page.Plan, [new PaymentTable()]);
        var token = System.Text.RegularExpressions.Regex.Match(EngineFixture.Body(page, new PaymentTable()), "value=\"([0-9]+)\\|\" role=\"menuitemcheckbox\"").Groups[1].Value;
        var unchecking = new PaymentTable();
        var pressing = new PaymentTable();

        await PlanForms.ApplyAsync(page.Plan, [unchecking], System.Collections.Immutable.ImmutableDictionary<string, string>.Empty, new Plans.AdapterSlotResolver());
        await PlanForms.ApplyAsync(page.Plan, [pressing], System.Collections.Immutable.ImmutableDictionary.CreateRange([KeyValuePair.Create("_btn", token + "|"), KeyValuePair.Create("f0", "on")]), new Plans.AdapterSlotResolver(), static _ => false);

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("Source.Columns[1].IsVisible", fields.Fields.Select(field => field.Field?.Path));
        Assert.Contains("Source.Columns[2].IsVisible", fields.Fields.Select(field => field.Field?.Path));
        Assert.False(unchecking.Source.Columns[1].IsVisible);
        Assert.False(pressing.Source.Columns[2].IsVisible);
    }

    [Fact]
    [Trait("AC", "ENGINE-242")]
    public async Task ReflectionIntegerIndexersArePlannedAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Reflected, new Bookshelf("Dune", "Emma"), new Bookshelf("Ulysses", "Walden", "Hamlet"));

        Assert.Contains(">Walden (array)<", EngineFixture.Body(page, new Bookshelf("Ulysses", "Walden")), StringComparison.Ordinal);
        Assert.Contains(">Ulysses (ranked)<", EngineFixture.Body(page, new Bookshelf("Ulysses", "Walden")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-243")]
    public async Task IndexesOutOfRangeGiveNoValueAsync()
    {
        var compiled = await fixture.PlansLikeAvaloniaAsync("<m:Indexed />", new Bookshelf("Dune", "Emma"), new Bookshelf("Ulysses"), new Bookshelf());
        var reflected = await fixture.PlansLikeAvaloniaAsync(Reflected, new Bookshelf("Dune", "Emma"), new Bookshelf("Ulysses"), new Bookshelf());

        Assert.DoesNotContain("Dune", EngineFixture.Body(compiled, new Bookshelf()), StringComparison.Ordinal);
        Assert.Contains(">Ulysses<", EngineFixture.Body(reflected, new Bookshelf("Ulysses")), StringComparison.Ordinal);
    }
}
