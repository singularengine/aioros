using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Models;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class LiveCompileTests(EngineFixture fixture)
{
    private const string List = """
          <ListBox ItemsSource="{Binding Ticks}">
            <ListBox.ItemTemplate>
              <DataTemplate x:DataType="m:Tick">
                <TextBlock Text="{Binding Name}" />
              </DataTemplate>
            </ListBox.ItemTemplate>
          </ListBox>
        """;

    [Fact]
    [Trait("AC", "ENGINE-46")]
    public async Task LiveChangesWaitForTheCompileAsync()
    {
        Task? fed = null;
        var ticker = new Ticker(model => fed = fixture.Engine.InvokeAsync(() => model.Price = "11", TestContext.Current.CancellationToken).AsTask(), "a", "b");

        var page = await fixture.CompileAsync("<StackPanel><TextBlock Text=\"{Binding Price}\" />" + List + List + "</StackPanel>", ticker);
        await fed!;

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal("11", ticker.Price);
        Assert.Contains(">11<", EngineFixture.Body(page, ticker), StringComparison.Ordinal);
    }
}
