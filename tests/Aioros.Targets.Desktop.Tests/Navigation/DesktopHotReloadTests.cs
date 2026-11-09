using Aioros.Avalonia.Hosting;
using Aioros.Pages;
using Aioros.Targets.Desktop.HotReload;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aioros.Targets.Desktop.Tests.Navigation;

public sealed class DesktopHotReloadTests(DesktopFixture fixture)
{
    private const string Source = "Pages/reloads/Page.axaml";

    private static string Xaml(string marker) => $$"""
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:m="using:Aioros.Targets.Desktop.Tests.Navigation" x:DataType="m:Reloaded">
          <ScrollViewer Height="120">
            <StackPanel>
              <TextBlock Text="{{marker}}" />
              <TextBox Name="Draft" />
              <TextBox Name="Bound" Text="{Binding Name}" />
              <Border Height="2000" />
            </StackPanel>
          </ScrollViewer>
        </UserControl>
        """;

    [Fact]
    [Trait("AC", "DESKTOP-98")]
    public async Task AReloadedSourceKeepsItsModelsAndViewStateAsync()
    {
        var model = new Reloaded();
        var loads = 0;
        var page = DesktopFixture.Page("/reloads", Source, _ =>
        {
            loads++;
            return model;
        });
        var sources = new XamlSources(typeof(DesktopFixture).Assembly);
        sources.Override(Source, Xaml("Before"));

        var result = await fixture.OnUiAsync(async () =>
        {
            var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
            await navigator.NavigateAsync("/reloads");
            DesktopFixture.Settle(window);
            Named<TextBox>(window, "Draft").Text = "typed";
            Named<TextBox>(window, "Bound").Focus();
            Viewer(window).Offset = new Vector(0, 300);
            DesktopFixture.Settle(window);
            var scrolled = Viewer(window).Offset.Y;
            var layout = window.Content;
            var pageView = Named<TextBox>(window, "Draft").FindAncestorOfType<UserControl>();

            sources.Override(Source, Xaml("After"));
            var reloaded = navigator.Reload(Source);
            DesktopFixture.Settle(window);
            var after = (
                Texts: DesktopFixture.Texts(window).ToList(),
                Same: ReferenceEquals(Named<TextBox>(window, "Draft").FindAncestorOfType<UserControl>()!.DataContext, model),
                NewView: !ReferenceEquals(Named<TextBox>(window, "Draft").FindAncestorOfType<UserControl>(), pageView),
                KeptLayout: ReferenceEquals(window.Content, layout),
                Offset: Viewer(window).Offset.Y,
                Focused: (TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement() as Control)?.Name,
                Draft: Named<TextBox>(window, "Draft").Text);
            var elsewhere = navigator.Reload("Pages/elsewhere/Page.axaml");
            var shown = window.Content;
            var layoutReloaded = navigator.Reload("Pages/Layout.axaml");
            DesktopFixture.Settle(window);
            var newLayout = !ReferenceEquals(window.Content, shown);
            var everything = navigator.Reload();
            DesktopFixture.Settle(window);
            var texts = DesktopFixture.Texts(window).ToList();
            window.Close();
            return (scrolled, reloaded, after, elsewhere, layoutReloaded, newLayout, everything, texts);
        });

        Assert.Equal(300, result.scrolled, 0.5);
        Assert.True(result.reloaded);
        Assert.Equal(["Shell", "After"], result.after.Texts);
        Assert.True(result.after.Same);
        Assert.True(result.after.NewView);
        Assert.True(result.after.KeptLayout);
        Assert.Equal(300, result.after.Offset, 0.5);
        Assert.Equal("Bound", result.after.Focused);
        Assert.Equal("typed", result.after.Draft);
        Assert.False(result.elsewhere);
        Assert.True(result.layoutReloaded);
        Assert.True(result.newLayout);
        Assert.True(result.everything);
        Assert.Equal(["Shell", "After"], result.texts);
        Assert.Equal(1, loads);
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static ScrollViewer Viewer(Window window) => window.GetVisualDescendants().OfType<ScrollViewer>().First(viewer => viewer.Height is > 119 and < 121);

    [Fact(Timeout = 60_000)]
    [Trait("AC", "DESKTOP-99")]
    public async Task SavedSourcesAndCodeEditsReloadTheWindowAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var project = Directory.CreateTempSubdirectory("aioros-desktop-");
        try
        {
            var file = Path.Combine(project.FullName, "Pages", "reloads", "Page.axaml");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var model = new Reloaded();
            var page = DesktopFixture.Page("/reloads", Source, _ => model);
            var sources = new XamlSources(typeof(DesktopFixture).Assembly, project.FullName);
            sources.Override(Source, Xaml("Compiled"));

            var result = await fixture.OnUiAsync(async () =>
            {
                var (window, navigator) = DesktopFixture.Open(pages: [page], sources: sources);
                await navigator.NavigateAsync("/reloads");
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var reload = new DesktopHotReload(window, navigator, sources, project.FullName);
                var running = reload.RunAsync(stop.Token);
                await reload.Watching.WaitAsync(cancellationToken);

                var saved = NextAsync(reload, cancellationToken);
                await File.WriteAllTextAsync(file, Xaml("Saved"), cancellationToken);
                await saved;
                var afterSave = DesktopFixture.Texts(window).ToList();
                var broken = NextAsync(reload, cancellationToken);
                await File.WriteAllTextAsync(file, Xaml("Broken").Replace("</ScrollViewer>", string.Empty, StringComparison.Ordinal), cancellationToken);
                await broken;
                DesktopFixture.Settle(window);
                var fault = reload.Fault;
                var afterBreak = DesktopFixture.Texts(window).ToList();
                var restored = NextAsync(reload, cancellationToken);
                await File.WriteAllTextAsync(file, Xaml("Saved"), cancellationToken);
                await restored;
                DesktopFixture.Settle(window);
                var afterRestore = (DesktopFixture.Texts(window).ToList(), reload.Fault);
                var fixedSave = NextAsync(reload, cancellationToken);
                await File.WriteAllTextAsync(file, Xaml("Fixed"), cancellationToken);
                await fixedSave;
                var afterFix = (DesktopFixture.Texts(window).ToList(), reload.Fault);
                var layout = window.Content;
                var code = NextAsync(reload, cancellationToken);
                DesktopCodeUpdates.UpdateApplication([typeof(Reloaded)]);
                await code;
                var rebuilt = !ReferenceEquals(window.Content, layout);
                var sameModel = ReferenceEquals(window.GetVisualDescendants().OfType<TextBox>().First().FindAncestorOfType<UserControl>()?.DataContext, model);
                await stop.CancelAsync();
                await running;
                window.Close();
                return (afterSave, fault, afterBreak, afterRestore, afterFix, rebuilt, sameModel);
            });

            Assert.Equal(["Shell", "Saved"], result.afterSave);
            Assert.NotNull(result.fault);
            Assert.Contains("Pages/reloads/Page.axaml", result.fault, StringComparison.Ordinal);
            Assert.Contains("Line", result.fault, StringComparison.Ordinal);
            Assert.Contains(result.fault, result.afterBreak);
            Assert.Contains("Saved", result.afterBreak);
            Assert.Equal(["Shell", "Saved"], result.afterRestore.Item1);
            Assert.Null(result.afterRestore.Fault);
            Assert.Equal(["Shell", "Fixed"], result.afterFix.Item1);
            Assert.Null(result.afterFix.Fault);
            Assert.True(result.rebuilt);
            Assert.True(result.sameModel);
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    private static Task<string?> NextAsync(DesktopHotReload reload, CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, HotReloaded reloaded)
        {
            reload.Reloaded -= Handler;
            done.TrySetResult(reloaded.Source);
        }

        reload.Reloaded += Handler;
        return done.Task.WaitAsync(cancellationToken);
    }
}
