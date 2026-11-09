using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Generators.Tests.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;

namespace Aioros.Generators.Tests.Plans;

public sealed class SelectorCodeGeneratorTests
{
    private const string Avalonia = """
        namespace Avalonia.Controls
        {
            public class Control { }
        }

        namespace Avalonia.Controls.Templates
        {
            public interface ITemplate<TParam, TControl>
            {
                TControl Build(TParam param);
            }

            public interface IDataTemplate : ITemplate<object?, Avalonia.Controls.Control?>
            {
                bool Match(object? data);
            }
        }
        """;

    private const string Selectors = """
        using System.Collections.Generic;
        using Avalonia.Controls;
        using Avalonia.Controls.Templates;

        namespace App.Views;

        public enum Level { Low, High }

        public sealed record Note(string Kind, Level Level);

        public sealed class Picker : IDataTemplate
        {
            public Dictionary<string, IDataTemplate> Templates { get; } = new();

            public Dictionary<Level, IDataTemplate> Levels { get; } = new();

            public IDataTemplate? DefaultTemplate { get; set; }

            public Control? Build(object? param)
            {
                if (param is not Note note)
                {
                    return null;
                }

                if (note.Kind == "level")
                {
                    return Levels.TryGetValue(note.Level, out var level) ? level.Build(param) : null;
                }

                return Templates.TryGetValue(note.Kind, out var template) ? template.Build(param) : DefaultTemplate?.Build(param);
            }

            public bool Match(object? data) => data is Note;
        }

        public sealed class Prefixed(string prefix) : IDataTemplate
        {
            public Dictionary<string, IDataTemplate> Templates { get; } = new();

            public Control? Build(object? param) => Templates.TryGetValue(prefix, out var template) ? template.Build(param) : null;

            public bool Match(object? data) => true;
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-111")]
    public void TemplateSelectorsAreRebuiltAndRunByGeneratedCode()
    {
        var assembly = new GeneratorHarness().Source(Avalonia).Source(Selectors).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var all = (ImmutableArray<SelectorCode>)assembly.GetType("Aioros.Generated.AiorosSelectors")!.GetProperty("All")!.GetValue(null)!;
        PlanSelectors.Register(all);
        var picker = assembly.GetType("App.Views.Picker")!;
        var note = assembly.GetType("App.Views.Note")!;
        var level = assembly.GetType("App.Views.Level")!;
        object Note(string kind, string value = "Low") => Activator.CreateInstance(note, kind, Enum.Parse(level, value))!;
        var spec = PlanSelectors.Spec(picker.AssemblyQualifiedName!, [new("Templates", "info"), new("Templates", "warn"), new("Levels", "High"), new("DefaultTemplate", string.Empty)]);
        var choose = new PlanSlotResolver().Chooser(spec);
        var json = PlanSerializer.TryWrite([new SelectorNode(choose, [[new LiteralNode("i")], [new LiteralNode("w")], [new LiteralNode("h")], [new LiteralNode("d")]]) { Selector = spec }]).Json!;
        var nodes = PlanSerializer.Read(json, new PlanSlotResolver());
        string Render(object model)
        {
            var writer = new ArrayBufferWriter<byte>();
            new RenderPlan([], nodes, []).Render(writer, [model]);
            return Encoding.UTF8.GetString(writer.WrittenSpan);
        }

        Assert.Equal(["App.Views.Picker"], all.Select(selector => selector.Name));
        Assert.Equal((0, 1, 2, 3), (choose(Note("info")), choose(Note("warn")), choose(Note("level", "High")), choose(Note("other"))));
        Assert.Equal((-1, -1), (choose(Note("level")), choose("not a note")));
        Assert.Equal(("w", "h", "d", string.Empty), (Render(Note("warn")), Render(Note("level", "High")), Render(Note("x")), Render(42)));
        Assert.Null(PlanSelectors.Chooser(PlanSelectors.Spec(picker.AssemblyQualifiedName!, [new("Missing", "x")])));
        Assert.Null(PlanSelectors.Chooser(PlanSelectors.Spec("App.Views.Prefixed, App", [new("Templates", "x")])));
    }
}
