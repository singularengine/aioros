using Aioros.Avalonia.Hosting;
using Aioros.ReactiveUI.Tests.Desktop;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;

[assembly: AssemblyFixture(typeof(DesktopFixture))]

namespace Aioros.ReactiveUI.Tests.Desktop;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public sealed class DesktopFixture : IAsyncLifetime
{
    private AvaloniaHost _host = null!;

    public async ValueTask InitializeAsync() => _host = await AvaloniaHost.StartAsync(typeof(TestApp));

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    public Task<T> OnUiAsync<T>(Func<Task<T>> action) => _host.InvokeAsync(action, TestContext.Current.CancellationToken);

    public static IReadOnlyList<string> Texts(Window window)
    {
        Settle(window);
        return [.. window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).Where(text => text.Length > 0)];
    }

    public static void Click(Window window, string text)
    {
        Settle(window);
        var target = window.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
