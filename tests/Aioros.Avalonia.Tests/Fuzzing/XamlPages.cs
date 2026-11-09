using System.Globalization;
using System.Text;

namespace Aioros.Avalonia.Tests.Fuzzing;

/// <summary>
/// Pages generated from a grammar of the XAML Aioros writes, one per seed: Avalonia's inputs under the Fluent theme with
/// classes, margins and bindings between them, text blocks reading them, the page's own styles and control themes.
/// </summary>
public static class XamlPages
{
    private static readonly string[] Setters =
    [
        """<Setter Property="Foreground" Value="#C2185B" />""", """<Setter Property="Background" Value="#FFF3E0" />""", """<Setter Property="FontWeight" Value="Bold" />""",
        """<Setter Property="FontSize" Value="18" />""", """<Setter Property="Opacity" Value="0.6" />""", """<Setter Property="Margin" Value="6" />""",
        """<Setter Property="BorderBrush" Value="#1565C0" />""", """<Setter Property="Padding" Value="10,4" />""",
    ];

    private static readonly string[] Pseudo = [string.Empty, ":checked", ":pointerover", ":disabled", ":focus"];

    public static string Generate(int seed)
    {
        var random = new Random(seed);
        var inputs = new List<(string Type, string? Name, bool Flag)>();
        var body = new StringBuilder();
        var classes = new List<string>();
        var count = random.Next(1, 5);
        for (var index = 0; index < count; index++)
        {
            var name = $"i{index}";
            var extra = Extra(random, classes, inputs);
            var (type, flag, xaml) = Input(random, name, extra);
            inputs.Add((type, name, flag));
            body.Append(xaml);
            if (random.Next(5) == 0)
            {
                body.Append(Reader(type, name));
            }
        }

        var styles = Styles(random, inputs.Select(static input => input.Type).Distinct().ToList(), classes);
        var themes = random.Next(5) == 0 ? Theme(random, inputs.Select(static input => input.Type).ToList()) : string.Empty;
        return $"<StackPanel Spacing=\"8\">{(themes.Length == 0 ? string.Empty : $"<StackPanel.Resources>{themes}</StackPanel.Resources>")}{(styles.Length == 0 ? string.Empty : $"<StackPanel.Styles>{styles}</StackPanel.Styles>")}{body}</StackPanel>";
    }

    /// <summary>An input's own attributes: a class, a margin, and its enabled or visible state read from an input before it, negated or not.</summary>
    private static string Extra(Random random, List<string> classes, List<(string Type, string? Name, bool Flag)> inputs)
    {
        var extra = new StringBuilder();
        if (random.Next(3) == 0)
        {
            var cls = $"c{random.Next(3)}";
            classes.Add(cls);
            extra.Append(CultureInfo.InvariantCulture, $" Classes=\"{cls}\"");
        }

        if (random.Next(4) == 0)
        {
            extra.Append(CultureInfo.InvariantCulture, $" Margin=\"{random.Next(0, 12)}\"");
        }

        var flags = inputs.Where(static input => input.Flag && input.Name is not null).ToList();
        if (flags.Count > 0 && random.Next(3) == 0)
        {
            var source = flags[random.Next(flags.Count)].Name;
            extra.Append(CultureInfo.InvariantCulture, $" {(random.Next(2) == 0 ? "IsEnabled" : "IsVisible")}=\"{{Binding {(random.Next(3) == 0 ? "!" : string.Empty)}#{source}.IsChecked}}\"");
        }

        return extra.ToString();
    }

