using Aioros.Markup;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html;

internal sealed class HtmlContext(PlanRecorder plan, StyleSheet sheet, ImmutableArray<object?> models, Action<Visual, ParentKind> write)
{
    public PlanRecorder Plan { get; } = plan;

    public StyleSheet Sheet { get; } = sheet;

    public ImmutableArray<object?> Models { get; } = models;

    public FormFields Fields { get; } = new();

    /// <summary>The library panels' flags the browser switches (<see cref="Variants.BrowserFlags"/>).</summary>
    public Variants.BrowserFlags Flags => field ??= new(this);

    /// <summary>The library's overlays the browser opens and closes (<see cref="Variants.BrowserModals"/>).</summary>
    public Variants.BrowserModals Modals => field ??= new(this);

    /// <summary>Whether the page is written as a post's answer, which knows every overlay's open state (<see cref="Variants.BrowserModals"/>).</summary>
    public bool Answering { get; set; }

    /// <summary>The fields written inside the library dialogs an answer writes, the only controls besides a prompt's whose post answers a command waiting on them.</summary>
    public List<int> Answers { get; } = [];

    /// <summary>How many popovers the page has numbered so far.</summary>
    public int Popovers { get; set; }

    /// <summary>The control whose flyout is being written, which its content belongs to though Avalonia shows it in a popup of its own.</summary>
    public Control? Opener { get; set; }

    /// <summary>What a post left for the user, written after the page and left out where it sits.</summary>
    public Overlays Overlays { get; set; } = Overlays.None;

    public bool LeavesOut(Control control) => Overlays.Hides(control) || Variants.BrowserModals.Owned(control);

    /// <summary>The controls written mid-transition and the look each enters from (<see cref="Looks.Entrances"/>).</summary>
    public ImmutableDictionary<Control, string> Entrances { get; set; } = ImmutableDictionary<Control, string>.Empty;

    /// <summary>Whether the control is a host panel with open prompts, written as a plain box the prompts' dialog is placed on.</summary>
    public bool HoldsPrompts(Control control) => Overlays.Anchor(control) is not null;

    public void Write(Visual visual, ParentKind parent) => write(visual, parent);

    private Visual? _item;

    private (Control Part, Action<ParentKind> Write)? _standIn;

    /// <summary>Writes a template's parts with <paramref name="instead"/> written in place of one of them (a choice card's indicator, written as the card's input).</summary>
    public void WriteWith(Visual parts, ParentKind parent, Control? part, Action<ParentKind> instead)
    {
        var outer = _standIn;
        _standIn = part is null ? outer : (part, instead);
        try
        {
            write(parts, parent);
        }
        finally
        {
            _standIn = outer;
        }
    }

    /// <summary>Whether the control is a part another writing stands in for, written here in its place.</summary>
    public bool StandsIn(Control control, ParentKind parent)
    {
        if (_standIn is not { } standIn || !ReferenceEquals(standIn.Part, control))
        {
            return false;
        }

        standIn.Write(parent);
        return true;
    }

    /// <summary>Writes an item's content inside the element that carries the item's look, its color for each state among it.</summary>
    public void WriteInItem(Visual presenter, Visual content, ParentKind parent)
    {
        var outer = _item;
        _item = presenter;
        try
        {
            write(content, parent);
        }
        finally
        {
            _item = outer;
        }
    }

    /// <summary>
    /// Whether a text takes its color from the item it is written in: nothing between it and the item's presenter sets a
    /// foreground, so the item's look gives it the color of the item's state, as Avalonia's inherited foreground does.
    /// </summary>
    public bool ColorFromItem(Visual text)
    {
        for (var current = text; _item is not null && current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, _item))
            {
                return true;
            }

            if (current.IsSet(TextElement.ForegroundProperty) && !ForwardsForeground(current))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Whether a template part sets its foreground to its templated control's (<c>{TemplateBinding Foreground}</c>), which then still inherits the item's.</summary>
    private static bool ForwardsForeground(Visual part) =>
        part.TemplatedParent is StyledElement parent && global::Avalonia.Data.BindingOperations.GetBindingExpressionBase(part, TextElement.ForegroundProperty) is { } expression
        && TemplateBindings.Property(expression, parent) == TextElement.ForegroundProperty;

