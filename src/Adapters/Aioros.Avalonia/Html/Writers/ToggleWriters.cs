using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class ToggleWriters
{
    private const string CheckGlyph = "<svg viewBox=\"0 0 16 16\" width=\"12\" height=\"12\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M2.5 8.5l3.5 3.5 7.5-8\"/></svg>";
    private const string SquareGlyph = "<svg viewBox=\"0 0 16 16\" width=\"8\" height=\"8\" fill=\"currentColor\"><rect width=\"16\" height=\"16\"/></svg>";

    public static void WriteToggleButton(HtmlContext context, ToggleButton toggle, ParentKind parent)
    {
        var (style, own) = StateLooks.OwnStyle(toggle, ToggleLooks.States(toggle), () =>
        {
            var box = context.Box(toggle, parent);
            BoxStyles.Font(box, toggle.FontSize, toggle.FontWeight, null);
            return box;
        });
        var look = ToggleLooks.Of(context, toggle);
        style.Append(look?.Css ?? ToggleStyle(toggle, context.MinHeight(toggle)));
        context.Plan.Literal("<label");
        context.Root(toggle, CssValues.Join(look?.States ?? ToggleStates(context, toggle, "ToggleButton"), own is null ? null : context.Sheet.Rules(own.Rules.Select(static rule => (rule.Selector, string.Concat(rule.Rule.Select(static entry => $"{entry.Key}:{entry.Value};")))))), style, attributes: false);
        context.Plan.Literal(">");
        WriteHiddenCheck(context, toggle, ToggleButton.IsCheckedProperty);
        context.Content(toggle.Content);
        context.Plan.Literal("</label>");
    }

    public static string ToggleStyle(TemplatedControl toggle, double minHeight)
    {
        var style = new StringBuilder();
        BoxStyles.Fill(style, toggle.Background, toggle.BorderBrush, toggle.BorderThickness, toggle.CornerRadius, toggle.Padding);
        if (CssValues.Brush(toggle.Foreground) is { } foreground)
        {
            style.Append(CultureInfo.InvariantCulture, $"color:{foreground};");
        }

        style.Append(minHeight > 0 ? $"min-height:{CssValues.Px(minHeight)};" : string.Empty);
        style.Append("position:relative;display:inline-flex;align-items:center;cursor:pointer;user-select:none;");
        return style.ToString();
    }

    public static string ToggleStates(HtmlContext context, Control toggle, string prefix)
    {
        string Paint(string state) =>
            string.Concat(
                Declaration("background", ThemeResources.Paint(toggle, $"{prefix}Background{state}")),
                Declaration("border-color", ThemeResources.Paint(toggle, $"{prefix}BorderBrush{state}")),
                Declaration("color", ThemeResources.Paint(toggle, $"{prefix}Foreground{state}")));
        var on = ":has(>input:checked)";
        var rules = toggle.IsEffectivelyEnabled
            ? new[] { (":hover", Paint("PointerOver")), (":active", Paint("Pressed")), (on, Paint("Checked")), (on + ":hover", Paint("CheckedPointerOver")), (on + ":active", Paint("CheckedPressed")) }
            : [(string.Empty, Paint("Disabled")), (on, Paint("CheckedDisabled"))];
        var name = "tg" + CssValues.Hash(string.Concat(rules.Select(rule => rule.Item1 + rule.Item2)));
        var css = new StringBuilder();
        foreach (var (selector, rule) in rules)
        {
            css.Append(CultureInfo.InvariantCulture, $".{name}.{name}{selector}{{{rule}}}");
        }

        css.Append(CultureInfo.InvariantCulture, $".{name}:has(>input:focus-visible){{outline:2px solid;outline-offset:1px}}\n");
        context.Sheet.AddRule(name, css.ToString());
        return name;
    }

    public static void WriteHiddenCheck(HtmlContext context, Control toggle, AvaloniaProperty property)
    {
        AddHidden(context);
        context.Plan.Literal("<input type=\"checkbox\"");
        context.Attributes(toggle);
        context.Plan.Literal(" name=\"f");
        context.Field(toggle);
        context.Plan.Literal("\" value=\"on\"");
        Condition(context, toggle, property, "true", " checked");
        context.Plan.Literal(toggle.IsEffectivelyEnabled ? " class=\"vh\">" : " class=\"vh\" disabled>");
    }

    public static void Condition(HtmlContext context, AvaloniaObject control, AvaloniaProperty property, string test, string text)
    {
        if (context.Plan.Planning && test == "true" && BoundConverters.Of(context.Plan, control, property) is { } converted)
        {
            context.Plan.Add(new ConditionNode(converted.Getter, [new LiteralNode(text)]) { Source = converted.Spec });
        }
        else if (context.Plan.Planning && context.Plan.BoundPath(control, property) is { } path)
        {
            var slot = Slots.Test(path, test);
            context.Plan.Add(new ConditionNode(slot.Getter, [new LiteralNode(text)]) { Source = slot.Spec });
        }
        else if (Slots.Check(test, control.GetValue(property)))
        {
            context.Plan.Literal(text);
        }
    }

    public static void WriteRadioButton(HtmlContext context, RadioButton radio, ParentKind parent)
    {
        if (ChoiceCards.Card(radio) is { } card)
        {
            ChoiceCards.Write(context, radio, parent, card, look => WriteRadioInput(context, radio, look));
            return;
        }

        var (style, own) = StateLooks.OwnStyle(radio, MarkLooks.States(radio, "input"), () =>
        {
            var box = context.Box(radio, parent);
            BoxStyles.Font(box, radio.FontSize, radio.FontWeight, radio.Foreground);
            return box;
        });
        var mark = MarkLooks.Class(context, radio, style, own: own);
        if (mark is null)
        {
            style.Append(radio.IsEffectivelyEnabled ? string.Empty : $"color:{ThemeResources.Paint(radio, "RadioButtonForegroundDisabled") ?? "GrayText"};");
            style.Append("display:flex;flex-direction:row;align-items:center;gap:8px;cursor:pointer;");
        }

        style.Append(mark is not null ? string.Empty : context.MinHeight(radio) > 0 ? $"min-height:{CssValues.Px(context.MinHeight(radio))};" : "min-height:32px;");
        context.Plan.Literal("<label");
        context.Root(radio, mark, style, attributes: false);
        context.Plan.Literal(">");
        WriteRadioInput(context, radio, mark is null ? $" class=\"{RadioLook(context, radio)}\"" : string.Empty);
        context.Content(radio.Content);
        context.Plan.Literal("</label>");
    }

    /// <summary>A radio button's native input: its group's field, its position as its value, its hot key as a page shortcut, then <paramref name="look"/>.</summary>
    private static void WriteRadioInput(HtmlContext context, RadioButton radio, string look)
    {
        var group = RadioGroup(radio);
        context.Plan.Literal("<input type=\"radio\"");
        context.Attributes(radio);
        context.Plan.Literal(HotKeys.Attributes(radio));
        context.Plan.Literal(" name=\"f");
        var leader = group.FirstOrDefault(context.Fields.Contains);
        if (leader is null || ReferenceEquals(leader, radio))
        {
            context.Field(radio, GroupKey(radio));
        }
        else
        {
            context.Plan.Add(new SameIdNode(GroupKey(leader)));
        }

        context.Plan.Literal($"\" value=\"{group.IndexOf(radio).ToString(CultureInfo.InvariantCulture)}\"");
        Condition(context, radio, ToggleButton.IsCheckedProperty, "true", " checked");
        context.Plan.Literal(look);
        context.Plan.Literal(radio.IsEffectivelyEnabled ? ">" : " disabled>");
    }

    private static string GroupKey(RadioButton leader) => "radio" + RuntimeHelpers.GetHashCode(leader).ToString(CultureInfo.InvariantCulture);

    public static List<RadioButton> RadioGroup(RadioButton radio)
    {
        if (radio.GroupName is { } name)
        {
            var root = radio.GetVisualAncestors().LastOrDefault() ?? radio;
            return [.. root.GetVisualDescendants().OfType<RadioButton>().Where(member => member.GroupName == name)];
        }

        return radio.GetLogicalParent() is { } parent
            ? [.. parent.GetLogicalChildren().OfType<RadioButton>().Where(member => member.GroupName is null)]
            : [radio];
    }

    public static void BindRadio(RadioButton radio, string? value)
    {
        if (!radio.IsEffectivelyEnabled || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            return;
        }

        var group = RadioGroup(radio);
        if (index >= group.Count)
        {
            return;
        }

        for (var position = 0; position < group.Count; position++)
        {
            group[position].IsChecked = position == index;
        }
    }

    public static void WriteToggleSwitch(HtmlContext context, ToggleSwitch toggle, ParentKind parent)
    {
        var (style, own) = StateLooks.OwnStyle(toggle, MarkLooks.States(toggle, ".tsr>input"), () =>
        {
            var box = context.Box(toggle, parent);
            BoxStyles.Font(box, toggle.FontSize, toggle.FontWeight, toggle.Foreground);
            return box;
        });
        var mark = MarkLooks.Class(context, toggle, style, target: ">.tsr>input", input: ".tsr>input", own: own);
        style.Append(mark is null ? "display:inline-flex;flex-direction:column;align-items:flex-start;cursor:pointer;user-select:none;" : string.Empty);
        context.Plan.Literal("<label");
        context.Root(toggle, mark ?? SwitchLook(context, toggle), style, attributes: false);
        context.Plan.Literal(">");
        if (toggle.Content is not null && (mark is null || MarkLooks.ContentFirst(toggle)))
        {
            context.Plan.Literal("<span>");
            context.Content(toggle.Content);
            context.Plan.Literal("</span>");
        }

        context.Sheet.AddRule("tsr", ".tsr{display:flex;flex-direction:row;align-items:center;gap:12px}.tsr>input:checked~.off,.tsr>input:not(:checked)~.on{display:none}\n");
        context.Plan.Literal("<span class=\"tsr\"><input type=\"checkbox\" role=\"switch\"");
        context.Attributes(toggle);
        context.Plan.Literal(" name=\"f");
        context.Field(toggle);
        context.Plan.Literal("\" value=\"on\"");
        context.Invalid(toggle);
        Condition(context, toggle, ToggleButton.IsCheckedProperty, "true", " checked");
        context.Plan.Literal(toggle.IsEffectivelyEnabled ? ">" : " disabled>");
        if (toggle.OffContent is not null || toggle.OnContent is not null)
        {
            context.Plan.Literal("<span class=\"off\">");
            context.Content(toggle.OffContent);
            context.Plan.Literal("</span><span class=\"on\">");
            context.Content(toggle.OnContent);
            context.Plan.Literal("</span>");
        }

        context.Plan.Literal("</span>");
        if (toggle.Content is not null && mark is not null && !MarkLooks.ContentFirst(toggle))
        {
            context.Plan.Literal("<span>");
            context.Content(toggle.Content);
            context.Plan.Literal("</span>");
        }

        context.Plan.Literal("</label>");
    }

    public static void WriteThreeState(HtmlContext context, CheckBox check, ParentKind parent)
    {
        var (style, own) = StateLooks.OwnStyle(check, OwnThreeStates(check), () =>
        {
            var box = context.Box(check, parent);
            BoxStyles.Font(box, check.FontSize, check.FontWeight, check.Foreground);
            return box;
        });
        context.Sheet.AddRule("tri", ".tri>label{display:none;position:relative;flex-direction:row;align-items:center;cursor:pointer;user-select:none}.tri:not(.trm)>label{gap:8px;min-height:32px}"
            + ".tri:has(input[value=false]:checked)>.u3,.tri:has(input[value=true]:checked)>.c3,.tri:has(input[value=null]:checked)>.i3{display:flex}"
            + ".tri:not(.trm) .box{width:20px;height:20px;flex:0 0 20px;display:flex;align-items:center;justify-content:center;border:1px solid;border-radius:4px}"
            + ".tri:not(.trm) label:has(>input:focus-visible) .box{outline:2px solid;outline-offset:1px}\n");
        AddHidden(context);
        var labelStyle = new StringBuilder();
        var mark = MarkLooks.Class(context, check, labelStyle, target: ">.box", states: ThreeStates(check));
        var label = mark is null ? string.Empty : " " + context.Sheet.Class(mark, labelStyle);
        context.Plan.Literal("<div");
        var classes = mark is null ? CssValues.Join("tri", BoxLook(context, check)) : "tri trm";
        context.Root(check, own is null ? classes : CssValues.Join(classes, context.Sheet.Rules(own.Rules.Select(static rule => (rule.Selector, string.Concat(rule.Rule.Select(static entry => $"{entry.Key}:{entry.Value};")))))), style);
        context.Plan.Literal(">");
        var disabled = check.IsEffectivelyEnabled ? string.Empty : " disabled";
        (string Label, string Value, string Glyph, string Current)[] states =
        [
            ("u3", "true", string.Empty, "true"),
            ("c3", "null", mark is null ? CheckGlyph : string.Empty, "null"),
            ("i3", "false", mark is null ? SquareGlyph : string.Empty, "false"),
        ];
        var first = true;
        foreach (var (name, value, glyph, current) in states)
        {
            context.Plan.Literal($"<label class=\"{name}{label}\"><input type=\"radio\" name=\"f");
            if (first)
            {
                context.Field(check);
                first = false;
            }
            else
            {
                context.Plan.Add(SameIdNode.Instance);
            }

            context.Plan.Literal($"\" value=\"{value}\"");
            Condition(context, check, ToggleButton.IsCheckedProperty, current, " checked");
            context.Plan.Literal($" class=\"vh\"{disabled}><span class=\"box\">{glyph}</span>");
            context.Content(check.Content);
            context.Plan.Literal("</label>");
        }

        context.Plan.Literal("</div>");
    }

    /// <summary>The states of a three-state check box's own element, which holds its three labels: the value its checked radio stands for, and the pointer over it.</summary>
    private static List<WebState> OwnThreeStates(CheckBox check)
    {
        List<WebState> values = [new(":checked", ":has(input[value=true]:checked)"), new(":indeterminate", ":has(input[value=null]:checked)")];
        return check.IsEffectivelyEnabled ? [new(":pointerover", ":hover"), new(":pressed", ":active"), .. values] : values;
    }

    /// <summary>The states of a three-state check box: each of its labels shows one of its values, unchecked, checked (<c>c3</c>) or indeterminate (<c>i3</c>).</summary>
    private static List<WebState> ThreeStates(CheckBox check) => check.IsEffectivelyEnabled
        ? [new(":pointerover", ":hover"), new(":pressed", ":active"), new(":checked", ".c3"), new(":indeterminate", ".i3"), new(":focus-visible", ":has(>input:focus-visible)")]
        : [new(":checked", ".c3"), new(":indeterminate", ".i3")];

    public static void BindCheckBox(CheckBox check, string? value)
    {
        if (!check.IsEffectivelyEnabled)
        {
            return;
        }

        if (!check.IsThreeState)
        {
            check.IsChecked = value is null && check.IsChecked is null ? null : value is not null;
            return;
        }

        check.IsChecked = value switch
        {
            "true" => true,
            "false" => false,
            "null" => null,
            _ => check.IsChecked,
        };
    }

    public static void AddHidden(HtmlContext context) =>
        context.Sheet.AddRule("vh", ".vh{position:absolute;opacity:0;width:1px;height:1px;margin:0;padding:0;border:0;pointer-events:none}\n");

    private static string Declaration(string property, string? value) => value is null ? string.Empty : $"{property}:{value};";

    private static string RadioLook(HtmlContext context, RadioButton radio)
    {
        string Paint(string part, string state) => ThemeResources.Paint(radio, $"RadioButton{part}{state}") ?? "transparent";
        string Ring(string state) =>
            $"border-color:{Paint("OuterEllipseCheckedStroke", state)};background:{Paint("CheckGlyphFill", state)};box-shadow:inset 0 0 0 5px {Paint("OuterEllipseCheckedFill", state)}";
        var css = new StringBuilder();
        var states = radio.IsEffectivelyEnabled ? new[] { (string.Empty, string.Empty), (":hover", "PointerOver"), (":active", "Pressed") } : [(string.Empty, "Disabled")];
        var name = "rb" + CssValues.Hash(string.Concat(states.Select(state => Paint("OuterEllipseStroke", state.Item2) + Ring(state.Item2))));
        css.Append(CultureInfo.InvariantCulture, $".{name}{{appearance:none;width:20px;height:20px;flex:0 0 20px;margin:0;border:1px solid;border-radius:50%;cursor:pointer}}");
        foreach (var (selector, state) in states)
        {
            css.Append(CultureInfo.InvariantCulture, $".{name}{selector}{{border-color:{Paint("OuterEllipseStroke", state)};background:{Paint("OuterEllipseFill", state)}}}");
            css.Append(CultureInfo.InvariantCulture, $".{name}:checked{selector}{{{Ring(state)}}}");
        }

        css.Append(CultureInfo.InvariantCulture, $".{name}:focus-visible{{outline:2px solid;outline-offset:1px}}\n");
        context.Sheet.AddRule(name, css.ToString());
        return name;
    }

    private static string SwitchLook(HtmlContext context, ToggleSwitch toggle)
    {
        string Paint(string key) => ThemeResources.Paint(toggle, "ToggleSwitch" + key) ?? "transparent";
        string Track(string state, bool on) => on
            ? $"background-color:{Paint("FillOn" + state)};border-color:{Paint("FillOn" + state)};background-image:radial-gradient(circle,{Paint("KnobFillOn" + state)} 5px,transparent 5.5px);background-position:20px 0;"
            : $"background-color:{Paint("FillOff" + state)};border-color:{Paint("StrokeOff" + state)};background-image:radial-gradient(circle,{Paint("KnobFillOff" + state)} 5px,transparent 5.5px);";
        var states = toggle.IsEffectivelyEnabled ? new[] { (string.Empty, string.Empty), (":hover", "PointerOver"), (":active", "Pressed") } : [(string.Empty, "Disabled")];
        var name = "ts" + CssValues.Hash(string.Concat(states.Select(state => Track(state.Item2, false) + Track(state.Item2, true))));
        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $".{name}>span:first-child:not(:last-child){{margin-bottom:6px}}");
        css.Append(CultureInfo.InvariantCulture, $".{name} .tsr{{display:flex;flex-direction:row;align-items:center;gap:12px;min-height:32px}}");
        css.Append(CultureInfo.InvariantCulture, $".{name} input{{appearance:none;width:40px;height:20px;flex:0 0 40px;margin:0;border:1px solid;border-radius:10px;background-size:18px 18px;background-repeat:no-repeat;background-position:0 0;transition:background-position .2s cubic-bezier(.33,1,.68,1);cursor:pointer}}");
        foreach (var (selector, state) in states)
        {
            css.Append(CultureInfo.InvariantCulture, $".{name} input{selector}{{{Track(state, false)}}}");
            css.Append(CultureInfo.InvariantCulture, $".{name} input:checked{selector}{{{Track(state, true)}}}");
        }

        css.Append(CultureInfo.InvariantCulture, $".{name} input:focus-visible{{outline:2px solid;outline-offset:2px}}");
        css.Append(CultureInfo.InvariantCulture, $".{name} input:checked~.off,.{name} input:not(:checked)~.on{{display:none}}\n");
        context.Sheet.AddRule(name, css.ToString());
        return name;
    }

    private static string BoxLook(HtmlContext context, CheckBox check)
    {
        string Paint(string part, string state) => ThemeResources.Paint(check, $"CheckBox{part}{state}") ?? "transparent";
        string Box(string state) =>
            $"border-color:{Paint("CheckBackgroundStroke", state)};background:{Paint("CheckBackgroundFill", state)};color:{Paint("CheckGlyphForeground", state)}";
        var states = check.IsEffectivelyEnabled ? new[] { (string.Empty, string.Empty), (":hover", "PointerOver"), (":active", "Pressed") } : [(string.Empty, "Disabled")];
        var name = "tr" + CssValues.Hash(string.Concat(states.Select(state => Box("Unchecked" + state.Item2) + Box("Checked" + state.Item2) + Box("Indeterminate" + state.Item2))));
        var css = new StringBuilder();
        foreach (var (selector, state) in states)
        {
            css.Append(CultureInfo.InvariantCulture, $".{name}>.u3{selector} .box{{{Box("Unchecked" + state)}}}");
            css.Append(CultureInfo.InvariantCulture, $".{name}>.c3{selector} .box{{{Box("Checked" + state)}}}");
            css.Append(CultureInfo.InvariantCulture, $".{name}>.i3{selector} .box{{{Box("Indeterminate" + state)}}}");
        }

        context.Sheet.AddRule(name, css.Append('\n').ToString());
        return name;
    }
}
