using System.Collections.Immutable;
using Aioros.Avalonia.Commands;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Commands;

public sealed class AwaitedCommandsTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "ENGINE-40")]
    public async Task ToolkitAsyncCommandsAreAwaitedAsync()
    {
        var step = new Step();
        var command = new AsyncRelayCommand<string?>(step.RunAsync);

        var run = AwaitedCommands.RunAsync(command, "note");
        var pending = !run.IsCompleted;
        step.Release();
        await run;

        Assert.True(pending);
        Assert.True(step.Ended);
        Assert.Equal(["note"], step.Calls);
    }

    [Fact]
    [Trait("AC", "ENGINE-41")]
    public async Task AwaitableExecuteAsyncIsAwaitedAsync()
    {
        var explicitStep = new Step();
        var valueStep = new Step();

        var explicitRun = AwaitedCommands.RunAsync(new PrismShapedCommand(explicitStep), "note");
        var valueRun = AwaitedCommands.RunAsync(new ValueTaskCommand(valueStep), 7);
        var pending = (!explicitRun.IsCompleted, !valueRun.IsCompleted);
        explicitStep.Release();
        valueStep.Release();
        await explicitRun;
        await valueRun;

        Assert.Equal((true, true), pending);
        Assert.True(explicitStep.Ended && valueStep.Ended);
        Assert.Equal(["note"], explicitStep.Calls);
        Assert.Equal([7], valueStep.Calls);
    }

    [Fact]
    [Trait("AC", "ENGINE-42")]
    public async Task ObservableExecuteIsAwaitedUntilItCompletesAsync()
    {
        var step = new Step();
        var command = new ReactiveShapedCommand<int, string>(async number =>
        {
            await step.RunAsync(number);
            return "done " + number;
        });

        var run = AwaitedCommands.RunAsync(command, 3);
        var pending = !run.IsCompleted;
        step.Release();
        await run;
        var empty = new Step();
        empty.Release();
        await AwaitedCommands.RunAsync(new ReactiveShapedCommand<int, string>(async number =>
        {
            await empty.RunAsync(number);
            return "empty";
        }), null);

        Assert.True(pending);
        Assert.Equal(1, command.Subscriptions);
        Assert.Equal(["done 3"], command.Results);
        Assert.Equal([0], empty.Calls);
    }

    [Fact]
    [Trait("AC", "ENGINE-43")]
    public async Task PlainCommandsRunSynchronouslyAsync()
    {
        var calls = new List<object?>();
        var refused = new List<object?>();

        var run = AwaitedCommands.RunAsync(new PlainCommand(calls.Add), "note");
        var completed = run.IsCompleted;
        await run;
        await AwaitedCommands.RunAsync(new RelayCommand<string>(calls.Add), "relay");
        await AwaitedCommands.RunAsync(new PlainCommand(refused.Add, enabled: false), "no");
        await AwaitedCommands.RunAsync(null, "none");

        Assert.True(completed);
        Assert.Equal(["note", "relay"], calls);
        Assert.Empty(refused);
    }

    [Fact]
    [Trait("AC", "ENGINE-44")]
    public async Task FailingCommandsFailTheRunAsync()
    {
        var thrown = new InvalidOperationException("thrown");
        var faulted = new InvalidOperationException("faulted");
        var signalled = new InvalidOperationException("signalled");
        var step = new Step();
        step.Fail(faulted);

        var plain = await Assert.ThrowsAsync<InvalidOperationException>(async () => await AwaitedCommands.RunAsync(new PlainCommand(_ => throw thrown), null));
        var task = await Assert.ThrowsAsync<InvalidOperationException>(async () => await AwaitedCommands.RunAsync(new PrismShapedCommand(step), null));
        var throwing = await Assert.ThrowsAsync<InvalidOperationException>(async () => await AwaitedCommands.RunAsync(new AsyncRelayCommand(() => throw thrown), null));
        var observable = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AwaitedCommands.RunAsync(new ReactiveShapedCommand<string, string>(_ => Task.FromException<string>(signalled)), null));

        Assert.Same(thrown, plain);
        Assert.Same(faulted, task);
        Assert.Same(thrown, throwing);
        Assert.Same(signalled, observable);
    }

    private const string Form = """
        <StackPanel>
          <Button Content="Save" Command="{Binding SaveCommand}" CommandParameter="{Binding Token}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "ENGINE-45")]
    public async Task PostsAnswerAfterTheCommandEndsAsync()
    {
        var planStep = new Step();
        var page = await fixture.CompileAsync(Form, new Later(new Step()));
        var fields = PlanForms.Fields(page.Plan, [new Later(new Step())]);
        var button = fields.Fields.Single(field => field.Field?.Kind == "command").Id;
        var form = ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, PlanForms.ButtonValue(button, "note"));

        var applied = PlanForms.ApplyAsync(page.Plan, [new Later(planStep)], form, new AdapterSlotResolver());
        var planPending = !applied.IsCompleted;
        planStep.Release();
        var planStatus = await applied;
        var signal = new Signal();
        var posted = fixture.PostAsync(Form, new Later(new SignalledCommand(signal)), [.. form.Select(pair => (pair.Key, pair.Value))]);
        var first = await Task.WhenAny(posted, signal.Awaited);
        var engineAwaiting = ReferenceEquals(first, signal.Awaited) && !posted.IsCompleted;
        signal.Release();
        var outcome = await posted;

        Assert.True(page.Reusable, page.Failure);
        Assert.True(planPending);
        Assert.Equal(PostStatus.Rendered, planStatus);
        Assert.True(planStep.Ended);
        Assert.True(engineAwaiting);
        Assert.Equal(PostStatus.Rendered, outcome.Status);
        Assert.True(signal.Ended);
        Assert.Equal(["note"], signal.Calls);
    }
}