    /// <summary>Writes a field's id inside its <c>name="f…"</c>, closed by the writer, and marks a field whose value goes nowhere <c>data-aioros-local</c> right after it (SSR-292).</summary>
    public void Field(Control control, string? key = null)
    {
        Fields.Add(control);
        Plan.Id(Forms.FieldSpecs.Of(Plan, control), key);
        if (Forms.FieldSpecs.IsLocal(control))
        {
            Plan.Literal("\" data-aioros-local=\"");
        }
    }

    private readonly HashSet<Control> _described = [];

    /// <summary>
    /// Marks a field invalid while its model reports errors for it: <c>aria-invalid</c>, and <c>aria-describedby</c> naming
    /// the element of its messages (<see cref="Describes"/>) when its template shows them, written right after the field's
    /// name so they follow its id, planned on the errors of the bound member, or written as they are for errors set in the XAML.
    /// </summary>
    public void Invalid(Control control)
    {
        if (!Writers.OverlayWriters.Validates(control))
        {
            return;
        }

        ImmutableArray<IPlanNode> attributes = [new LiteralNode(" aria-invalid=\"true\"")];
        if (Writers.OverlayWriters.ShowsMessages(control))
        {
            _described.Add(control);
            attributes = [new LiteralNode(" aria-invalid=\"true\" aria-describedby=\"e"), SameIdNode.Instance, new LiteralNode("\"")];
        }

        if (Plan.Planning && control.DataContext is System.ComponentModel.INotifyDataErrorInfo && Writers.OverlayWriters.ValidatedPath(Plan, control) is { } path)
        {
            var invalid = Slots.InvalidOf(path);
            Plan.Add(new ConditionNode(invalid.Getter, attributes) { Source = invalid.Spec });
        }
        else if (DataValidationErrors.GetHasErrors(control))
        {
            foreach (var node in attributes)
            {
                Plan.Add(node);
            }
        }
    }

    /// <summary>Whether a field names the element of its messages (<see cref="Invalid"/>), which then carries its id.</summary>
    public bool Describes(Control control) => _described.Contains(control);

    /// <summary>Writes the id of something a post names in <c>_btn</c> (a button, a drop target), numbered apart from the fields.</summary>
    public void Button(Control control, string? key = null)
    {
        Fields.Add(control, button: true);
        Plan.Id(Forms.FieldSpecs.Of(Plan, control), key, button: true);
    }

    /// <summary>Writes the id of a key binding's button, numbered with the buttons, its post running the binding's command (<see cref="Writers.KeyBindingWriters"/>).</summary>
    public void KeyButton(Control owner, global::Avalonia.Input.KeyBinding binding, SlotSpec? command)
    {
        Fields.Add(owner, button: true, key: binding);
        Plan.Id(command, button: true);
    }

    public void Content(object? content)
    {
        if (content is Control control)
        {
            write(control, ParentKind.Row);
        }
        else
        {
            Plan.Text(content?.ToString());
        }
    }

    /// <summary>A content control's content: bound text becomes a slot, anything else is written as it is now.</summary>
    public void Content(ContentControl owner)
    {
        if (owner.Content is not Control && LocalBindings.IsBound(owner, ContentControl.ContentProperty))
        {
            if (owner.Content is not (null or string) && Templated(owner) is not null
                && ItemParts.Presenter(owner, "PART_ContentPresenter") is { } presenter && Plan.BoundPath(owner, ContentControl.ContentProperty) is { } path)
            {
                Plan.Presented(presenter, ContentPresenter.ContentProperty);
                Templating.TemplatedContents.Write(Plan, presenter, path, () =>
                {
                    if (presenter.Child is { } child)
                    {
                        write(child, ParentKind.Row);
                    }
                });
                return;
            }

            Plan.Value(owner, ContentControl.ContentProperty);
            return;
        }

        if (owner.Content is not (null or string or Control) && Templated(owner) is { } built)
        {
            Plan.WriteConstant(built, () => write(built, ParentKind.Row));
            return;
        }

        Content(owner.Content);
    }

