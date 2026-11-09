using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Motion;

public sealed class SweepTests(DesktopFixture fixture)
{
    private const string Source = "Pages/sweeps/Page.axaml";

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <UserControl.Resources>
            <LinearGradientBrush x:Key="Brand" StartPoint="0%,0%" EndPoint="100%,0%"><GradientStop Offset="0" Color="#7C3AED" /><GradientStop Offset="1" Color="#06B6D4" /></LinearGradientBrush>
          </UserControl.Resources>
          <TextBlock Name="Swept" Text="Swept" Width="200" Foreground="{StaticResource Brand}" a:Web.Sweep="4" />
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-101")]
    public async Task SweptGradientsMoveOnEachFrameAsync()
    {
        var page = DesktopFixture.Page("/sweeps", Source, _ => new Reloaded());
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml);

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/sweeps");
            DesktopFixture.Settle(window);
            var text = window.GetVisualDescendants().OfType<TextBlock>().First(control => control.Name == "Swept");
            var shared = (LinearGradientBrush)text.FindAncestorOfType<UserControl>()!.FindResource("Brand")!;
            Frame();
            var first = Offset(text);
            for (var frame = 0; frame < 6; frame++)
            {
                Frame();
            }

            var later = Offset(text);
            var own = text.Foreground as LinearGradientBrush;
            window.Close();
            return (first, later, Own: own is not null && !ReferenceEquals(own, shared), own?.SpreadMethod, SharedUntouched: shared.Transform is null);
        });

        Assert.True(result.later > result.first, $"{result.first} -> {result.later}");
        Assert.Equal((true, GradientSpreadMethod.Repeat, true), (result.Own, result.SpreadMethod, result.SharedUntouched));
    }

    private static double Offset(TextBlock text) =>
        text.Foreground is Brush { Transform: TransformGroup group } ? group.Children.OfType<TranslateTransform>().Single().X : double.NaN;

    private static void Frame()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
