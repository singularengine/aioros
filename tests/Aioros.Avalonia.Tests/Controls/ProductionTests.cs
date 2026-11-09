using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class ProductionTests(EngineFixture fixture)
{
    private const string Page = """
        <StackPanel>
          <TextBlock Text="{Binding Title}" IsVisible="{Binding Visible}" />
          <TextBox Text="{Binding Name}" />
          <CheckBox Content="Agree" IsChecked="{Binding Agree}" />
          <ToggleSwitch IsChecked="{Binding Agree}" />
          <RadioButton Content="Yes" IsChecked="{Binding Agree}" />
          <ComboBox SelectedItem="{Binding Plan}"><x:String>Free</x:String><x:String>Pro</x:String></ComboBox>
          <Slider Minimum="0" Maximum="100" Value="{Binding Volume}" />
          <ProgressBar Minimum="0" Maximum="100" Value="{Binding Progress}" ShowProgressText="True" />
          <CalendarDatePicker SelectedDate="{Binding Day}" />
          <TimePicker SelectedTime="{Binding At}" />
          <NumericUpDown Value="{Binding Volume}" />
          <ListBox ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}">
            <ListBox.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></ListBox.ItemTemplate>
          </ListBox>
          <TabControl SelectedIndex="{Binding Tab}">
            <TabItem Header="A"><TextBlock Text="first" /></TabItem>
            <TabItem Header="B"><TextBlock Text="second" /></TabItem>
          </TabControl>
          <PipsPager NumberOfPages="{Binding Count}" SelectedPageIndex="{Binding Page}" />
          <ItemsControl ItemsSource="{Binding Items}">
            <ItemsControl.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></ItemsControl.ItemTemplate>
          </ItemsControl>
          <a:Icon Kind="{Binding Icon}" />
          <Expander Header="More" IsExpanded="{Binding Agree}"><TextBlock Text="inside" /></Expander>
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "PRODUCTION-03")]
    public async Task CompiledPagesSerializeAsync()
    {
        var page = await fixture.CompileAsync(Page, new Models.Showcase());
        var other = new Models.Showcase { Title = "Other", Visible = false, Name = "Grace", Plan = "Free", Agree = false, Volume = 75, Progress = 90, Tab = 1, Page = 3, Icon = "x", Items = [new Item("a"), new Item("b"), new Item("c")] };

        var write = PlanSerializer.TryWrite(page.Plan.Body);
        var read = PlanSerializer.Read(write.Json ?? "[]", new AdapterSlotResolver());

        Assert.True(page.Reusable, page.Failure);
        Assert.Null(write.Failure);
        Assert.Equal(Render(page.Plan.Body, other), Render(read, other));
    }

    private const string Form = """
        <StackPanel>
          <TextBox Text="{Binding Name}" />
          <CheckBox Content="Agree" IsChecked="{Binding Agree}" />
          <ToggleSwitch IsChecked="{Binding Express}" />
          <NumericUpDown Value="{Binding Quantity}" Minimum="1" Maximum="10" />
          <Slider Minimum="0" Maximum="100" Value="{Binding Volume}" />
          <CalendarDatePicker SelectedDate="{Binding Day}" />
          <TimePicker SelectedTime="{Binding At}" />
          <ComboBox SelectedItem="{Binding Plan}"><x:String>Free</x:String><x:String>Pro</x:String></ComboBox>
          <ListBox ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}">
            <ListBox.ItemTemplate><DataTemplate x:DataType="m:Item"><TextBlock Text="{Binding Name}" /></DataTemplate></ListBox.ItemTemplate>
          </ListBox>
          <PipsPager NumberOfPages="{Binding Count}" SelectedPageIndex="{Binding Page}" IsPreviousButtonVisible="False" IsNextButtonVisible="False" />
          <Button Content="Save" Command="{Binding SaveCommand}" CommandParameter="note" />
          <TextBlock Text="{Binding Saved}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "PRODUCTION-06")]
    public async Task FormsApplyToPlansLikeAvaloniaAsync()
    {
        var page = await fixture.CompileAsync(Form, new Checkout());
        var fields = PlanForms.Fields(page.Plan, [new Checkout()]);
        var form = fields.Fields
            .Select(field => (field.Field?.Kind, Name: PlanForms.FieldName(field.Id), field.Id))
            .Select(field => field.Kind switch
            {
                "text" => (field.Name, "Grace"),
                "toggle" => (field.Name, "on"),
                "number" => (field.Name, "7"),
                "slider" => (field.Name, "150"),
                "date" => (field.Name, "2025-01-02"),
                "time" => (field.Name, "08:30"),
                "combo" => (field.Name, "0"),
                "choice-item" => (field.Name, "2"),
                "page" => (field.Name, "3"),
                "command" => (PlanForms.ButtonField, PlanForms.ButtonValue(field.Id, "note")),
                _ => (field.Name, string.Empty),
            })
            .Where(pair => pair.Item2.Length > 0)
            .ToImmutableDictionary(pair => pair.Item1, pair => pair.Item2);
        var expected = new Checkout();
        var actual = new Checkout();

        var posted = await fixture.PostAsync(Form, expected, [.. form.Select(pair => (pair.Key, pair.Value))]);
        var status = await PlanForms.ApplyAsync(page.Plan, [actual], form, new AdapterSlotResolver());

        Assert.True(page.Reusable, page.Failure);
        Assert.True(fields.Described);
        Assert.Equal(10, form.Count);
        Assert.Equal(PostStatus.Rendered, status);
        Assert.Equal("Grace|False|True|7|100|2025-01-02T00:00:00.0000000|08:30:00|Free|three|3|note:Grace", expected.Snapshot);
        Assert.Equal(expected.Snapshot, actual.Snapshot);
        Assert.Equal(Render(posted.Page!.Plan.Body, expected), Render(page.Plan.Body, actual));
    }

    private static string Render(ImmutableArray<IPlanNode> body, object model)
    {
        var writer = new ArrayBufferWriter<byte>();
        new RenderPlan([], body, []).RenderBody(writer, [model]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