    /// <summary>What the owner's presenter built from its content with a data template, when it is more than the content's text.</summary>
    private static Control? Templated(ContentControl owner)
    {
        ThemeLooks.ExpandTemplates(owner);
        return ItemParts.Presenter(owner, "PART_ContentPresenter") is { Child: { } child } presenter
            && !(child is TextBlock text && string.Equals(text.Text, PlanValues.Format(presenter.Content), StringComparison.Ordinal))
                ? child
                : null;
    }

    /// <summary>A new popover id: <c>m</c> and its number in Avalonia's render, a mark the plan numbers on each render while it is recorded (<see cref="PopoverMarks.Of"/>).</summary>
    public string NextPopover() => Plan.Recording ? PopoverMarks.Of(++Popovers) : "m" + (++Popovers).ToString(CultureInfo.InvariantCulture);

    /// <summary>The controls written inside an element that carries their whole box (<see cref="BoxStyles.Outer"/>), whose own element only fills it.</summary>
    public HashSet<Control> Wrapped { get; } = [];

    /// <summary>The style of the element that carries a control's whole box: its motion, box and effects (<see cref="BoxStyles.Outer"/>).</summary>
    public StringBuilder OuterBox(Control control, ParentKind parent)
    {
        var style = new StringBuilder();
        var animated = MotionStyles.Append(control, style, Sheet);
        return style.Append(BoxStyles.Outer(control, parent, animated));
    }

    /// <summary>The control's minimum height for its own element: the one its field's parts give it inside the element that carries its whole box, its own otherwise.</summary>
    public double MinHeight(Control control) => Wrapped.Contains(control) ? FieldParts.MinHeight(control) : control.MinHeight;

    public StringBuilder Box(Control control, ParentKind parent, bool size = true)
    {
        var style = new StringBuilder();
        if (!Wrapped.Contains(control))
        {
            var animated = MotionStyles.Append(control, style, Sheet);
            style.Append(BoxStyles.Box(control, parent, animated, size));
        }

        FontFaces.Append(style, control, Sheet);
        if (Overlays.Anchor(control) is { } anchor)
        {
            style.Append(CultureInfo.InvariantCulture, $"anchor-name:--{anchor};pointer-events:none;");
        }
        else if (Modals.Anchor(control) is { } covered)
        {
            style.Append(CultureInfo.InvariantCulture, $"anchor-name:--{covered};");
        }

        return style;
    }

    /// <summary>The tool tip that describes each control (<c>aria-describedby</c>), set before the control is written, its id given where it is first written (<see cref="TipId"/>).</summary>
    public Dictionary<Control, (string? Id, Slot? Shown)> DescribedBy { get; } = [];

    /// <summary>
    /// The id of the tool tip that describes the control, numbered where it is first written (the control's
    /// <c>aria-describedby</c>, after a flyout's <c>popovertarget</c>), as the plan numbers its popovers in document order.
    /// </summary>
    public string TipId(Control control)
    {
        var (id, shown) = DescribedBy[control];
        if (id is null)
        {
            id = NextPopover();
            DescribedBy[control] = (id, shown);
        }

        return id;
    }

    /// <summary>Template parts their control's writer writes in their place when the tree reaches them (a carousel's strip, buttons and indicators).</summary>
    public Dictionary<Control, Action<ParentKind>> Substitutes { get; } = [];

    /// <summary>The ids the answer gives controls other elements name (a prompt's title and description).</summary>
    public Dictionary<Control, string> Ids { get; } = [];

    /// <summary>The classes an owner's writer gives some of its parts (the parts of a deck's card that fade behind the newest, <c>a:Web.DeckBehind</c>).</summary>
    public Dictionary<Control, string> PartClasses { get; } = [];

    /// <summary>The texts a file picker's template shows its chosen file's name in, which the framework's script writes the name in once a file is chosen.</summary>
    public HashSet<Control> FileNames { get; } = [];

    /// <summary>The controls whose assertive live setting their prompt's <c>alertdialog</c> role announces, written without a live role of their own.</summary>
    public HashSet<Control> Announced { get; } = [];

