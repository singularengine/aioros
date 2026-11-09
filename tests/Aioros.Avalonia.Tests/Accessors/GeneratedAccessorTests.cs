using System.Collections.Immutable;
using System.Windows.Input;
using Aioros.Avalonia.Plans;
using Aioros.Avalonia.Tests.Controls;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia.Data.Converters;

namespace Aioros.Avalonia.Tests.Accessors;

public sealed class GeneratedAccessorTests(EngineFixture fixture)
{
    [Fact]
    [Trait("AC", "PRODUCTION-13")]
    public async Task PlansReadAndWriteThroughGeneratedAccessorsAndFallBackToRuntimeDelegatesAsync()
    {
        var marker = new Marker();
        MemberAccessors.Register(
        [
            new(typeof(Typed), nameof(Typed.Name), static target => "generated:" + ((Typed)target).Name)
            {
                ValueType = static () => typeof(string),
                Set = static (target, value) => ((Typed)target).Name = "set:" + value,
            },
            new(typeof(Typed), nameof(Typed.Inner), static target => ((Typed)target).Inner),
            new(typeof(Inner), nameof(Inner.Text), static _ => "generated inner"),
            new(typeof(Typed), nameof(Typed.Blank), static _ => string.Empty),
            new(typeof(Typed), nameof(Typed.Go), _ => marker),
        ]);
        var typed = new Typed();
        var plain = new Plain();
        var generated = new AdapterSlotResolver();
        var runtime = new AdapterSlotResolver(generated: false);
        var converter = "static:" + typeof(StringConverters).AssemblyQualifiedName + ":" + nameof(StringConverters.IsNotNullOrEmpty);
        var converted = new SlotSpec("convert", nameof(Typed.Blank), converter) { Values = [string.Empty, "null", typeof(bool).AssemblyQualifiedName!, string.Empty] };

        var nested = generated.Getter(SlotSpec.Bound("Inner.Text"))(typed);
        var nestedAtRunTime = runtime.Getter(SlotSpec.Bound("Inner.Text"))(typed);
        var outer = OuterScopes.Swap(ImmutableStack.Create<object?>(typed));
        var fromOuter = generated.Getter(SlotSpec.Bound("^1.Name"))(null);
        OuterScopes.Swap(outer);
        var blank = generated.Getter(converted)(typed);
        var blankAtRunTime = runtime.Getter(converted)(typed);
        generated.Binding(new SlotSpec("text", nameof(Typed.Name)))!(typed, "typed");
        runtime.Binding(new SlotSpec("text", nameof(Plain.Name)))!(plain, "written");
        await generated.Command(new SlotSpec("command", nameof(Typed.Go)))!.ExecuteAsync(typed, null);
        var plainName = generated.Getter(SlotSpec.Bound(nameof(Plain.Name)))(plain);
        generated.Binding(new SlotSpec("text", nameof(Plain.Name)))!(plain, "again");
        var coverage = MemberAccessors.Coverage();

        Assert.Equal(("generated inner", "inner"), (nested, nestedAtRunTime));
        Assert.Equal("generated:real", fromOuter);
        Assert.Equal((false, true), (blank, blankAtRunTime));
        Assert.Equal("set:typed", typed.Name);
        Assert.Equal((1, 0), (marker.Runs, typed.Go.Runs));
        Assert.Equal(("written", "again"), (plainName, plain.Name));
        Assert.Contains(typeof(Plain).FullName + ".Name", coverage.Runtime);
        Assert.Contains(typeof(Plain).FullName + ".Name (set)", coverage.Runtime);
        Assert.DoesNotContain(coverage.Runtime, member => member.StartsWith(typeof(Typed).FullName + ".", StringComparison.Ordinal));
        Assert.True(coverage.Generated >= 5);
    }

    [Fact]
    [Trait("AC", "PRODUCTION-17")]
    public async Task PrecompilationCountsTheWritesOfTwoWayFieldsWithoutWritingAsync()
    {
        const string Body = """
            <StackPanel>
              <TextBox Text="{Binding Memo}" />
              <CheckBox IsChecked="{Binding Paid}" />
              <CheckBox IsChecked="{Binding Locked, Mode=OneWay}" />
            </StackPanel>
            """;
        var written = 0;
        MemberAccessors.Register(
        [
            new(typeof(Remittance), nameof(Remittance.Memo), static target => ((Remittance)target).Memo)
            {
                ValueType = static () => typeof(string),
                Set = (_, _) => written++,
            },
            new(typeof(Remittance), nameof(Remittance.Paid), static target => ((Remittance)target).Paid),
        ]);
        var remittance = new Remittance();
        var page = await fixture.CompileAsync(Body, remittance);

        new PlanSlotResolver(AdapterSlotResolver.Fallback).LookUpSetters(page.Plan, [remittance]);
        var coverage = MemberAccessors.Coverage();

        Assert.Equal(0, written);
        Assert.Equal(("memo", true, false), (remittance.Memo, remittance.Paid, remittance.Locked));
        Assert.DoesNotContain(typeof(Remittance).FullName + ".Memo (set)", coverage.Runtime);
        Assert.Contains(typeof(Remittance).FullName + ".Paid (set)", coverage.Runtime);
        Assert.DoesNotContain(typeof(Remittance).FullName + ".Locked (set)", coverage.Runtime);
        Assert.True(coverage is { GeneratedWrites: >= 1, Writes: >= 2 });
    }

    private sealed class Typed
    {
        public string Name { get; set; } = "real";

        public Inner Inner { get; } = new();

        public string Blank { get; } = "not blank";

        public Marker Go { get; } = new();
    }

    private sealed class Inner
    {
        public string Text { get; } = "inner";
    }

    private sealed class Plain
    {
        public string Name { get; set; } = "plain";
    }

    private sealed class Marker : ICommand
    {
        public int Runs { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Runs++;
    }
}
