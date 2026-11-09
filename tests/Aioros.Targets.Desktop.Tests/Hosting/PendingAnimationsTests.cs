using Aioros.Targets.Desktop.Hosting;
using Aioros.Targets.Desktop.Tests.Navigation;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class PendingAnimationsTests(DesktopFixture fixture)
{
    [Fact]
    [Trait("AC", "DESKTOP-96")]
    public async Task AvaloniaReportsDelayedAnimationsAndRequestedFramesAsPendingAsync()
    {
        var states = await fixture.OnUiAsync(() =>
        {
            var target = new Border();
            var window = new Window { Width = 200, Height = 100, Content = target };
            window.Show();
            var idle = Settle();
            target.Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Delay = TimeSpan.FromMilliseconds(400), Duration = TimeSpan.FromMilliseconds(300) }];
            target.Opacity = 0;
            Render();
            var delayed = PendingAnimations.Active;
            target.Transitions = null;
            var cancelled = Settle();
            window.RequestAnimationFrame(static _ => { });
            var requested = PendingAnimations.Active;
            var drawn = Settle();
            window.Close();
            return Task.FromResult((idle, delayed, cancelled, requested, drawn));
        });

        Assert.Equal((false, true, false, true, false), states);
    }

    private static bool Settle()
    {
        for (var frame = 0; frame < 10 && PendingAnimations.Active; frame++)
        {
            Render();
        }

        return PendingAnimations.Active;
    }

    private static void Render()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