    /// <summary>The <c>name</c> each exclusive panel or list gives the expanders inside it (<see cref="Web.ExclusiveProperty"/>).</summary>
    public Dictionary<Control, string> Groups { get; } = [];

    /// <summary>The control Avalonia focused in an open prompt, which the dialog focuses when it opens.</summary>
    public Control? AutoFocus { get; set; }

    /// <summary>The element's <c>dir</c> where its flow direction differs from its parent's, as Avalonia mirrors it there.</summary>
    private static string Direction(Control control) =>
        control.FlowDirection == ((control.GetVisualParent() as Control)?.FlowDirection ?? global::Avalonia.Media.FlowDirection.LeftToRight) ? string.Empty
        : control.FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft ? " dir=\"rtl\"" : " dir=\"ltr\"";

    /// <summary>The marks of a region holding answers and of a button waiting for one (<c>a:Web.Answers</c>, <c>a:Web.NeedsAnswer</c>), and of a region answering keys and its buttons' keys (<c>a:Web.KeyScope</c>, <c>a:Web.Keys</c>).</summary>
    private static string AnswerMarks(Control control) =>
        (Web.GetAnswers(control) ? " data-aioros-answers" : string.Empty) + (Web.GetNeedsAnswer(control) ? " data-aioros-needs-answer" : string.Empty)
        + (Web.GetKeyScope(control) ? " data-aioros-keyscope" : string.Empty) + Writers.HotKeys.Scoped(Web.GetKeys(control));

    public void Attributes(Control control)
    {
        OpenInApp(control);
        Flags.WriteMarks(control);
        Plan.Literal(Panels.ScrollEnds.Attributes(control, Sheet));
        Plan.Literal(Gestures.Swipes.Attributes(control, Sheet));
        if (control is not global::Avalonia.Controls.Button && Web.GetFlips(control) is { } flips)
        {
            Plan.Literal($" data-aioros-flips=\"{CssValues.Encode(flips)}\"");
        }

        if (Web.GetId(control) is null && Ids.TryGetValue(control, out var named))
        {
            Plan.Literal($" id=\"{named}\"");
        }

        if (ReferenceEquals(control, AutoFocus))
        {
            Plan.Literal(" autofocus");
        }

        Plan.Literal(Direction(control) + AnswerMarks(control));

        if (DescribedBy.TryGetValue(control, out var tip) && (tip.Shown is not null || ToolTip.GetTip(control) is not null || Flags.OtherTip(control) is not null) && !Describes(control))
        {
            if (tip.Shown is { } shown)
            {
                Plan.Begin(Plan.Scope);
                Plan.Literal($" aria-describedby=\"{TipId(control)}\"");
                Plan.Add(new ConditionNode(shown.Getter, Plan.End()) { Source = shown.Spec });
            }
            else
            {
                Plan.Literal($" aria-describedby=\"{TipId(control)}\"");
            }
        }

        if (Web.GetId(control) is { } id)
        {
            Plan.Literal($" id=\"{CssValues.Encode(id)}\"");
        }

        CurrentMark(control);
        Plan.Literal(Marks(control));
        CodeSlots(control);

        if (LocalBindings.IsBound(control, global::Avalonia.Automation.AutomationProperties.NameProperty))
        {
            Plan.Literal(" aria-label=\"");
            Plan.Value(control, global::Avalonia.Automation.AutomationProperties.NameProperty);
            Plan.Literal("\"");
        }
        else if (global::Avalonia.Automation.AutomationProperties.GetName(control) is { Length: > 0 } name)
        {
            Plan.Literal($" aria-label=\"{CssValues.Encode(name)}\"");
        }

        Writers.FilteredLists.Attributes(this, control);
        Writers.ChoiceLists.Attributes(this, control);
    }

