using System.Text.RegularExpressions;
using Aioros.Avalonia.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Motion;

public sealed class CountTests(DesktopFixture fixture)
{
    private const string Source = "Pages/counts/Page.axaml";

    private const string Xaml = """
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:a="https://aioros.dev" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <TextBlock Name="Figure" Text="85,620" a:Web.CountUp="0.2" />
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-102")]
    public async Task NumbersCountUpToTheirTextAsync()
    {
        var page = DesktopFixture.Page("/counts", Source, _ => new Reloaded());
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml);

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/counts");
            DesktopFixture.Settle(window);
            var text = window.GetVisualDescendants().OfType<TextBlock>().First(control => control.Name == "Figure");
            var seen = new List<string>();
            for (var frame = 0; frame < 2000 && text.Text != "85,620" | seen.Count == 0; frame++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                seen.Add(text.Text ?? string.Empty);
            }

            window.Close();
            return (seen, final: text.Text);
        });

        var steps = result.seen.Where(value => value != "85,620").ToList();
        Assert.NotEmpty(steps);
        Assert.All(steps, value => Assert.Matches(@"^\d{1,3}(,\d{3})?$", value));
        Assert.All(steps, value => Assert.True(int.Parse(value.Replace(",", string.Empty, StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture) < 85_620, value));
        Assert.Equal("85,620", result.final);
    }
}
