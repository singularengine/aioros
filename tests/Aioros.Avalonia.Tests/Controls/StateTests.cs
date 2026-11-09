using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class StateTests(EngineFixture fixture)
{
    private const string Body = """
        <StackPanel>
          <m:Stepper a:Web.State="Step,Mode,Note" Mode="Busy" Note="hello">
            <m:StepPicker a:Web.State="Picked" Picked="True" IsVisible="False" />
          </m:Stepper>
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-265")]
    public async Task ControlStatesAreWrittenInHiddenInputsAsync()
    {
        var html = await fixture.HtmlAsync(Body);
        var plain = await fixture.HtmlAsync("""<StackPanel><m:Stepper Mode="Busy" /></StackPanel>""");

        Assert.Contains("<input type=\"hidden\" name=\"_s0\" value=\"0\"><input type=\"hidden\" name=\"_s1\" value=\"Busy\"><input type=\"hidden\" name=\"_s2\" value=\"hello\"><input type=\"hidden\" name=\"_s3\" value=\"True\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"_s", plain, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-266")]
    public async Task ControlStatesAreRestoredBeforeTheButtonIsLookedForAsync()
    {
        var second = await fixture.HtmlAsync(Body.Replace("Mode=\"Busy\"", "Step=\"1\" Mode=\"Busy\"", StringComparison.Ordinal));
        var finish = Regex.Match(second, "name=\"_btn\" value=\"([0-9]+\\|)\"[^>]*>Finish").Groups[1].Value;

        var pressed = await fixture.PostAsync(Body, new ShellModel(true), ("_s0", "1"), ("_btn", finish));
        var stale = await fixture.PostAsync(Body, new ShellModel(true), ("_s0", "1"), ("_s1", "Sideways"), ("_s9", "x"), ("_btn", finish));

        Assert.Equal(PostStatus.Rendered, pressed.Status);
        Assert.Contains(">Step 2<", EngineFixture.Body(pressed.Page!), StringComparison.Ordinal);
        Assert.Equal(PostStatus.Rendered, stale.Status);
        var answer = EngineFixture.Body(stale.Page!);
        Assert.Contains(">Step 2<", answer, StringComparison.Ordinal);
        Assert.Contains("name=\"_s1\" value=\"Busy\"", answer, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-283")]
    public async Task AStateBoundToThePageModelIsPlannedBehindALayoutAsync()
    {
        var page = await fixture.RenderAsync(EngineFixture.Page("StateShell", "Pages/Shell/Layout.axaml"), "light", new Brand("Lumen"), new Article("Inbox"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Contains("<input type=\"hidden\" name=\"_s0\" value=\"Inbox\">", EngineFixture.Body(page, new Brand("Lumen"), new Article("Inbox")), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-284")]
    public async Task ARadioGroupWrittenOncePerValueKeepsItsFieldAsync()
    {
        const string Grouped = """
            <StackPanel>
              <TextBox Text="x" />
              <StackPanel HorizontalAlignment="{Binding Align}">
                <RadioButton Content="A" />
                <RadioButton Content="B" />
              </StackPanel>
            </StackPanel>
            """;
        var page = await fixture.PlansLikeAvaloniaAsync(Grouped, new Aligned(global::Avalonia.Layout.HorizontalAlignment.Left), new Aligned(global::Avalonia.Layout.HorizontalAlignment.Right));

        Assert.Equal(2, Regex.Count(EngineFixture.Body(page, new Aligned(global::Avalonia.Layout.HorizontalAlignment.Right)), "type=\"radio\"[^>]* name=\"f1\""));
    }

    [Fact]
    [Trait("AC", "ENGINE-289")]
    public async Task AnAttachedStateRebuildsThePageBeforeItsPostIsBoundAsync()
    {
        var written = await fixture.HtmlAsync(Paged, new PagedModel { Page = 1 });
        var fields = Regex.Matches(written, "<input[^>]* name=\"(f[0-9]+)\"").Select(match => match.Groups[1].Value).ToList();

        var posted = await fixture.PostAsync(Paged, new PagedModel(), ("_s0", "1"), (fields[0], "B"), (fields[1], "C"), (fields[2], "T"));

        Assert.Contains("<input type=\"hidden\" name=\"_s0\" value=\"1\">", written, StringComparison.Ordinal);
        Assert.Equal(3, fields.Count);
        Assert.Equal(PostStatus.Rendered, posted.Status);
        Assert.Equal(["B", "C", "T"], Regex.Matches(EngineFixture.Body(posted.Page!), "<input[^>]* name=\"f[0-9]+\"[^>]* value=\"([^\"]*)\"").Select(match => match.Groups[1].Value));
    }
    private const string Paged = """
        <StackPanel>
          <ItemsControl m:Pager.Page="{Binding Page}" a:Web.State="Pager.Page" ItemsSource="{Binding Rows}">
            <ItemsControl.ItemTemplate><DataTemplate><TextBox Text="{Binding Text}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <TextBox Text="{Binding Tail}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-298")]
    public async Task AStateBoundTwoWayIsAStateNodeOfThePlanAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Paged, new PagedModel(), new PagedModel { Page = 1 });
        var local = await fixture.PlansLikeAvaloniaAsync(Body, new ShellModel(true));
        var fields = Regex.Matches(EngineFixture.Body(page, new PagedModel { Page = 1 }), "<input[^>]* name=\"(f[0-9]+)\"").Select(match => match.Groups[1].Value).ToList();
        var form = new[] { ("_s0", "1"), (fields[0], "B"), (fields[1], "C"), (fields[2], "T") };
        var planned = new PagedModel();

        var status = await PlanForms.ApplyAsync(page.Plan, [planned], form.ToImmutableDictionary(field => field.Item1, field => field.Item2), new AdapterSlotResolver());
        var engine = await fixture.PostAsync(Paged, new PagedModel(), form);

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal([new StateNode(0, new SlotSpec("state", "Page", "int"))], Nodes(page.Plan.Body).OfType<StateNode>());
        Assert.Empty(Nodes(local.Plan.Body).OfType<StateNode>());
        Assert.Equal(PostStatus.Rendered, status);
        Assert.Equal(EngineFixture.Body(engine.Page!), EngineFixture.Body(page, planned));
    }

    [Fact]
    [Trait("AC", "SSR-264")]
    public async Task AStateThatDoesNotParseIsLeftOutByThePlanAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Paged, new PagedModel(), new PagedModel { Page = 1 });
        var field = Regex.Match(EngineFixture.Body(page, new PagedModel()), "<input[^>]* name=\"(f[0-9]+)\"").Groups[1].Value;
        var planned = new PagedModel();

        var status = await PlanForms.ApplyAsync(page.Plan, [planned], ImmutableDictionary<string, string>.Empty.Add("_s0", "many").Add(field, "A2"), new AdapterSlotResolver());

        Assert.Equal(PostStatus.Rendered, status);
        Assert.Equal(0, planned.Page);
        Assert.Contains("value=\"A2\"", EngineFixture.Body(page, planned), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-300")]
    public async Task ValueInputsThatWriteNothingAreKeptFieldsAsync()
    {
        const string Inputs = """
            <StackPanel>
              <TextBox Text="{Binding Name, Mode=OneWay}" />
              <TextBox Text="local" />
              <AutoCompleteBox Text="{Binding Name, Mode=OneWay}" />
              <NumericUpDown Value="3" />
              <ComboBox SelectedItem="{Binding Name, Mode=OneWay}"><x:String>Ada</x:String><x:String>Bea</x:String></ComboBox>
              <StackPanel DataContext="{Binding Viewing}" x:DataType="m:Viewer"><TextBox Text="{Binding Name}" /></StackPanel>
              <CheckBox IsChecked="{Binding Open, Mode=OneWay}" />
              <TextBox Text="{Binding Name}" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Inputs, new Viewer());

        var kinds = PlanForms.Fields(page.Plan, [new Viewer()]).Fields.Where(field => !field.Button).Select(field => field.Field?.Kind);

        Assert.Equal("kept text kept number kept text - text", string.Join(" ", kinds.Select(kind => kind ?? "-")));
    }

    [Fact]
    [Trait("AC", "ENGINE-304")]
    public async Task AStateValueIsSealedWhenASealIsInEffectAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Paged, new PagedModel { Page = 1 }, new PagedModel());
        var plain = EngineFixture.Body(page, new PagedModel { Page = 1 });
        string sealedBody, dynamics;
        using (StateSeals.Use(new Prefixed()))
        {
            sealedBody = EngineFixture.Body(page, new PagedModel { Page = 1 });
            var writer = new System.Buffers.ArrayBufferWriter<byte>();
            PlanDynamics.Write(writer, page.Plan.Body, [new PagedModel { Page = 1 }]);
            dynamics = System.Text.Encoding.UTF8.GetString(writer.WrittenSpan);
        }

        Assert.Single(Nodes(page.Plan.Body).OfType<StateValueNode>());
        Assert.Contains("name=\"_s0\" value=\"1\"", plain, StringComparison.Ordinal);
        Assert.Contains("name=\"_s0\" value=\"sealed:_s0 1\"", sealedBody, StringComparison.Ordinal);
        Assert.Contains("sealed:_s0 1", dynamics, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "ENGINE-305")]
    public async Task APageCompiledUnderASealKeepsItsPlanAsync()
    {
        CompiledPage page;
        using (StateSeals.Use(new Salted()))
        {
            page = await fixture.CompileAsync(Paged, new PagedModel { Page = 1 });
        }

        Assert.True(page.Reusable, page.Failure);
    }

    private sealed class Salted : IStateSeal
    {
        public string Seal(string value) => value + "." + Guid.NewGuid().ToString("N");

        public bool TryOpen(string sealedValue, out string value)
        {
            value = sealedValue.Split('.')[0];
            return true;
        }
    }

    private sealed class Prefixed : IStateSeal
    {
        public string Seal(string value) => "sealed:" + value;

        public bool TryOpen(string sealedValue, out string value)
        {
            value = sealedValue.StartsWith("sealed:", StringComparison.Ordinal) ? sealedValue[7..] : string.Empty;
            return value.Length > 0;
        }
    }

    private static IEnumerable<IPlanNode> Nodes(ImmutableArray<IPlanNode> nodes) =>
        nodes.SelectMany(node => node switch
        {
            ScopeNode scope => Nodes(scope.Body).Prepend(node),
            ConditionNode condition => Nodes(condition.Body).Prepend(node),
            _ => [node],
        });
}