    /// <summary>The marks the framework's script reads on the control's element: a file field's name, an entrance that waits to be scrolled into view (MOTION-22) and a number that counts up (MOTION-27).</summary>
    private string Marks(Control control) =>
        (FileNames.Contains(control) ? " data-aioros-file-name" : string.Empty) + (Web.GetReveal(control) ? " data-aioros-reveal" : string.Empty)
        + (Web.GetCountUp(control) is > 0 and var seconds ? $" data-aioros-count=\"{CssValues.Num(Math.Round(seconds * 1000))}\"" : string.Empty)
        + (Web.GetMarquee(control) > 0 ? " data-aioros-marquee" : string.Empty)
        + (Web.GetSpotlight(control) is not null ? " data-aioros-spotlight" : string.Empty)
        + (double.IsNaN(Web.GetParallax(control)) ? string.Empty : $" data-aioros-parallax=\"{CssValues.Num(Web.GetParallax(control))}\"");

    /// <summary>
    /// The control's <see cref="Web.CurrentProperty"/> as <c>aria-current</c> when it is one of WAI-ARIA's kinds, a bound one
    /// written while the bound value is.
    /// </summary>
    private void CurrentMark(Control control)
    {
        if (Plan.Planning && LocalBindings.IsBound(control, Web.CurrentProperty))
        {
            var test = BoundConverters.Of(Plan, control, Web.CurrentProperty) is { Spec: { } converted }
                ? Slots.Of(converted with { Values = converted.Values.SetItem(3, "current") })
                : Plan.BoundPath(control, Web.CurrentProperty) is { } path ? Slots.Test(path, "current") : (Slot?)null;
            if (test is { } slot)
            {
                Plan.Begin(Plan.Scope);
                Plan.Literal(" aria-current=\"");
                Plan.Value(control, Web.CurrentProperty);
                Plan.Literal("\"");
                Plan.Add(new ConditionNode(slot.Getter, Plan.End()) { Source = slot.Spec });
                return;
            }
        }

        if (Aioros.Runtime.Slots.PlanSlotResolver.Check("current", Web.GetCurrent(control)))
        {
            Plan.Literal($" aria-current=\"{Web.GetCurrent(control)}\"");
        }
    }

    private readonly HashSet<Control> _slotInputs = [];

    /// <summary>
    /// The marks of a list showing a text box's characters (<see cref="Web.SlotsProperty"/>) the framework's script fills:
    /// the list, its text box, each item's bound text and the element whose active look is written under the script's mark.
    /// </summary>
    private void CodeSlots(Control control)
    {
        if (Web.GetSlots(control) is { } input)
        {
            _slotInputs.Add(input);
            Plan.Literal(" data-aioros-slots");
        }

        if (_slotInputs.Contains(control))
        {
            Plan.Literal(" data-aioros-slots-input");
        }

        if (Web.GetSlotActive(control) is { Length: > 0 } active)
        {
            StateStyles.RegisterClass(this, control, active, "[data-aioros-active]");
            Plan.Literal(" data-aioros-slot-box");
        }

        if (control is TextBlock text && LocalBindings.IsBound(text, TextBlock.TextProperty) && text.FindAncestorOfType<ItemsControl>() is { } list && Web.GetSlots(list) is not null)
        {
            Plan.Literal(" data-aioros-slot-text");
        }
    }

    /// <summary>Marks a link that opens the installed app (<see cref="Web.OpenInAppProperty"/>) for the framework's script.</summary>
    public void OpenInApp(Control control) => Plan.Literal(Web.GetOpenInApp(control) ? " data-open-app" : string.Empty);

    private readonly Dictionary<Control, List<(Control Owner, string Name)>> _stateClasses = [];

    /// <summary>
    /// Gives a control the state class its owner's state styles wrote for it, in place of the one the same owner gave it
    /// before: a control written once per variant of a binding (a bound class) carries only the class of the variant
    /// written last, as Avalonia's render of that variant does.
    /// </summary>
    /// <summary>Takes back the state classes an owner gave, before it is written again in another variant.</summary>
    public void ForgetStateClasses(Control owner)
    {
        foreach (var names in _stateClasses.Values)
        {
            names.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
        }
    }

    public void AddStateClass(Control control, string name, Control owner)
    {
        if (!_stateClasses.TryGetValue(control, out var names))
        {
            _stateClasses[control] = names = [];
        }

        names.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
        names.Add((owner, name));
    }