    private static (string Type, bool Flag, string Xaml) Input(Random random, string name, string extra) => random.Next(9) switch
    {
        0 => ("CheckBox", true, $"""<CheckBox Name="{name}" Content="Option {name}"{(random.Next(4) == 0 ? " IsThreeState=\"True\"" : string.Empty)}{extra} />"""),
        1 => ("ToggleSwitch", true, $"""<ToggleSwitch Name="{name}" AutomationProperties.Name="Switch {name}"{extra} />"""),
        2 => ("ToggleButton", true, random.Next(2) == 0 ? $"""<ToggleButton Name="{name}" Content="Toggle {name}"{extra} />""" : $"""<ToggleButton Name="{name}" AutomationProperties.Name="Like {name}"{extra}><a:Icon Kind="heart" Size="16" /></ToggleButton>"""),
        3 => ("ListBox", false, $"""<ListBox Name="{name}"{extra}><x:String>One</x:String><x:String>Two</x:String><x:String>Three</x:String></ListBox>"""),
        4 => ("ComboBox", false, $"""<ComboBox Name="{name}" Width="200"{(random.Next(2) == 0 ? " PlaceholderText=\"Pick one\"" : string.Empty)}{(random.Next(3) == 0 ? " HorizontalContentAlignment=\"Left\"" : string.Empty)}{extra}><x:String>Ada</x:String><x:String>Beatrice Longname</x:String><x:String>Cyd</x:String></ComboBox>"""),
        5 => ("Slider", false, $"""<Slider Name="{name}" Minimum="0" Maximum="10" Value="4" Width="200" AutomationProperties.Name="Level {name}"{extra} />"""),
        6 => ("TextBox", false, $"""<TextBox Name="{name}" Text="demo" Width="200" AutomationProperties.Name="Text {name}"{(random.Next(4) == 0 ? " AcceptsReturn=\"True\"" : string.Empty)}{extra} />"""),
        7 => ("NumericUpDown", false, $"""<NumericUpDown Name="{name}" Value="3" Minimum="0" Maximum="9" Width="200" AutomationProperties.Name="Count {name}"{extra} />"""),
        _ => ("TabControl", false, $"""<TabControl Name="{name}"{extra}><TabItem Header="One"><TextBlock Text="First" /></TabItem><TabItem Header="Two"><TextBlock Text="Second" /></TabItem></TabControl>"""),
    };

    private static string Reader(string type, string name) => type switch
    {
        "CheckBox" or "ToggleSwitch" or "ToggleButton" => $$"""<TextBlock Text="{Binding #{{name}}.IsChecked}" />""",
        "TextBox" => $$"""<TextBlock Text="{Binding #{{name}}.Text}" />""",
        "Slider" => $$"""<ProgressBar Value="{Binding #{{name}}.Value}" Maximum="10" Width="200" />""",
        _ => string.Empty,
    };

    private static string Styles(Random random, List<string> types, List<string> classes)
    {
        var styles = new StringBuilder();
        var count = random.Next(0, 4);
        for (var index = 0; index < count; index++)
        {
            var type = types[random.Next(types.Count)];
            var cls = classes.Count > 0 && random.Next(3) == 0 ? "." + classes[random.Next(classes.Count)] : string.Empty;
            var state = type is "CheckBox" or "ToggleSwitch" or "ToggleButton" && random.Next(5) == 0 ? "[IsChecked=True]" : Pseudo[random.Next(Pseudo.Length)];
            styles.Append(CultureInfo.InvariantCulture, $"""<Style Selector="{type}{cls}{state}">{Setters[random.Next(Setters.Length)]}</Style>""");
        }

        return styles.ToString();
    }

    private static string Theme(Random random, List<string> types) => types.Contains("ToggleButton") && random.Next(2) == 0
        ? """<ControlTheme x:Key="{x:Type ToggleButton}" TargetType="ToggleButton"><Setter Property="Background" Value="#EEEEEE" /><Setter Property="Padding" Value="8,4" /><Setter Property="Template"><ControlTemplate><Border Name="box" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}"><ContentPresenter Content="{TemplateBinding Content}" /></Border></ControlTemplate></Setter><Style Selector="^:checked /template/ Border#box"><Setter Property="Background" Value="#C2185B" /></Style></ControlTheme>"""
        : types.Contains("CheckBox")
            ? """<ControlTheme x:Key="{x:Type CheckBox}" TargetType="CheckBox"><Setter Property="Template"><ControlTemplate><StackPanel Orientation="Horizontal" Spacing="6"><Border Name="mark" Width="14" Height="14" BorderBrush="#555555" BorderThickness="1" /><ContentPresenter Content="{TemplateBinding Content}" /></StackPanel></ControlTemplate></Setter><Style Selector="^:checked /template/ Border#mark"><Setter Property="Background" Value="#1565C0" /></Style></ControlTheme>"""
            : string.Empty;
}
