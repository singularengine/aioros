using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Windows.Input;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Media;

namespace Aioros.Avalonia.Tests.Accessors;

public sealed class RuntimeSlotTests
{
    private static readonly SlotSpec[] Getters =
    [
        SlotSpec.Bound("Name"),
        SlotSpec.Bound("Child.Text"),
        SlotSpec.Bound("^1.Name"),
        new("self"),
        new("one", "Count"),
        new("flag", "Hidden") { Values = ["not", "true", string.Empty] },
        new("flag", "Text") { Values = [string.Empty, "false", "complement"] },
        new("flag", "Count") { Values = [string.Empty, "true", string.Empty] },
        new("enabled", "Flag", "Save") { Values = ["path", "Name"] },
        new("enabled", null, "Save") { Values = ["text", "go", "not"] },
        new("test", "Hidden", "null"),
        new("test", "Index", "first"),
        new("first-page", "Index"),
        new("last-page", "Index", "#3"),
        new("range", "Pages"),
        new("errors", "Name"),
    ];

    private static readonly string[] AvaloniaFormats = ["color", "points"];

    private static readonly object?[] Angles = [0, 30.0, -12.25, 89.5f, "30"];

    private static readonly SlotSpec[] Formats =
    [
        new("css-number"), new("invariant"), new("string-format", null, "{0:N1} items"), new("identity"), new("access"),
        new("offset", null, "1.5"), new("progress-text") { Values = ["0", "200", "{1:0}% of {3}"] }, new("error-text"),
        new("except", null, "none"), new("date"), new("time", null, "seconds"), new("time", null, "minutes"), new("icon"),
        new("color"), new("points"), new("tangent"),
    ];