    private (Control Root, Dictionary<Control, string> Roles)? _shell;

    /// <summary>The control's role in a window-filling shell (<see cref="Panels.ViewportShells"/>), with the shell's rules added once.</summary>
    private string? ShellRole(Control control)
    {
        var root = control;
        while (root.Parent is Control parent and not TopLevel)
        {
            root = parent;
        }

        if (_shell is not { } shell || !ReferenceEquals(shell.Root, root))
        {
            _shell = shell = (root, Panels.ViewportShells.Roles(control));
        }

        if (!shell.Roles.TryGetValue(control, out var role))
        {
            return null;
        }

        Sheet.AddRule("viewport-shell", Panels.ViewportShells.Css);
        return role;
    }

    public string Extra(Control control)
    {
        var classes = _stateClasses.TryGetValue(control, out var names) ? string.Join(' ', names.Select(entry => entry.Name).Distinct(StringComparer.Ordinal)) : string.Empty;
        classes = CssValues.Join(classes, ShellRole(control));
        classes = CssValues.Join(classes, PartClasses.GetValueOrDefault(control));
        var above = Web.GetHideAbove(control);
        if (!double.IsNaN(above))
        {
            classes = CssValues.Join(classes, Sheet.HideAbove(above));
        }

        var below = Web.GetHideBelow(control);
        if (!double.IsNaN(below))
        {
            classes = CssValues.Join(classes, Sheet.HideBelow(below));
        }

        if (Web.GetTransitionName(control) is { } transition)
        {
            var name = "vt-" + transition;
            Sheet.AddRule(name, $".{name}{{view-transition-name:{transition}}}\n");
            classes = CssValues.Join(classes, name);
        }

        return classes;
    }

    /// <summary>The classes of a control's element: the given ones with its state and responsive classes, then the hash class of its style.</summary>
    public string Classes(Control control, string? classes, StringBuilder style) => Flags.Classes(control, CssValues.Join(CssValues.Join(classes, Extra(control)), Entrance(control)), style);

    /// <summary>The class of the look a control written mid-transition enters from, or nothing.</summary>
    public string? Entrance(Control control)
    {
        if (!Entrances.TryGetValue(control, out var css))
        {
            return null;
        }

        var name = "en" + CssValues.Hash(css);
        Sheet.AddRule(name, Looks.Entrances.Rule(name, css));
        return name;
    }

    /// <summary>
    /// Writes what opens every control's element after its tag: its id and automation name, its classes (the hash class
    /// of its box and look without the declarations its bound values write, its state and responsive classes) and the
    /// inline style of its bound values, after <paramref name="inline"/>. The caller writes the tag before and closes it;
    /// a writer whose element wraps a native input writes the id and automation name on the input instead.
    /// </summary>
    public void Root(Control control, string? classes, StringBuilder style, string? inline = null, bool attributes = true)
    {
        if (attributes)
        {
            Attributes(control);
        }

        var slots = OwnSlots(control);
        Plan.Literal($" class=\"{Classes(control, classes, BoundStyles.Strip(style, slots))}\"");
        InlineStyle(control, slots, inline);
    }

    public void DynamicStyle(Control control) => InlineStyle(control, OwnSlots(control), null);

    /// <summary>The bound declarations of the element that carries the control's whole box, written in its inline style.</summary>
    public void OuterStyle(Control control) => InlineStyle(control, BoundStyles.Outer(BoundStyles.Slots(control), outer: true), null);

    private List<InlineSlot> OwnSlots(Control control) =>
        Wrapped.Contains(control) ? BoundStyles.Outer(BoundStyles.Slots(control), outer: false) : BoundStyles.Slots(control);

    private void InlineStyle(Control control, List<InlineSlot> slots, string? inline)
    {
        if (slots.Count == 0 && string.IsNullOrEmpty(inline))
        {
            return;
        }

        Plan.Literal($" style=\"{inline}{(string.IsNullOrEmpty(inline) || slots.Count == 0 ? string.Empty : ";")}");
        BoundStyles.Write(this, control, slots);
        Plan.Literal("\"");
    }
}
