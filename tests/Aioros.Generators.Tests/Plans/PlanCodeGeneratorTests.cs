using System.Buffers;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using Aioros.Generators.Tests.Pages;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;

namespace Aioros.Generators.Tests.Plans;

public sealed class PlanCodeGeneratorTests
{
    private const string Avalonia = """
        namespace Avalonia
        {
            public class AvaloniaProperty { public static readonly object UnsetValue = new(); }
            public class Application { }
        }

        namespace Avalonia.Data
        {
            public sealed class BindingNotification(object value)
            {
                public object Value { get; } = value;

                public static object ExtractValue(object o) => o is BindingNotification notification ? notification.Value : o;
            }

            public static class BindingOperations { public static readonly object DoNothing = new(); }
        }

        namespace Avalonia.Data.Converters
        {
            using System;
            using System.Collections.Generic;
            using System.Globalization;

            public interface IValueConverter
            {
                object Convert(object value, Type targetType, object parameter, CultureInfo culture);

                object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
            }

            public interface IMultiValueConverter
            {
                object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture);
            }

            public static class BoolConverters
            {
                public static readonly IValueConverter Not = new Negation();

                private sealed class Negation : IValueConverter
                {
                    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is false;

                    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
                }
            }

            public class StringFormatMultiValueConverter(string format, IMultiValueConverter inner) : IMultiValueConverter
            {
                public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) =>
                    string.Format(culture, format, inner is null ? [.. values] : [inner.Convert(values, targetType, parameter, culture)]);
            }
        }
        """;

    private const string Converters = """
        using System;
        using System.Collections.Generic;
        using System.Globalization;
        using Avalonia.Data.Converters;

        namespace App.Models;

        public sealed class Initials : IValueConverter
        {
            public static readonly Initials Instance = new();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ((string)value)[..1] + parameter + targetType.Name;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
        }

        public sealed class Joined : IValueConverter, IMultiValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => new Avalonia.Data.BindingNotification(value);

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;

            public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) =>
                values.Count == 0 ? Avalonia.Data.BindingOperations.DoNothing : string.Join("+", values);
        }

        public sealed class Unset : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Avalonia.AvaloniaProperty.UnsetValue;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
        }

        public sealed class Tuned : IValueConverter
        {
            public int Step { get; private set; }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
        }
        """;

    private const string Toolkit = """
        using System;
        using System.Threading.Tasks;
        using System.Windows.Input;

        namespace CommunityToolkit.Mvvm.Input
        {
            [AttributeUsage(AttributeTargets.Method)]
            public sealed class RelayCommandAttribute : Attribute { }

            public interface IRelayCommand : ICommand { }

            public interface IAsyncRelayCommand : IRelayCommand { Task ExecuteAsync(object parameter); }

            public sealed class RelayCommand(Action run) : IRelayCommand
            {
                public event EventHandler CanExecuteChanged { add { } remove { } }

                public bool CanExecute(object parameter) => true;

                public void Execute(object parameter) => run();
            }

            public sealed class AsyncRelayCommand(Func<Task> run) : IAsyncRelayCommand
            {
                public event EventHandler CanExecuteChanged { add { } remove { } }

                public bool CanExecute(object parameter) => true;

                public void Execute(object parameter) => _ = run();

                public Task ExecuteAsync(object parameter) => run();
            }
        }
        """;

    private const string Commands = """
        using System;
        using System.Threading.Tasks;
        using System.Windows.Input;
        using CommunityToolkit.Mvvm.Input;

        namespace App.Models;

        public abstract class Gate : ICommand
        {
            public TaskCompletionSource Done { get; } = new();

            public string Log { get; protected set; } = string.Empty;

            public event EventHandler CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter) => Log += "execute:" + parameter;
        }

        public sealed class Valued : Gate
        {
            public async ValueTask<int> ExecuteAsync(object parameter)
            {
                await Done.Task;
                Log += "valued:" + parameter;
                return 1;
            }
        }

        public interface IHiddenAsync { Task ExecuteAsync(object parameter); }

        public sealed class Hidden : Gate, IHiddenAsync
        {
            Task IHiddenAsync.ExecuteAsync(object parameter) => Done.Task.ContinueWith(_ => Log += "hidden:" + parameter, TaskScheduler.Default);
        }

        public sealed class Observed : Gate, IObservable<int>
        {
            private IObserver<int> _observer;

            public IObservable<int> Execute(string parameter)
            {
                Log += "observed:" + parameter;
                return this;
            }

            public IDisposable Subscribe(IObserver<int> observer)
            {
                _observer = observer;
                Done.Task.ContinueWith(_ => _observer.OnCompleted(), TaskScheduler.Default);
                return null;
            }
        }

        public sealed class Plain : Gate { }

        public sealed class Unlisted : Gate { }

        public sealed partial class Board
        {
            public Valued Valued { get; } = new();
            public Hidden Hidden { get; } = new();
            public Observed Observed { get; } = new();
            public Plain Plain { get; } = new();
            public ICommand Any { get; } = new Unlisted();
            public TaskCompletionSource Saved { get; } = new();
            public string Archived { get; private set; } = string.Empty;
            [RelayCommand] private Task SaveAsync() => Saved.Task;
            [RelayCommand] private void Archive() => Archived = "archived";
        }
        """;

