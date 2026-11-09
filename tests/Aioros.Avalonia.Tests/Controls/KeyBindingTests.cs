using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class KeyBindingTests(EngineFixture fixture)
{
    private const string Body = """
        <UserControl.KeyBindings>
          <KeyBinding Gesture="Ctrl+S" Command="{Binding SayHello}" />
        </UserControl.KeyBindings>
        <StackPanel>
          <StackPanel.KeyBindings>
            <KeyBinding Gesture="Ctrl+Shift+A" Command="{Binding ArchiveAsync}" CommandParameter="30" />
          </StackPanel.KeyBindings>
          <TextBox Text="{Binding Name}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "BUTTONS-97")]
    public async Task KeyBindingsAreWrittenAsHiddenCommandButtonsAsync()
    {
        var page = await fixture.PlansLikeAvaloniaAsync(Body, new Greeter(), new Greeter { Name = "Ada" });
        var unbound = await fixture.CompileAsync("""
            <StackPanel>
              <StackPanel.KeyBindings><KeyBinding Gesture="Ctrl+Q" Command="{x:Static m:KeyCommands.Quit}" /></StackPanel.KeyBindings>
              <TextBlock Text="q" />
            </StackPanel>
            """, new Greeter());
        var off = EngineFixture.Body(page, new Greeter());
        var on = EngineFixture.Body(page, new Greeter { Name = "Ada" });

        Assert.Matches("^<button type=\"submit\" name=\"_btn\" value=\"0\\|\" hidden data-aioros-key=\"ctrl\\+KeyS\" aria-keyshortcuts=\"Control\\+S\" disabled></button>", Strip(off));
        Assert.Matches("^<button type=\"submit\" name=\"_btn\" value=\"0\\|\" hidden data-aioros-key=\"ctrl\\+KeyS\" aria-keyshortcuts=\"Control\\+S\"></button>", Strip(on));
        Assert.Matches("<button type=\"submit\" name=\"_btn\" value=\"1\\|30\" hidden data-aioros-scope data-aioros-key=\"ctrl\\+shift\\+KeyA\" aria-keyshortcuts=\"Control\\+Shift\\+A\"></button><div[^>]*><input", on);
        Assert.False(unbound.Reusable);
        Assert.Contains("KeyBinding.Command", unbound.Failure, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-98")]
    public async Task KeyBindingsRunTheirCommandsWhenPostedAsync()
    {
        var page = await fixture.CompileAsync(Body, new Greeter());
        var (planned, refused, engine, engineRefused) = (new Greeter { Name = "Ada" }, new Greeter(), new Greeter { Name = "Ada" }, new Greeter());

        var archive = await PlanForms.ApplyAsync(page.Plan, [planned], Form("1|30"), new AdapterSlotResolver());
        var hello = await PlanForms.ApplyAsync(page.Plan, [planned], Form("0|"), new AdapterSlotResolver());
        await PlanForms.ApplyAsync(page.Plan, [refused], Form("0|"), new AdapterSlotResolver());
        var engineArchive = await fixture.PostAsync(Body, engine, ("_btn", "1|30"));
        await fixture.PostAsync(Body, engineRefused, ("_btn", "0|"));
        var stale = await PlanForms.ApplyAsync(page.Plan, [new Greeter()], Form("1|31"), new AdapterSlotResolver());
        var staleEngine = new Greeter();
        var engineStale = await fixture.PostAsync(Body, staleEngine, ("_btn", "1|31"));

        Assert.True(page.Reusable, page.Failure);
        Assert.Equal((PostStatus.Rendered, PostStatus.Rendered, PostStatus.Rendered), (archive, hello, engineArchive.Status));
        Assert.Equal(["archived 30", "hello Ada"], planned.Greeted);
        Assert.Equal(["archived 30"], engine.Greeted);
        Assert.Empty(refused.Greeted);
        Assert.Empty(engineRefused.Greeted);
        Assert.Equal((PostStatus.Conflict, PostStatus.Conflict), (stale, engineStale.Status));
        Assert.Empty(staleEngine.Greeted);
    }

    private static string Strip(string html) => html.StartsWith("<div", StringComparison.Ordinal) ? html[(html.IndexOf('>', StringComparison.Ordinal) + 1)..] : html;

    private static ImmutableDictionary<string, string> Form(string button) => ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, button);
}