    private static readonly object?[] Values =
    [
        null, "_Save __it", "none", "check", "#3a7", "#3A7BfF", "#3A7BgF", "#80123456", "#12345", 7, 12.5m, 30.0, true, new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Unspecified),
        new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)), new TimeSpan(1, 2, 3), new InvalidOperationException("broken"),
        new List<Point> { new(1.234, 5.678), new(9, 10.005) }, new List<Point>(),
    ];

    [Fact]
    [Trait("AC", "PRODUCTION-15")]
    public async Task TheRuntimeResolverReadsPlansAsTheAdapterDoesAndHandsTheRestToItsFallbackAsync()
    {
        Register();
        var runtime = new PlanSlotResolver();
        var adapter = new AdapterSlotResolver(generated: false);
        var shop = new Shop();
        var outer = OuterScopes.Swap(ImmutableStack.Create<object?>(shop));
        var read = Getters.Select(spec => (Show(runtime.Getter(spec)(shop)), Show(adapter.Getter(spec)(shop)))).ToList();
        OuterScopes.Swap(outer);
        var formatted = Formats.SelectMany(format => Values.Where(value => format.Kind != "color" || value is null || value is string text && text.StartsWith('#'))
            .Select(value => (format.Kind, Try(() => runtime.Formatter(format)!(value)), Try(() => adapter.Formatter(format)!(value))))).ToList();
        var spanned = Formats.Select(format => (Format: format, Span: runtime.SpanFormatter(format))).Where(entry => entry.Span is not null)
            .SelectMany(entry => Values.Where(value => entry.Format.Kind != "color" || value is null || value is string text && text.StartsWith('#')).Select(value => (entry.Format.Kind, Text: Spanned(entry.Span!, value), Formatted: Try(() => runtime.Formatter(entry.Format)!(value)))))
            .ToList();
        var encoded = Formats.Where(format => runtime.Utf8Formatter(format) is not null).Select(format => format.Kind).ToList();
        var icons = new object?[] { "check", "status-done", "no-such-icon", null, 3 }
            .Select(value => (Bytes: System.Text.Encoding.UTF8.GetString(runtime.Utf8Formatter(new SlotSpec("icon"))!(value)), Markup: runtime.Formatter(new SlotSpec("icon"))!(value))).ToList();
        var avalonia = AdapterSlotResolver.Fallback;
        var referenced = Values.SelectMany(value => AvaloniaFormats.Where(kind => kind == "points" ? value is not IEnumerable || value is string or List<Point> : value is null || value is string text && text.StartsWith('#')).Select(kind => (kind, runtime.Formatter(new SlotSpec(kind))!(value), avalonia.Formatter(new SlotSpec(kind), runtime.Paths)(value))))
            .Concat(Angles.Select(value => ("tangent", runtime.Formatter(new SlotSpec("tangent"))!(value), CssText.Num(Math.Tan(Matrix.ToRadians(value is IConvertible and not string ? Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) : 0))))))
            .ToList();
        var chosen = Choices().Select(spec => Enumerable.Range(0, 4).Select(index => (runtime.Predicate(spec)(shop, index, shop.Items.ElementAtOrDefault(index)), adapter.Predicate(spec)(shop, index, shop.Items.ElementAtOrDefault(index)))).ToList()).ToList();
        var (posted, adapted) = (new Shop(), new Shop());
        await PostAsync(runtime, posted);
        await PostAsync(adapter, adapted);

        var recording = new Recording();
        var fallen = new PlanSlotResolver(recording);
        fallen.Getter(new SlotSpec("is", "Count", "Left") { Values = ["T", string.Empty, string.Empty, string.Empty] });
        fallen.Getter(new SlotSpec("tag", "Name", "is") { Values = ["T", "a"] });
        fallen.Getter(new SlotSpec("convert", "Name", "static:Unknown, App:Member") { Values = [string.Empty, "null", typeof(string).AssemblyQualifiedName!, string.Empty] });
        fallen.Getter(new SlotSpec("multi", "Name|Text", "type:Unknown, App") { Values = [string.Empty, "null", typeof(string).AssemblyQualifiedName!] });
        fallen.Formatter(new SlotSpec("thickness"));
        fallen.Formatter(new SlotSpec("chart", null, "{}"));
        var brush = fallen.Formatter(new SlotSpec("color"))!(Brushes.Red);
        MemberAccessors.Register([.. Members<Unregistered>(("SaveCommand", unregistered => unregistered.SaveCommand, null, null))]);
        var named = fallen.Formatter(new SlotSpec("color"))!("red");
        var unlisted = fallen.Getter(SlotSpec.Bound("Unlisted"))(new Unregistered());
        await fallen.Command(new SlotSpec("command", "SaveCommand"))!.ExecuteAsync(new Unregistered(), null);
        var type = fallen.TypeOf("App.Unknown, App");

        Assert.All(read, pair => Assert.Equal(pair.Item2, pair.Item1));
        Assert.All(formatted, entry => Assert.Equal((entry.Kind, entry.Item3), (entry.Kind, entry.Item2)));
        Assert.Equal(["color", "css-number", "identity"], spanned.Select(entry => entry.Kind).Distinct().Order(StringComparer.Ordinal));
        Assert.All(spanned.Where(entry => entry.Text is not null), entry => Assert.Equal((entry.Kind, entry.Formatted), (entry.Kind, entry.Text)));
        Assert.Contains(("color", "#3a7bff"), spanned.Select(entry => (entry.Kind, entry.Text)));
        Assert.Contains(("color", (string?)null), spanned.Select(entry => (entry.Kind, entry.Text)));
        Assert.Equal(["icon"], encoded);
        Assert.All(icons, icon => Assert.Equal(icon.Markup, icon.Bytes));
        Assert.Contains(icons, icon => icon.Bytes.StartsWith("<path", StringComparison.Ordinal));
        Assert.All(referenced, entry => Assert.Equal((entry.Item1, entry.Item3), (entry.Item1, entry.Item2)));
        Assert.All(chosen, pairs => Assert.All(pairs, pair => Assert.Equal(pair.Item2, pair.Item1)));
        Assert.Equal(adapted.Snapshot(), posted.Snapshot());
        Assert.Equal(("posted", 1), (posted.Saved, posted.Runs));
        Assert.Equal(
            ["getter:is", "getter:tag", "getter:convert", "getter:multi", "formatter:thickness", "formatter:chart", "format:color:ImmutableSolidColorBrush", "format:color:String", "member:Unlisted", "command:Never", "type:App.Unknown, App"],
            recording.Calls);
        Assert.Equal(("fallback", "fallback", "fallback", typeof(Recording)), (brush, named, unlisted, type));
        Assert.Contains("'is' slot", Assert.Throws<InvalidOperationException>(() => runtime.Getter(new SlotSpec("is", "Count", "Left"))).Message, StringComparison.Ordinal);
        Assert.Contains("thickness", Assert.Throws<InvalidOperationException>(() => runtime.Formatter(new SlotSpec("thickness"))).Message, StringComparison.Ordinal);
        Assert.Contains(typeof(Unregistered).FullName + ".Unlisted", Assert.Throws<InvalidOperationException>(() => runtime.Getter(SlotSpec.Bound("Unlisted"))(new Unregistered())).Message, StringComparison.Ordinal);
    }

    private static IEnumerable<SlotSpec> Choices() =>
    [
        new("index-path", "Index"), new("page", "Index"), new("item-path", "Name"), new("item-text", "Name") { Values = ["a", "Shop", "c"] },
        new("indexes") { Values = ["0", "2"] },
    ];

    private static async Task PostAsync(IFieldResolver resolver, Shop shop)
    {
        (string Kind, string Path, string? Argument, string[] Values, string? Posted)[] fields =
        [
            ("text", "Name", null, [], "posted"),
            ("check", "Flag", "on", ["two"], null),
            ("check", "Hidden", "on", ["three"], "false"),
            ("toggle", "Open", "on", [], "on"),
            ("number", "Price", null, ["0", "100", "clip"], "250.5"),
            ("slider", "Ratio", null, ["0", "10"], "4.25"),
            ("date", "When", null, [], "2027-03-04"),
            ("date-offset", "Offset", null, [], "2027-05-06"),
            ("time", "Time", null, [], "07:08:09"),
            ("combo", "Choice", "Items", [], "2"),
            ("combo", "Fixed", null, ["x", "y"], "1"),
            ("choice-index", "Index", "Items", [], "2"),
            ("choice-item", "Picked", "Items", [], "1"),
            ("page", "Page", "Pages", [], "3"),
        ];
        foreach (var (kind, path, argument, values, posted) in fields)
        {
            resolver.Binding(new SlotSpec(kind, path, argument) { Values = [.. values] })!(shop, posted);
        }

        var command = resolver.Command(new SlotSpec("command", "Save", "Name"))!;
        await command.ExecuteAsync(shop, command.Parameter(shop));
    }

    private static string? Spanned(ValueSpanFormatter format, object? value)
    {
        var text = new char[PlanValues.SpanLength];
        try
        {
            return format(value, text, out var written) ? new string(text, 0, written) : null;
        }
        catch (Exception error) when (error is FormatException or InvalidCastException)
        {
            return "throws " + error.GetType().Name;
        }
    }

    private static string Try(Func<string> format)
    {
        try
        {
            return format();
        }
        catch (Exception error) when (error is FormatException or InvalidCastException)
        {
            return "throws " + error.GetType().Name;
        }
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string text => text,
        IEnumerable sequence => string.Join(",", sequence.Cast<object?>().Select(Show)),
        _ => value + ":" + value.GetType().Name,
    };

    private static void Register()
    {
        PlanCommands.Register([new CommandShape(typeof(Run), static (command, parameter) =>
        {
            command.Execute(parameter);
            return ValueTask.CompletedTask;
        }) { Exact = true }]);
        MemberAccessors.Register(
        [
            .. Members<Shop>(
                ("Name", shop => shop.Name, (shop, value) => shop.Name = (string)value!, typeof(string)),
                ("Text", shop => shop.Text, null, null),
                ("Count", shop => shop.Count, null, null),
                ("Flag", shop => shop.Flag, (shop, value) => shop.Flag = (bool)value!, typeof(bool)),
                ("Hidden", shop => shop.Hidden, (shop, value) => shop.Hidden = (bool?)value, typeof(bool?)),
                ("Open", shop => shop.Open, (shop, value) => shop.Open = (bool)value!, typeof(bool)),
                ("Price", shop => shop.Price, (shop, value) => shop.Price = (decimal?)value, typeof(decimal?)),
                ("Ratio", shop => shop.Ratio, (shop, value) => shop.Ratio = (double)value!, typeof(double)),
                ("When", shop => shop.When, (shop, value) => shop.When = (DateTime?)value, typeof(DateTime?)),
                ("Offset", shop => shop.Offset, (shop, value) => shop.Offset = (DateTimeOffset?)value, typeof(DateTimeOffset?)),
                ("Time", shop => shop.Time, (shop, value) => shop.Time = (TimeSpan?)value, typeof(TimeSpan?)),
                ("Choice", shop => shop.Choice, (shop, value) => shop.Choice = (string?)value, typeof(string)),
                ("Fixed", shop => shop.Fixed, (shop, value) => shop.Fixed = (string?)value, typeof(string)),
                ("Picked", shop => shop.Picked, (shop, value) => shop.Picked = (string?)value, typeof(string)),
                ("Items", shop => shop.Items, null, null),
                ("Index", shop => shop.Index, (shop, value) => shop.Index = (int)value!, typeof(int)),
                ("Page", shop => shop.Page, (shop, value) => shop.Page = (int)value!, typeof(int)),
                ("Pages", shop => shop.Pages, null, null),
                ("Child", shop => shop.Child, null, null),
                ("Save", shop => shop.Save, null, null)),
            .. Members<Child>(("Text", child => child.Text, null, null)),
            .. Members<Point>(("X", point => point.X, null, null), ("Y", point => point.Y, null, null)),
        ]);
    }

    private static IEnumerable<MemberAccessor> Members<T>(params (string Name, Func<T, object?> Get, Action<T, object?>? Set, Type? Value)[] members) =>
        members.Select(member => new MemberAccessor(typeof(T), member.Name, target => member.Get((T)target))
        {
            ValueType = member.Value is { } value ? () => value : null,
            Set = member.Set is { } set ? (target, value) => set((T)target, value) : null,
        });

    private sealed class Shop : INotifyDataErrorInfo
    {
        public Shop() => Save = new Run(this);

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
        {
            add { }
            remove { }
        }

        public string Name { get; set; } = "Shop";

        public string Text { get; } = "true";

        public int Count { get; } = 3;

        public bool Flag { get; set; } = true;

        public bool? Hidden { get; set; }

        public bool Open { get; set; }

        public decimal? Price { get; set; } = 1;

        public double Ratio { get; set; }

        public DateTime? When { get; set; }

        public DateTimeOffset? Offset { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3));

        public TimeSpan? Time { get; set; }

        public string? Choice { get; set; }

        public string? Fixed { get; set; }

        public string? Picked { get; set; }

        public List<string> Items { get; } = ["a", "b", "c"];

        public int Index { get; set; } = 1;

        public int Page { get; set; }

        public int Pages { get; } = 4;

        public Child Child { get; } = new();

        public ICommand Save { get; }

        public string? Saved { get; set; }

        public int Runs { get; set; }

        public bool HasErrors => true;

        public IEnumerable GetErrors(string? propertyName) => propertyName == "Name" ? new[] { "required" } : Array.Empty<string>();

        public string Snapshot() => string.Join("|", Name, Flag, Hidden, Open, Price, Ratio, When, Offset, Time, Choice, Fixed, Picked, Index, Page, Saved, Runs);
    }

    private sealed class Child
    {
        public string Text { get; } = "child";
    }

    private sealed class Run(Shop shop) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => !Equals(parameter, "blocked");

        public void Execute(object? parameter)
        {
            shop.Saved = (string?)parameter;
            shop.Runs++;
        }
    }

    private sealed class Unregistered
    {
        public string Unlisted { get; } = "real";

        public ICommand SaveCommand { get; } = new Never();
    }

    private sealed class Never : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private sealed class Recording : ISlotFallback
    {
        public List<string> Calls { get; } = [];

        public Func<object, object?> Getter(Type owner, string member)
        {
            Calls.Add("member:" + member);
            return static _ => "fallback";
        }

        public ValueGetter Getter(SlotSpec spec, MemberPaths paths)
        {
            Calls.Add("getter:" + spec.Kind);
            return static _ => "fallback";
        }

        public (Type ValueType, Action<object, object?> Set) Setter(Type owner, string member)
        {
            Calls.Add("setter:" + member);
            return (typeof(object), static (_, _) => { });
        }

        public ValueFormatter Formatter(SlotSpec spec, MemberPaths paths)
        {
            Calls.Add("formatter:" + spec.Kind);
            return static _ => "fallback";
        }

        public string Format(SlotSpec spec, object value)
        {
            Calls.Add("format:" + spec.Kind + ":" + value.GetType().Name);
            return "fallback";
        }

        public Func<ICommand, object?, ValueTask> Command(Type type)
        {
            Calls.Add("command:" + type.Name);
            return static (_, _) => ValueTask.CompletedTask;
        }

        public Type Type(string name)
        {
            Calls.Add("type:" + name);
            return typeof(Recording);
        }
    }
}
