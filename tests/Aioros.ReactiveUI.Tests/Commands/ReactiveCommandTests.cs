using Aioros.Avalonia.Commands;
using Aioros.ReactiveUI.Tests.Models;
using ReactiveUI;

namespace Aioros.ReactiveUI.Tests.Commands;

public sealed class ReactiveCommandTests
{
    [Fact]
    [Trait("AC", "REACTIVE-01")]
    public async Task ReactiveCommandsAreAwaitedUntilTheyEndAsync()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = false;
        var save = ReactiveCommand.CreateFromTask<string>(async note =>
        {
            await gate.Task;
            ended = note == "saved";
        });
        var screen = new Screen();
        var album = new AlbumViewModel(3, screen);
        var open = ReactiveCommand.CreateFromObservable(() => screen.Router.Navigate.Execute(album));
        var failing = ReactiveCommand.CreateFromTask(() => Task.FromException(new InvalidOperationException("The store is closed.")));

        var run = AwaitedCommands.RunAsync(save, "saved");
        var pending = !run.IsCompleted;
        gate.SetResult();
        await run;
        await AwaitedCommands.RunAsync(open, null);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitedCommands.RunAsync(failing, null).AsTask());

        Assert.True(pending);
        Assert.True(ended);
        Assert.Equal([album], screen.Router.NavigationStack);
        Assert.Equal("The store is closed.", failure.Message);
    }

    private sealed class Screen : IScreen
    {
        public RoutingState Router { get; } = new();
    }
}
