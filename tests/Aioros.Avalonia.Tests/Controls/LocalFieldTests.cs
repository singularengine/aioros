using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Aioros.Targets.Ssr.Pages;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class LocalFieldTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "SSR-292")]
    public async Task FieldsWhoseValuesGoNowhereAreMarkedLocalAsync()
    {
        var html = await fixture.HtmlAsync("""
            <StackPanel>
              <TextBox Text="demo" AutomationProperties.Name="Demo" />
              <CheckBox Content="Remember" IsChecked="True" />
              <ToggleSwitch IsChecked="True" AutomationProperties.Name="Switch" />
              <RadioButton GroupName="g" Content="A" IsChecked="True" />
              <RadioButton GroupName="g" Content="B" />
              <TextBox Text="{Binding Name}" AutomationProperties.Name="Bound" />
              <CheckBox Content="Agree" IsChecked="{Binding Agree}" />
              <TextBox Name="Email" Text="x" AutomationProperties.Name="Named" />
            </StackPanel>
            """, new Profile());

        var fields = Regex.Matches(html, "<input [^>]*>").Select(match => match.Value).Where(tag => !tag.Contains("type=\"hidden\"", StringComparison.Ordinal)).ToList();
        var local = fields.Select(tag => tag.Contains("data-aioros-local", StringComparison.Ordinal)).ToList();

        Assert.Equal(8, fields.Count);
        Assert.Equal([true, true, true, true, false, false, false, false], local);
        Assert.All(fields.Where(tag => tag.Contains("data-aioros-local", StringComparison.Ordinal)), tag => Assert.Matches("name=\"f\\d+\" data-aioros-local=\"\"", tag));
    }

    private const string LocalInputs = """
        <StackPanel>
          <TextBlock Text="{Binding Name}" />
          <CheckBox Content="Remember" />
          <ToggleSwitch IsChecked="True" AutomationProperties.Name="Switch" />
          <ToggleButton Content="Bold" />
          <Slider Minimum="0" Maximum="10" Value="4" AutomationProperties.Name="Volume" />
          <TextBox Text="demo" AutomationProperties.Name="Demo" />
          <NumericUpDown Value="3" Minimum="0" Maximum="9" AutomationProperties.Name="Count" />
          <ComboBox SelectedIndex="0"><x:String>Ada</x:String><x:String>Bea</x:String></ComboBox>
          <ListBox SelectedIndex="1"><x:String>One</x:String><x:String>Two</x:String><x:String>Three</x:String></ListBox>
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-310")]
    public async Task InputsNotBoundArePlannedOverThePagesOwnStateAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(LocalInputs, new Profile());
        var fields = PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button).ToList();
        var written = EngineFixture.Body(page, new Profile());
        var posted = PostedForms.Fields(written).Where(static field => field.Name.StartsWith('f')).ToList();
        var form = posted.Aggregate(PostedForms.Of(written), static (form, field) => field.Samples().FirstOrDefault() is { } sample ? form.SetItem(field.Name, sample) : form.Remove(field.Name));

        var model = new Profile();
        string planned;
        PostStatus? status;
        using (LocalValues.Use())
        {
            status = await PlanForms.ApplyAsync(page.Plan, [model], form, new AdapterSlotResolver());
            planned = EngineFixture.Body(page, model);
        }

        var engine = await fixture.PostAsync(LocalInputs, new Profile(), [.. form.Select(static pair => (pair.Key, pair.Value))]);

        Assert.All(fields, static field => Assert.StartsWith("@", field.Field?.Path ?? "-", StringComparison.Ordinal));
        Assert.Equal(8, fields.Count);
        Assert.Equal(PostStatus.Rendered, status);
        Assert.Equal("Ada", model.Name);
        Assert.Equal(EngineFixture.Body(engine.Page!, new Profile()), planned);
        Assert.NotEqual(written, planned);
    }

    [Fact]
    [Trait("AC", "ENGINE-313")]
    public async Task OnlyInputsWrittenOnceReadLocalPathsAsync()
    {
        const string Repeated = """
            <StackPanel DataContext="{x:Null}">
              <ToggleSwitch AutomationProperties.Name="Switch" />
              <ItemsControl>
                <ItemsControl.ItemTemplate><DataTemplate><CheckBox Content="{Binding}" /></DataTemplate></ItemsControl.ItemTemplate>
                <x:String>One</x:String>
                <x:String>Two</x:String>
              </ItemsControl>
              <ListBox><ListBoxItem><CheckBox Content="Own" /></ListBoxItem></ListBox>
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Repeated, new Profile());
        var paths = PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button && field.Field is not null)
            .Select(static field => (field.Field!.Kind, Local: LocalPaths.TryRead(field.Field.Path ?? string.Empty, out _))).ToList();

        Assert.Equal(("toggle", true), paths[0]);
        Assert.Equal(("check", true), paths[^1]);
        Assert.DoesNotContain(paths[1..^1], static path => path.Local && path.Kind == "check");
    }

    [Fact]
    [Trait("AC", "ENGINE-315")]
    public async Task InputsOnlyTheVisualTreeHoldsReadLocalPathsAsync()
    {
        var page = await fixture.CompileAsync("""
            <DrawerPage DrawerLayoutBehavior="Split" IsOpen="True" DrawerLength="260">
              <DrawerPage.DrawerHeader><ToggleSwitch AutomationProperties.Name="Theme" /></DrawerPage.DrawerHeader>
              <DrawerPage.Drawer><CheckBox Content="Compact" /></DrawerPage.Drawer>
              <ContentPage Header="Detail"><StackPanel><TextBox Text="demo" AutomationProperties.Name="Demo" /><NumericUpDown Value="2" /></StackPanel></ContentPage>
            </DrawerPage>
            """, new Profile());
        var fields = PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button).Select(static field => field.Field).ToList();

        Assert.Equal(["toggle", "check", "text", "number"], fields.Select(static field => field?.Kind ?? "-"));
        Assert.All(fields, static field => Assert.True(LocalPaths.TryRead(field!.Path!, out _)));
    }

    private const string Named = """
        <StackPanel>
          <ToggleSwitch Name="more" AutomationProperties.Name="More" />
          <TextBlock Text="Details" IsVisible="{Binding #more.IsChecked}" />
          <Slider Name="level" Minimum="0" Maximum="100" IsSnapToTickEnabled="True" TickFrequency="10" Width="200" />
          <ProgressBar Minimum="0" Maximum="100" Width="200" Value="{Binding #level.Value}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-316")]
    public async Task NamedInputsAndTheBindingsThatReadThemShareTheirLocalPathsAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Named, new Profile());
        var fields = PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button).ToList();
        var form = PostedForms.Of(EngineFixture.Body(page, new Profile())).SetItem(PlanForms.FieldName(fields[0]), "on").SetItem(PlanForms.FieldName(fields[1]), "37");

        string planned;
        using (LocalValues.Use())
        {
            await PlanForms.ApplyAsync(page.Plan, [new Profile()], form, new AdapterSlotResolver());
            planned = EngineFixture.Body(page, new Profile());
        }

        var engine = await fixture.PostAsync(Named, new Profile(), [.. form.Select(static pair => (pair.Key, pair.Value))]);

        Assert.Equal(["toggle", "slider"], fields.Select(static field => field.Field?.Kind ?? "-"));
        Assert.Equal(EngineFixture.Body(engine.Page!, new Profile()), planned);
        Assert.Contains("Details", planned, StringComparison.Ordinal);
    }

    private const string Paged = """
        <StackPanel>
          <ToggleSplitButton Content="Bold" />
          <PipsPager NumberOfPages="5" SelectedPageIndex="1" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-317")]
    public async Task ToggleSplitButtonsAndPipsPagersArePlannedOverThePagesOwnStateAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Paged, new Profile());
        var fields = PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button).ToList();
        var written = EngineFixture.Body(page, new Profile());
        var form = PostedForms.Of(written).SetItem(PlanForms.FieldName(fields[0]), "on").SetItem(PlanForms.FieldName(fields[3]), "on");

        string planned;
        using (LocalValues.Use())
        {
            await PlanForms.ApplyAsync(page.Plan, [new Profile()], form, new AdapterSlotResolver());
            planned = EngineFixture.Body(page, new Profile());
        }

        var engine = await fixture.PostAsync(Paged, new Profile(), [.. form.Select(static pair => (pair.Key, pair.Value))]);

        Assert.Equal(["toggle", "page", "page-step", "page-step"], fields.Select(static field => field.Field?.Kind ?? "-"));
        Assert.All(fields, static field => Assert.True(LocalPaths.TryRead(field.Field!.Path!, out _)));
        Assert.Equal(EngineFixture.Body(engine.Page!, new Profile()), planned);
        Assert.NotEqual(written, planned);
    }

    [Fact]
    [Trait("AC", "ENGINE-318")]
    public async Task ALibrarysSelectingControlIsNotTheLocalStateOfThePageAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel>
              <m:LibraryList Name="menu" SelectedIndex="0"><x:String>Home</x:String><x:String>Docs</x:String></m:LibraryList>
              <ListBox SelectedIndex="0"><x:String>One</x:String><x:String>Two</x:String></ListBox>
            </StackPanel>
            """, new Profile());
        var paths = PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button)
            .Select(static field => field.Field?.Path is { } path && LocalPaths.TryRead(path, out _)).ToList();

        Assert.Equal([false, true], paths);
    }

    [Fact]
    [Trait("AC", "ENGINE-320")]
    public async Task OnlyInputsUnderAGuaranteedThemeHoldThePagesOwnStateAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="Own" TargetType="ToggleButton">
                  <Setter Property="Template"><ControlTemplate><Border Background="Red" Padding="4"><ContentPresenter Content="{TemplateBinding Content}" /></Border></ControlTemplate></Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <CheckBox Content="Remember" />
              <ToggleSwitch AutomationProperties.Name="Switch" />
              <TextBox Text="demo" AutomationProperties.Name="Demo" />
              <ToggleButton Theme="{StaticResource Own}" Content="Own" />
              <StackPanel>
                <StackPanel.Styles><Style Selector="CheckBox:checked"><Setter Property="Margin" Value="20" /></Style></StackPanel.Styles>
                <CheckBox Content="Moved" />
              </StackPanel>
              <StackPanel>
                <StackPanel.Styles><Style Selector="CheckBox[IsChecked=True]"><Setter Property="FontWeight" Value="Bold" /></Style></StackPanel.Styles>
                <CheckBox Content="Bolded" />
              </StackPanel>
            </StackPanel>
            """, new Profile());

        Assert.Equal(["guaranteed", "guaranteed", "guaranteed", "outside", "outside", "outside"], Guarantees(page));
    }

    [Fact]
    [Trait("AC", "ENGINE-321")]
    public async Task OnlyInputsNoCodeOutsideTheGuaranteeObservesHoldThePagesOwnStateAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <m:PlainPanel><CheckBox Content="Plain" /></m:PlainPanel>
              <m:HandledPanel><CheckBox Content="Handled" /></m:HandledPanel>
              <CheckBox Content="Marked" m:Marks.Note="kept" />
              <Slider Name="ratio" Minimum="1" Maximum="3" Value="2" Width="200" />
              <m:Proportion Value="{Binding #ratio.Value}" Width="200"><Border Background="Red" /></m:Proportion>
            </StackPanel>
            """, new Profile());

        Assert.Equal([true, false, false, false], LocalFields(page));
    }

    [Fact]
    [Trait("AC", "ENGINE-325")]
    public async Task AnInputAnotherElementWritesBackIsNotThePagesOwnStateAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <ToggleButton x:Name="toggle" Content="Pane" />
              <SplitView IsPaneOpen="{Binding #toggle.IsChecked, Mode=TwoWay}" />
              <CheckBox Content="Remember" />
            </StackPanel>
            """, new Profile());

        Assert.Equal(["none", "guaranteed"], Guarantees(page));
    }

    [Fact]
    [Trait("AC", "ENGINE-332")]
    public async Task AListOfEqualItemsIsNotThePagesOwnStateAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync("""
            <StackPanel>
              <ComboBox Width="200"><x:String>Two</x:String><x:String>Two</x:String></ComboBox>
              <ComboBox Width="200"><x:String>One</x:String><x:String>Two</x:String></ComboBox>
            </StackPanel>
            """, new Profile());

        Assert.Equal(["none", "guaranteed"], Guarantees(page));
    }

    private static List<string> Guarantees(CompiledPage page) =>
        [.. PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button).Select(static field => field.Field?.Path is { } path && LocalPaths.TryRead(path, out var local) ? local.Guaranteed ? "guaranteed" : "outside" : "none")];

    private static List<bool> LocalFields(CompiledPage page) =>
        [.. PlanForms.Fields(page.Plan, [new Profile()]).Fields.Where(static field => !field.Button).Select(static field => field.Field?.Path is { } path && LocalPaths.TryRead(path, out _))];
}
