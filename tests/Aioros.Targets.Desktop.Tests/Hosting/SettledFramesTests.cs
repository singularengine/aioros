using Aioros.Targets.Desktop.Hosting;

namespace Aioros.Targets.Desktop.Tests.Hosting;

public sealed class SettledFramesTests
{
    [Fact]
    [Trait("AC", "DESKTOP-94")]
    public async Task ACaptureKeepsTheFirstFrameThePageSettlesOnAsync()
    {
        int[] animated = [10, 40, 70, 90, 100, 100, 100, 100, 100, 100, 100, 100];
        var (settled, moves, disposed) = await RunAsync(index => index < animated.Length ? animated[index] : 100);
        var (endless, endlessMoves, _) = await RunAsync(index => index);

        Assert.Equal((100, 4, 5 + SettledFrames.Stable), (settled!.Value, settled.Index, moves));
        Assert.Equal(moves - 1, disposed);
        Assert.Equal((SettledFrames.Limit - 1, SettledFrames.Limit), (endless!.Value, endlessMoves));
    }

    [Fact]
    [Trait("AC", "DESKTOP-95")]
    public async Task FramesTakenWhileAnimationsArePendingNeverSettleAsync()
    {
        int[] delayed = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 30, 60, 90, 100];
        var (settled, moves, disposed) = await RunAsync(index => index < delayed.Length ? delayed[index] : 100, index => index < delayed.Length);
        var (endless, endlessMoves, _) = await RunAsync(static _ => 0, static _ => true);

        Assert.Equal((100, 13, 14 + SettledFrames.Stable), (settled!.Value, settled.Index, moves));
        Assert.Equal(moves - 1, disposed);
        Assert.Equal((0, SettledFrames.Limit - 1, SettledFrames.Limit), (endless!.Value, endless.Index, endlessMoves));
    }

    private static async Task<(Frame? Frame, int Moves, int Disposed)> RunAsync(Func<int, int> pixels, Func<int, bool>? pending = null)
    {
        var moves = 0;
        var frames = new List<Frame>();
        var kept = await SettledFrames.WaitAsync(
            () =>
            {
                var frame = new Frame(frames.Count, pixels(frames.Count));
                frames.Add(frame);
                return frame;
            },
            static (a, b) => a.Value == b.Value,
            () => pending?.Invoke(frames.Count - 1) ?? false,
            () =>
            {
                moves++;
                return Task.CompletedTask;
            });
        return (kept, moves, frames.Count(frame => frame.Disposed));
    }

    private sealed class Frame(int index, int value) : IDisposable
    {
        public int Index => index;

        public int Value => value;

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