    private const string ToolkitOutput = """
        namespace App.Models;

        public sealed partial class Board
        {
            public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand SaveCommand => new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SaveAsync);
            public CommunityToolkit.Mvvm.Input.IRelayCommand ArchiveCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(Archive);
        }
        """;

    [Fact]
    [Trait("AC", "GENERATOR-56")]
    public void ConvertersAreGeneratedByTheirPlansFileReference()
    {
        var assembly = new GeneratorHarness().Source(Avalonia).Source(Converters).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var plain = new GeneratorHarness().Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var code = (ConverterCode)assembly.GetType("Aioros.Generated.AiorosConverters")!.GetProperty("Code")!.GetValue(null)!;
        PlanConverters.Register(code);
        var resolver = new PlanSlotResolver();
        static string Named(Assembly assembly, string type) => assembly.GetType(type)!.AssemblyQualifiedName!;
        var text = typeof(string).AssemblyQualifiedName!;
        SlotSpec Convert(string reference, string parameter = "", string negate = "") =>
            new("convert", string.Empty, reference) { Values = [parameter, parameter.Length == 0 ? "null" : "text", text, negate] };

        var initials = resolver.Getter(Convert("static:" + Named(assembly, "App.Models.Initials") + ":Instance", "-"))("Ann");
        var created = resolver.Getter(Convert("type:" + Named(assembly, "App.Models.Initials")))("Bob");
        var negated = resolver.Getter(Convert("static:" + Named(assembly, "Avalonia.Data.Converters.BoolConverters") + ":Not", negate: "not"))(false);
        var notified = resolver.Getter(Convert("type:" + Named(assembly, "App.Models.Joined")))("x");
        var unset = resolver.Getter(Convert("type:" + Named(assembly, "App.Models.Unset")))("x");
        var joined = resolver.Getter(new SlotSpec("multi", "|", "type:" + Named(assembly, "App.Models.Joined")) { Values = [string.Empty, "null", text] })("y");
        var nothing = resolver.Getter(new SlotSpec("multi", string.Empty, "type:" + Named(assembly, "App.Models.Joined")) { Values = [string.Empty, "null", text] });
        var formatted = resolver.Getter(new SlotSpec("multi", "|", "format:\n{0} of {1}") { Values = [string.Empty, "null", text] })("z");

        Assert.Equal(
            ["static:App.Models.Initials:Instance", "static:Avalonia.Data.Converters.BoolConverters:Not", "type:App.Models.Initials", "type:App.Models.Joined", "type:App.Models.Unset"],
            code.Converters.Select(converter => converter.Reference).Order(StringComparer.Ordinal));
        Assert.Equal(("A-String", "BString", false), (initials, created, negated));
        Assert.Equal((null, null), (notified, unset));
        Assert.Equal(("y+y", "z of z"), (joined, formatted));
        Assert.Equal("x", nothing("x"));
        Assert.Null(plain.GetType("Aioros.Generated.AiorosConverters"));
    }

