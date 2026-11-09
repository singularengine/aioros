using Aioros.Avalonia.Tests.Hosting;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class AspectRatioTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "LAYOUT-94")]
    public async Task AnAlignedRatioTakesItsSlotUpToItsMaxWidthAsync()
    {
        var page = await fixture.CompileAsync("""
            <StackPanel Width="510" HorizontalAlignment="Left" Spacing="16">
              <Border a:Web.Id="wide" a:Web.AspectRatio="1.7778" MaxWidth="768" HorizontalAlignment="Left" Background="#f5f5f5" />
              <Border a:Web.Id="narrow" a:Web.AspectRatio="1.7778" MaxWidth="300" HorizontalAlignment="Left" Background="#f5f5f5" />
            </StackPanel>
            """);

        var boxes = await BrowserLayout.EvaluateAsync(page, 800, 600, """
            () => ['wide', 'narrow'].map((id) => {
              const box = document.getElementById(id).getBoundingClientRect(), column = document.getElementById(id).parentElement.getBoundingClientRect()
              return `${Math.round(box.x - column.x)} ${Math.round(box.width)}x${Math.round(box.height)}`
            }).join()
            """);

        Assert.Equal("0 510x287,0 300x169", boxes);
    }
}
