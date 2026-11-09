using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class MethodCommandTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-63")]
    public async Task MethodBindingsArePlannedAsCommandsAsync()
    {
        const string Body = """
            <StackPanel>
              <Button Command="{Binding SayHello}" Content="Hello" />
              <Button Command="{Binding ArchiveAsync}" CommandParameter="30" Content="Archive" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body, new Greeter());
        var planned = new Greeter { Name = "Ada" };
        var refused = new Greeter();
        var engine = new Greeter { Name = "Ada" };

        var hello = await PlanForms.ApplyAsync(page.Plan, [planned], Form("0|"), new AdapterSlotResolver());
        var archive = await PlanForms.ApplyAsync(page.Plan, [planned], Form("1|30"), new AdapterSlotResolver());
        await PlanForms.ApplyAsync(page.Plan, [refused], Form("0|"), new AdapterSlotResolver());
        await fixture.PostAsync(Body, engine, ("_btn", "0|"));
        await fixture.PostAsync(Body, engine, ("_btn", "1|30"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("value=\"0\\|\"[^>]* disabled>Hello</button>", EngineFixture.Body(page, new Greeter()));
        Assert.Matches("value=\"0\\|\" class=\"[^\"]+\">Hello</button>", EngineFixture.Body(page, new Greeter { Name = "Ada" }));
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered), (hello, archive));
        Assert.Equal(["hello Ada", "archived 30"], planned.Greeted);
        Assert.Empty(refused.Greeted);
        Assert.Equal(["hello Ada", "archived 30"], engine.Greeted);
    }

    [Fact]
    [Trait("AC", "ENGINE-64")]
    public async Task CommandActionsArePlannedByTheirShapeAsync()
    {
        const string Body = """
            <StackPanel m:Act.Target="{Binding}">
              <TextBox Text="{Binding Name}" />
              <Button Command="{m:Act Wave}" Content="Wave" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body, new Greeter());
        var planned = new Greeter();
        var engine = new Greeter();

        await PlanForms.ApplyAsync(page.Plan, [planned], Form("0|").Add("f0", "Bo"), new AdapterSlotResolver());
        await fixture.PostAsync(Body, engine, ("f0", "Bo"), ("_btn", "0|"));
        var missing = await fixture.CompileAsync("""<StackPanel m:Act.Target="{Binding}"><Button Command="{m:Act Missing}" Content="Gone" /></StackPanel>""", new Greeter());

        Assert.True(page.Reusable, page.Failure);
        Assert.Matches("value=\"0\\|\"[^>]* disabled>Wave</button>", EngineFixture.Body(page, new Greeter()));
        Assert.Matches("value=\"0\\|\" class=\"[^\"]+\">Wave</button>", EngineFixture.Body(page, new Greeter { Name = "Bo" }));
        Assert.Equal(["wave Bo"], planned.Greeted);
        Assert.Equal(["wave Bo"], engine.Greeted);
        Assert.False(missing.Reusable);
        Assert.Contains("Button.Command is the action 'Missing' (ShapedAction)", missing.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-121")]
    public async Task TemplateButtonsRunTheirControlsMethodsThroughAvaloniaAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source="avares://Aioros.Avalonia.Tests/Themes/ClearableTheme.axaml" /></ResourceDictionary.MergedDictionaries></ResourceDictionary>
              </StackPanel.Resources>
              <TextBlock Text="{Binding Text}" />
              <m:Clearable Text="seed" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body, Tile.On);
        var html = EngineFixture.Body(page, Tile.On);
        var button = System.Text.RegularExpressions.Regex.Match(html, "value=\"(\\d+\\|)\" class=\"[^\"]+\">Clear</button>");
        var posted = await fixture.PostAsync(Body, Tile.Off, (PlanForms.ButtonField, button.Groups[1].Value));
        var guarded = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.Styles>
                <Style Selector="m|Resettable">
                  <Setter Property="Template">
                    <ControlTemplate><Button Content="Reset" Command="{Binding $parent[m:Resettable].Reset}" /></ControlTemplate>
                  </Setter>
                </Style>
              </StackPanel.Styles>
              <m:Resettable />
            </StackPanel>
            """, Tile.On);

        Assert.True(page.Reusable, page.Failure);
        foreach (var model in new[] { Tile.On, Tile.Off })
        {
            Assert.Equal(EngineFixture.Body(await fixture.CompileAsync(Body, model), model), EngineFixture.Body(page, model));
        }

        Assert.True(button.Success, html);
        Assert.Contains(PlanForms.Fields(page.Plan, [Tile.On]).Fields, field => field.Button && field.Field is null);
        Assert.Contains(">seed<", html, StringComparison.Ordinal);
        Assert.Contains(">icon<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">seed<", EngineFixture.Body(posted.Page!, Tile.Off), StringComparison.Ordinal);
        Assert.False(guarded.Reusable);
        Assert.Contains("Resettable.CanReset", guarded.Failure, StringComparison.Ordinal);
    }

    private static ImmutableDictionary<string, string> Form(string button) => ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, button);
}