    [Fact]
    [Trait("AC", "GENERATOR-101")]
    public void StaticListsAreGeneratedByTheirPlansFileReference()
    {
        const string Lists = """
            using System.Collections.Generic;

            namespace App.Models;

            public sealed class Order(string name)
            {
                public static Order[] All { get; } = [new("Due"), new("Title")];

                public static readonly List<string> Names = ["a", "b"];

                public static string Title { get; } = "Orders";

                public static int Count { get; } = 2;

                public string Name { get; } = name;
            }
            """;
        var assembly = new GeneratorHarness().Source(Avalonia).Source(Lists).Xaml("Pages/Page.axaml", "<TextBlock />").Run().Load();
        var code = (ConverterCode)assembly.GetType("Aioros.Generated.AiorosConverters")!.GetProperty("Code")!.GetValue(null)!;
        PlanConverters.Register(code);
        var owner = assembly.GetType("App.Models.Order")!;
        object? Read(string member) => new PlanSlotResolver().Getter(new SlotSpec("static", null, "static:" + owner.AssemblyQualifiedName + ":" + member))(null);

        Assert.Equal(
            ["static:App.Models.Order:All", "static:App.Models.Order:Names"],
            code.Converters.Select(converter => converter.Reference).Where(reference => reference.StartsWith("static:App.Models.Order:", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Same(owner.GetProperty("All")!.GetValue(null), Read("All"));
        Assert.Same(owner.GetField("Names")!.GetValue(null), Read("Names"));
        Assert.Throws<InvalidOperationException>(() => Read("Title"));
    }

    [Fact]
    [Trait("AC", "GENERATOR-57")]
    public void TypesNamedInXamlAreFoundByNameWithoutLoadingThem()
    {
        const string Models = """
            namespace App.Models;

            public sealed class Board { }
            public sealed class Card { }
            public sealed class Note { }
            """;
        var assembly = new GeneratorHarness()
            .Source(Models)
            .Xaml("Pages/Page.axaml", "<ItemsControl><ItemsControl.ItemTemplate><DataTemplate DataType=\"{x:Type vm:Card}\"><TextBlock /></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>", "Board")
            .Run()
            .Load();
        var all = (ImmutableArray<PlanType>)assembly.GetType("Aioros.Generated.AiorosTypes")!.GetProperty("All")!.GetValue(null)!;
        PlanTypes.Register(all);
        var card = assembly.GetType("App.Models.Card")!;
        var json = $$"""[{"t":"type","c":[{"n":"{{card.AssemblyQualifiedName}}","b":[{"t":"lit","x":"card"}]},{"n":"{{typeof(string).AssemblyQualifiedName}}","b":[{"t":"lit","x":"text"}]}]}]""";
        var nodes = PlanSerializer.Read(json, new PlanSlotResolver());
        string Render(object model)
        {
            var writer = new ArrayBufferWriter<byte>();
            new RenderPlan([], nodes, []).Render(writer, [model]);
            return Encoding.UTF8.GetString(writer.WrittenSpan);
        }

        Assert.Equal(["App.Models.Board", "App.Models.Card"], all.Select(type => type.Name));
        Assert.Equal([assembly.GetType("App.Models.Board"), card], all.Select(type => type.Resolve()));
        Assert.Same(card, PlanTypes.Find(card.AssemblyQualifiedName!));
        Assert.Null(PlanTypes.Find("App.Models.Note, App"));
        Assert.Equal(("card", "text"), (Render(Activator.CreateInstance(card)!), Render("plain")));
    }

    [Fact]
    [Trait("AC", "GENERATOR-58")]
    public async Task CommandShapesAreGeneratedAndAwaitedAsync()
    {
        var assembly = new GeneratorHarness()
            .Source(Toolkit).Source(Commands)
            .Xaml("Pages/Page.axaml", "<TextBlock />", "Board")
            .Generated(ToolkitOutput)
            .Run()
            .Load();
        var all = (ImmutableArray<CommandShape>)assembly.GetType("Aioros.Generated.AiorosCommands")!.GetProperty("All")!.GetValue(null)!;
        PlanCommands.Register(all);
        var board = Activator.CreateInstance(assembly.GetType("App.Models.Board")!)!;
        object Member(string name) => board.GetType().GetProperty(name)!.GetValue(board)!;
        static string Log(object gate) => (string)gate.GetType().GetProperty("Log")!.GetValue(gate)!;
        static TaskCompletionSource Done(object gate) => (TaskCompletionSource)gate.GetType().GetProperty("Done")!.GetValue(gate)!;
        async Task<(bool Pending, string Log)> RunAsync(string name)
        {
            var command = Member(name);
            var run = PlanCommands.RunAsync((ICommand)command, "p", fallback: null).AsTask();
            var pending = !run.IsCompleted;
            Done(command).SetResult();
            await run;
            return (pending, Log(command));
        }

        var save = PlanCommands.RunAsync((ICommand)Member("SaveCommand"), null, fallback: null).AsTask();
        var savePending = !save.IsCompleted;
        ((TaskCompletionSource)Member("Saved")).SetResult();
        await save;
        await PlanCommands.RunAsync((ICommand)Member("ArchiveCommand"), null, fallback: null);
        var valued = await RunAsync("Valued");
        var hidden = await RunAsync("Hidden");
        var observed = await RunAsync("Observed");
        await PlanCommands.RunAsync((ICommand)Member("Plain"), "q", fallback: null);

        Assert.Equal(
            [("App.Models.Hidden", false), ("App.Models.Observed", false), ("App.Models.Valued", false), ("CommunityToolkit.Mvvm.Input.AsyncRelayCommand", false), ("CommunityToolkit.Mvvm.Input.IAsyncRelayCommand", false), ("App.Models.Plain", true), ("CommunityToolkit.Mvvm.Input.RelayCommand", true)],
            all.Select(shape => (shape.Type.FullName, shape.Exact)));
        Assert.True(savePending);
        Assert.Equal("archived", Member("Archived"));
        Assert.Equal((true, "valued:p"), valued);
        Assert.Equal((true, "hidden:p"), hidden);
        Assert.Equal((true, "observed:p"), observed);
        Assert.Equal("execute:q", Log(Member("Plain")));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await PlanCommands.RunAsync((ICommand)Member("Any"), null, fallback: null));
    }

    [Fact]
    [Trait("AC", "GENERATOR-59")]
    public void AvaloniaTypesAreGivenAsFunctions()
    {
        const string Framework = "namespace Avalonia { public class Application { } public struct Point { public double X { get; set; } } }";
        const string Models = """
            namespace App { public sealed class MainApp : Avalonia.Application { } }
            namespace App.Models { public sealed class Chart { public Avalonia.Point Origin { get; set; } } }
            namespace App.Views { public partial class Badge { } }
            """;
        var pages = new GeneratorHarness()
            .Library("Avalonia", Framework)
            .Source(Models)
            .Xaml("Views/Badge.axaml", "<TextBlock />", "Chart", " x:Class=\"App.Views.Badge\"")
            .Xaml("Pages/Page.axaml", "<v:Badge />", "Chart", " xmlns:v=\"using:App.Views\"")
            .Run()
            .Pages(out var assembly);
        var application = assembly.GetType("Aioros.Generated.AiorosPages")!.GetProperty("ApplicationType")!;
        var accessors = (ImmutableArray<MemberAccessor>)assembly.GetType("Aioros.Generated.AiorosAccessors")!.GetProperty("All")!.GetValue(null)!;
        var deferred = (ImmutableArray<DeferredAccessors>)assembly.GetType("Aioros.Generated.AiorosAccessors")!.GetProperty("Deferred")!.GetValue(null)!;
        var origin = accessors.Single(accessor => accessor.Name == "Origin");
        var view = Assert.Single(pages.Single().Views).View;
        var point = origin.ValueType!();
        MemberAccessors.Defer(deferred);
        var x = MemberAccessors.Getter(point, "X")!(Activator.CreateInstance(point)!);

        Assert.Equal(typeof(Func<Type>), application.PropertyType);
        Assert.Equal(assembly.GetType("App.MainApp"), ((Func<Type>)application.GetValue(null)!)());
        Assert.Equal("Avalonia.Point", point.FullName);
        Assert.Equal(assembly.GetType("App.Views.Badge"), view.Type!());
        Assert.DoesNotContain(accessors, accessor => accessor.Owner.Namespace == "Avalonia");
        Assert.Equal(["Avalonia.Point"], deferred.Select(group => group.Owner));
        Assert.Equal(0.0, x);
    }

    [Fact]
    [Trait("AC", "GENERATOR-60")]
    public void ValidatedViewModelsKeepTheirPropertiesForTheTrimmer()
    {
        const string Models = """
            using System;
            using System.Collections;
            using System.ComponentModel;

            namespace App.Models;

            public sealed class Form : INotifyDataErrorInfo
            {
                public string Name { get; set; } = string.Empty;
                public Detail Detail { get; } = new();
                public bool HasErrors => false;
                public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged { add { } remove { } }
                public IEnumerable GetErrors(string propertyName) => Array.Empty<string>();
            }

            public sealed class Detail { public string Text { get; set; } = string.Empty; }
            """;
        var assembly = new GeneratorHarness().Source(Models).Xaml("Pages/Page.axaml", "<TextBlock />", "Form").Run().Load();
        var getter = assembly.GetType("Aioros.Generated.AiorosAccessors")!.GetProperty("All")!.GetMethod!;
        var kept = getter.GetCustomAttributes<System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute>().ToList();

        Assert.Equal([assembly.GetType("App.Models.Form")], kept.Select(attribute => attribute.Type));
        Assert.All(kept, attribute => Assert.Equal(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, attribute.MemberTypes));
    }
}
