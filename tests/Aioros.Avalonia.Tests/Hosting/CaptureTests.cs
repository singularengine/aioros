using Aioros.Avalonia.Tests.Controls;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class CaptureTests(EngineFixture fixture)
{
    private const string Button = """<Button Content="Go" Width="120" Height="40" Margin="10" HorizontalAlignment="Left" VerticalAlignment="Top" />""";

    [Fact]
    [Trait("AC", "ENGINE-55")]
    public async Task StatesAreForcedOnInputElementsBeforeTheFrameAsync()
    {
        var normal = Pixels.At((await fixture.CaptureInAsync(Button, new CaptureOptions("light", 200, 80))).Png, 20, 20);
        var over = Pixels.At((await fixture.CaptureInAsync(Button, new CaptureOptions("light", 200, 80, "pointerover"))).Png, 20, 20);
        var pressed = Pixels.At((await fixture.CaptureInAsync(Button, new CaptureOptions("light", 200, 80, "pressed"))).Png, 20, 20);

        Assert.NotEqual(normal, over);
        Assert.NotEqual(over, pressed);
        await Assert.ThrowsAsync<ArgumentException>(async () => await fixture.CaptureInAsync(Button, new CaptureOptions("light", 200, 80, "hovering")));
    }

    [Fact]
    [Trait("AC", "ENGINE-56")]
    public async Task VisibleTextsAreListedWithTheirBoundsAsync()
    {
        var captured = await fixture.CaptureInAsync(
            """
            <StackPanel HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock Text="Hello" Margin="10,20,0,0" HorizontalAlignment="Left" />
              <TextBlock Text="Hidden" IsVisible="False" />
              <TextBox Text="World" Width="160" HorizontalAlignment="Left" />
            </StackPanel>
            """,
            new CaptureOptions("light", 300, 200));

        var hello = Assert.Single(captured.Texts, text => text.Text == "Hello");
        var world = Assert.Single(captured.Texts, text => text.Text == "World");

        Assert.Equal((10, 20), (hello.X, hello.Y));
        Assert.True(hello.Width > 0 && hello.Height > 0);
        Assert.True(world.X > 0 && world.Y > hello.Y + hello.Height && world.X + world.Width < 160);
        Assert.DoesNotContain(captured.Texts, text => text.Text == "Hidden");
    }

    [Fact]
    [Trait("AC", "ENGINE-57")]
    public async Task DisabledElementsKeepTheirLookInEveryStateAsync()
    {
        const string Field = """<TextBox Text="Off" IsEnabled="False" Width="120" Margin="10" HorizontalAlignment="Left" VerticalAlignment="Top" />""";

        var looks = new List<(byte R, byte G, byte B)>();
        foreach (var state in new[] { "normal", "pointerover", "pressed", "focus" })
        {
            looks.Add(Pixels.At((await fixture.CaptureInAsync(Field, new CaptureOptions("light", 200, 80, state))).Png, 100, 20));
        }

        Assert.Single(looks.Distinct());
    }

    [Fact]
    [Trait("AC", "ENGINE-65")]
    public async Task AnObjectOfAnotherThreadFailsItsCaptureAloneAsync()
    {
        var foreign = await Task.Run(static () => new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Colors.Teal), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.CaptureInAsync("""<Border Background="{Binding Fill}" Width="40" Height="40" />""", new CaptureOptions("light", 100, 100), new Models.Painted(foreign)));
        var next = await fixture.CaptureInAsync("""<Border Background="Teal" Width="40" Height="40" HorizontalAlignment="Left" VerticalAlignment="Top" />""", new CaptureOptions("light", 100, 100));

        Assert.Contains("Border.Background", refused.Message, StringComparison.Ordinal);
        Assert.Contains("thread", refused.Message, StringComparison.Ordinal);
        Assert.Equal(((byte)0, (byte)128, (byte)128), Pixels.At(next.Png, 20, 20));
    }

    [Fact]
    [Trait("AC", "ENGINE-263")]
    public async Task AnimationsAreCapturedAsTheBrowserShowsThemAsync()
    {
        var captured = await fixture.CaptureInAsync(
            """
            <StackPanel HorizontalAlignment="Left" VerticalAlignment="Top">
              <StackPanel.Styles>
                <Style Selector="Border.finite">
                  <Style.Animations>
                    <Animation Duration="0:0:10" FillMode="Forward">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="0" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
                <Style Selector="Border.endless">
                  <Style.Animations>
                    <Animation Duration="0:0:0.7" IterationCount="Infinite">
                      <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0" /></KeyFrame>
                      <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1" /></KeyFrame>
                    </Animation>
                  </Style.Animations>
                </Style>
              </StackPanel.Styles>
              <Border Classes="finite" Width="40" Height="40" Background="#ff0000" />
              <Border Classes="endless" Width="40" Height="40" Background="#ff0000" />
            </StackPanel>
            """,
            new CaptureOptions("light", 100, 100));

        var hovered = await fixture.CaptureInAsync(
            """
            <Button Content=" " Width="60" Height="40" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button.Styles>
                <Style Selector="Button /template/ ContentPresenter#PART_ContentPresenter">
                  <Setter Property="Transitions"><Transitions><BrushTransition Property="Background" Duration="0:0:10" /></Transitions></Setter>
                </Style>
                <Style Selector="Button:pointerover /template/ ContentPresenter#PART_ContentPresenter">
                  <Setter Property="Background" Value="#0000ff" />
                </Style>
              </Button.Styles>
            </Button>
            """,
            new CaptureOptions("light", 100, 100, "pointerover"));

        Assert.Equal(((byte)255, (byte)255, (byte)255), Pixels.At(captured.Png, 20, 20));
        Assert.Equal(((byte)255, (byte)255, (byte)255), Pixels.At(captured.Png, 20, 60));
        Assert.Equal(((byte)0, (byte)0, (byte)255), Pixels.At(hovered.Png, 30, 20));
    }
}
