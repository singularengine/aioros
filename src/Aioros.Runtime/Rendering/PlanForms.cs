using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Rendering;

public delegate void FieldBinding(object? scope, string? value);

public sealed record FieldCommand(ValueGetter Parameter, Func<object?, object?, ValueTask> ExecuteAsync)
{
    /// <summary>
    /// A drop target's move: given the field's scope, its parameter (the target's key) and what the post names after
    /// the field's token (<c>index|item</c>, or a step for <c>index</c>), runs the command with the move and answers how
    /// the post went; <see langword="null"/> for a button, whose post must name its parameter exactly.
    /// </summary>
    public Func<object?, object?, string, ValueTask<PostStatus>>? DropAsync { get; init; }
}

public interface IFieldResolver
{
    FieldBinding? Binding(SlotSpec field);

    FieldCommand? Command(SlotSpec field);

    /// <summary>
    /// Notes the values a post's binding of this field would overwrite in this scope and gives back what puts them back,
    /// so a post bound only to look for its button can be undone when the button is not there; none when it cannot.
    /// </summary>
    Action? Kept(SlotSpec field, object? scope) => null;
}

/// <summary>A travelling state a render wrote (<see cref="StateNode"/>): its index, its binding and the scopes it was written in.</summary>
public sealed record PlannedState(int Index, SlotSpec Field, object? Scope)
{
    public ImmutableStack<object?> Outer { get; init; } = [];

    /// <summary>The key of the list item the state stands in, which its name carries.</summary>
    public UInt128? ItemKey { get; init; }
}

public sealed record PlannedField(int Id, SlotSpec? Field, object? Scope)
{
    /// <summary>The scopes around the field's, for slots that read an ancestor's data context.</summary>
    public ImmutableStack<object?> Outer { get; init; } = [];

    /// <summary>The model slot of the view whose region holds the field, or <c>-1</c> for the page and its layouts.</summary>
    public int View { get; init; } = -1;

    /// <summary>The key of the item of the repeated view whose region holds the field, when it is one.</summary>
    public string? Item { get; init; }

    /// <summary>The key of the list item the field belongs to, when its list's items have an identity.</summary>
    public UInt128? ItemKey { get; init; }

    /// <summary>What names the field in the form: its id, prefixed by its view's slot inside a view (<c>2.0</c>) and its item inside a repeated one (<c>2-7.0</c>), followed by its list item's key (<c>1~1a2b3c4d</c>).</summary>
    public string Token => PlanForms.Token(View, Id, Item) + ItemKeys.Suffix(ItemKey);

    /// <summary>The field's token without its list item's key, the name a post without keys gives it.</summary>
    public string BareToken => PlanForms.Token(View, Id, Item);

    /// <summary>Whether the field was written inside an item of a bound list, so its id moves with the list.</summary>
    public bool Listed { get; init; }

    /// <summary>Whether a posted token names this field: with its key, or bare.</summary>
    public bool Named(string? token) => token == Token || token == BareToken;

    /// <summary>Whether the field is a button, numbered apart from the fields and named only by a post's <c>_btn</c>.</summary>
    public bool Button { get; init; }

    /// <summary>Where the field's command belongs.</summary>
    public ViewTarget Target => new(View, Item);

    /// <summary>The values a choice field's options were written with, the only ones a post binds; none for any other field.</summary>
    public ImmutableArray<string> Options { get; init; }

    /// <summary>Whether a library's slot renderer reserved the field, whose buttons may depend on what a post binds.</summary>
    public bool Rendered { get; init; }
}

public sealed class FieldLog
{
    private readonly List<PlannedField> _fields = [];
    private Dictionary<int, ImmutableArray<string>.Builder>? _options;
    private (int Id, int View, string? Item, int Index) _lastOption = (-1, -1, null, -1);

    /// <summary>The fields recorded, each choice with the options written for it.</summary>
    public IReadOnlyList<PlannedField> Fields
    {
        get
        {
            if (_options is { } pending)
            {
                foreach (var (index, options) in pending)
                {
                    _fields[index] = _fields[index] with { Options = options.ToImmutable() };
                }

                _options = null;
            }

            return _fields;
        }
    }

    /// <summary>
    /// Whether every field the render wrote has a description: a button without one does not count, since it matters only
    /// when a post presses it, and that post goes to the engine.
    /// </summary>
    public bool Described => _fields.TrueForAll(planned => planned.Field is not null || planned.Button);

    /// <summary>The travelling states recorded, by their index (<c>_s{n}</c>).</summary>
    public IReadOnlyList<PlannedState> States => _states;

    private readonly List<PlannedState> _states = [];

    public void State(int index, SlotSpec field, object? scope, UInt128? itemKey = null) => _states.Add(new PlannedState(index, field, scope) { Outer = OuterScopes.Current, ItemKey = itemKey });

    private readonly Dictionary<ViewTarget, string> _shapes = [];

    /// <summary>Notes the mark of a region's lists.</summary>
    public void Shape(ViewTarget target, string mark) => _shapes[target] = mark;

    /// <summary>The mark of a region's lists, or nothing when it wrote none.</summary>
    public string? ShapeOf(ViewTarget target) => _shapes.GetValueOrDefault(target);

    public void Record(int id, SlotSpec? field, object? scope, int view = -1, string? item = null, bool rendered = false, bool button = false, UInt128? itemKey = null, bool listed = false)
    {
        _fields.Add(new PlannedField(id, field, scope) { Outer = OuterScopes.Current, View = view, Item = item, Rendered = rendered, Button = button, ItemKey = itemKey, Listed = listed });
        if (!button && _lastOption.Id == id && _lastOption.View == view && _lastOption.Item == item)
        {
            _lastOption = (-1, -1, null, -1);
        }
    }

    /// <summary>
    /// Adds an option's value to the choice field recorded last with this id in this view and item: the field is looked up
    /// once for a run of options and their values gathered in a builder, so a choice of many options is recorded in linear time.
    /// </summary>
    public void Option(int id, int view, string? item, string value)
    {
        var index = _lastOption.Id == id && _lastOption.View == view && _lastOption.Item == item
            ? _lastOption.Index
            : _fields.FindLastIndex(planned => !planned.Button && planned.Id == id && planned.View == view && planned.Item == item);
        if (index < 0)
        {
            return;
        }

        _lastOption = (id, view, item, index);
        var options = _options ??= [];
        if (!options.TryGetValue(index, out var builder))
        {
            builder = ImmutableArray.CreateBuilder<string>();
            if (!_fields[index].Options.IsDefault)
            {
                builder.AddRange(_fields[index].Options);
            }

            options[index] = builder;
        }

        builder.Add(value);
    }

    /// <summary>Whether a slot renderer reserved a field of this view, so its buttons are looked for again once the post is bound.</summary>
    public bool Rerenders(ViewTarget target) => _fields.Exists(planned => planned.Rendered && planned.Target == target);
}

public static class PlanForms
{

    public const string ButtonField = "_btn";

    /// <summary>The hidden field that marks the lists a region wrote (<see cref="ItemKeys.ShapeText"/>): its name for a region.</summary>
    public static string ShapeName(ViewTarget target) =>
        "_shape" + (target.Slot < 0 ? string.Empty : target.Slot.ToString(CultureInfo.InvariantCulture) + (target.Item is null ? string.Empty : "-" + ViewItems.Token(target.Item)));

    /// <summary>
    /// The field a post without a button names when the framework's script posts a changed selection (LiveView's
    /// <c>phx-change</c>, htmx's <c>hx-trigger="change"</c>): the token of the field that changed, which picks the view
    /// whose fields the post binds, as a button's token does.
    /// </summary>
    public const string ChangeField = "_chg";

    /// <summary>
    /// The field an open prompt posts with its buttons: the steps that opened it and changed it (the opening button, then
    /// each step's button and the prompt's fields), which the engine replays on a fresh page before the post's own step.
    /// </summary>
    public const string PromptField = "_prompt";

    /// <summary>
    /// Whether a posted name is a control's state (<c>_s{n}</c>, a library's <c>a:Web.State</c> or an overlay's open
    /// state), restored before a post is bound: by the plan through its state nodes, by the engine otherwise.
    /// </summary>
    public static bool IsState(string name) =>
        name.Length > 2 && (name.StartsWith("_s", StringComparison.Ordinal) || name.StartsWith("_m", StringComparison.Ordinal)) && ItemKeys.Bare(name.AsSpan(2)).IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>
    /// Why the plan would leave this post to the engine, without binding anything: the travelling states it cannot restore,
    /// the fields it holds no description of, the fields it cannot bind, or kept fields the post carries, whose values only the engine shows; nothing when it binds
    /// the post.
    /// </summary>
    public static string? Refusal(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(resolver);
        var fields = Fields(plan, models);
        var states = form.Keys.Where(IsState).Where(name => PlanStates.StateOf(fields, name) is not { } state || resolver.Binding(state.Field) is null).ToList();
        var undescribed = fields.Fields.Where(static field => field.Field is null && !field.Button).Select(FieldName).ToList();
        var unbound = fields.Fields.Where(field => field.Field is { } spec && spec.Kind != KeptKind && resolver.Binding(spec) is null && resolver.Command(spec) is null)
            .Select(field => FieldName(field) + " (" + field.Field!.Kind + ")").ToList();
        return states.Count > 0 ? "travelling states it cannot restore: " + string.Join(", ", states)
            : undescribed.Count > 0 ? "fields it holds no description of: " + string.Join(", ", undescribed)
            : unbound.Count > 0 ? "fields it cannot bind: " + string.Join(", ", unbound)
            : Shows(fields, form) ? "it carries fields whose values only the engine shows: " + string.Join(", ", fields.Fields.Where(field => !field.Button && field.Field?.Kind == KeptKind && form.ContainsKey(FieldName(field))).Select(FieldName))
            : null;
    }

    /// <summary>
    /// Whether the plan's render writes what the engine's would after this post: it holds a state node for every travelling
    /// state the post carries and a description of every field, and the post carries no kept field, whose value only the engine shows.
    /// </summary>
    public static bool DescribesStates(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var fields = Fields(plan, models);
        return fields.Described && !Shows(fields, form)
            && form.Keys.Where(IsState).All(name => PlanStates.StateOf(fields, name) is not null);
    }

    /// <summary>
    /// Writes the post's values of the page's local inputs (<see cref="Slots.LocalPaths"/>) into the request's own state, as
    /// the engine's controls take them, for the plan to render an answer the engine gave; nothing else is written, and
    /// nothing at all when the plan cannot describe the page for these models (a value it never learned): false then, and
    /// the engine renders the answer.
    /// </summary>
    public static bool BindLocals(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        if (!LocalValues.InEffect || ViewOf(form) is not { } target)
        {
            return true;
        }

        try
        {
            Bind(SettledFields.Locals(SettledFields.Of(plan, models, form, resolver), resolver), target, form, static _ => true);
            return true;
        }
        catch (PlanMissException)
        {
            return false;
        }
    }

    /// <summary>
    /// The fields of the page's local inputs the guarantee does not cover (<see cref="Slots.LocalPath.Guaranteed"/>), as the
    /// browser names them: the plan answers a post on the page only once its verification proved each of them.
    /// </summary>
    public static ImmutableArray<string> Unguaranteed(RenderPlan plan, ImmutableArray<object?> models) =>
        [.. Fields(plan, models).Fields.Where(static field => !field.Button && field.Field is { Path: { } path } && Slots.LocalPaths.TryRead(path, out var local) && !local.Guaranteed).Select(FieldName).Distinct(StringComparer.Ordinal)];

    /// <summary>Whether a posted name is a library's travelling state (<c>_s{n}</c>), which the server seals; an overlay's open state (<c>_m{n}</c>) is not.</summary>
    public static bool IsSealedState(string name) => IsState(name) && name[1] == 's';

    /// <summary>
    /// The field the framework's script posts when an open prompt is dismissed in the browser (<c>escape</c> for the
    /// Escape key, <c>outside</c> for a click outside it), with the prompt's steps: the engine replays them and hands the
    /// dismissal to the app's dialog as Avalonia's input.
    /// </summary>
    public const string DismissField = "_dismiss";

    /// <summary>
    /// The hidden field a notice that holds buttons carries for a post without the framework's script: its buttons'
    /// tokens on its first line, then the step that showed it, which a press of one of them posts as <see cref="PromptField"/>.
    /// </summary>
    public const string NoticeField = "_notice";

    /// <summary>
    /// The hidden field an open prompt carries beside its <see cref="PromptField"/>: the tokens of its buttons and, with an
    /// <c>f</c>, of its fields, the only controls whose post answers it.
    /// </summary>
    public const string AnswersField = "_answers";

    /// <summary>
    /// A post's form as its overlays scope it, without their fields: a press of a notice's button posts the notice's step as
    /// <see cref="PromptField"/>; otherwise an open prompt's steps stay only when the post answers the prompt (one of its
    /// buttons pressed, one of its fields changed, or a dismissal), so a control of the page outside it, which a browser
    /// without scripting leaves pressable, runs alone.
    /// </summary>
    public static ImmutableDictionary<string, string> Overlays(ImmutableDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var rest = form.Remove(NoticeField).Remove(AnswersField);
        var pressed = form.TryGetValue(ButtonField, out var button) && button.IndexOf('|', StringComparison.Ordinal) is var bar and >= 0 ? button[..bar] : null;
        if (form.TryGetValue(NoticeField, out var notice) && notice.ReplaceLineEndings("\n") is var lines && lines.IndexOf('\n', StringComparison.Ordinal) is var end and >= 0
            && pressed is not null && Names(lines[..end], pressed))
        {
            return rest.SetItem(PromptField, lines[(end + 1)..]);
        }

        if (!form.TryGetValue(AnswersField, out var answers) || !rest.ContainsKey(PromptField))
        {
            return rest;
        }

        var changed = form.TryGetValue(ChangeField, out var named) && named.Length > 0 ? "f" + named : null;
        var answering = form.ContainsKey(DismissField) || (pressed is not null && Names(answers, pressed)) || (changed is not null && Names(answers, changed));
        return answering ? rest : rest.Remove(PromptField);
    }

    /// <summary>
    /// The hidden field a range's panel carries after its two sliders: their field names in order, the lower first, so a post
    /// that carries them crossed (two thumbs dragged past each other without the framework's script) binds them in order.
    /// </summary>
    public const string RangeField = "_range";

    /// <summary>A post's form with each range's two values in order (swapped when crossed), without its <see cref="RangeField"/>.</summary>
    public static ImmutableDictionary<string, string> Ranges(ImmutableDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (!form.TryGetValue(RangeField, out var ranges))
        {
            return form;
        }

        var rest = form.Remove(RangeField);
        foreach (var pair in ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (pair.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var lower, var upper]
                && rest.TryGetValue(lower, out var low) && rest.TryGetValue(upper, out var high)
                && double.TryParse(low, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(high, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && a > b)
            {
                rest = rest.SetItem(lower, high).SetItem(upper, low);
            }
        }

        return rest;
    }

    private static bool Names(string tokens, string token) => tokens.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(token, StringComparer.Ordinal);

    public static FieldLog Fields(RenderPlan plan, ImmutableArray<object?> models)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var fields = new FieldLog();
        using var discarded = new PooledBuffer();
        plan.Render(discarded, models, fields);
        return fields;
    }

    public static string FieldName(int id) => "f" + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>A field's name in the form: <c>f</c> and its token.</summary>
    public static string FieldName(PlannedField field) => "f" + field.Token;

    /// <summary>
    /// A field's token: its id in the page, its view's slot and its id inside that view (<c>2.0</c>), or its view's slot,
    /// its item's key escaped (<see cref="ViewItems.Token"/>) and its id inside one item of a repeated view (<c>2-7.0</c>).
    /// </summary>
    public static string Token(int view, int id, string? item = null) =>
        view < 0 ? id.ToString(CultureInfo.InvariantCulture)
        : view.ToString(CultureInfo.InvariantCulture) + (item is null ? string.Empty : "-" + ViewItems.Token(item)) + "." + id.ToString(CultureInfo.InvariantCulture);

    public static string ButtonValue(int id, object? parameter) => ButtonValue(Token(-1, id), parameter);

    public static string ButtonValue(string token, object? parameter) => token + "|" + PlanValues.Format(parameter);

    /// <summary>
    /// The view a post's command belongs to: the model slot (and, in a repeated view, the item) named by its button's token,
    /// or by the changed field's token in a post without a button (<see cref="ChangeField"/>), <see cref="ViewTarget.Page"/>
    /// for the page and its layouts (and for a post naming neither), or nothing when the token is malformed. As Blazor's
    /// <c>FormName</c> picks the one form a post is for, the view picks which fields the post binds.
    /// </summary>
    public static ViewTarget? ViewOf(ImmutableDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.TryGetValue(ButtonField, out var posted) && posted.Length > 0)
        {
            return Parse(posted) is { } button ? button.Target : null;
        }

        if (form.TryGetValue(ChangeField, out var changed) && changed.Length > 0)
        {
            return Parse(changed + "|") is { } field ? field.Target : null;
        }

        return ViewTarget.Page;
    }

    internal static (ViewTarget Target, int Id)? Parse(string posted)
    {
        var separator = posted.IndexOf('|', StringComparison.Ordinal);
        if (separator < 0)
        {
            return null;
        }

        var token = ItemKeys.Bare(posted.AsSpan(0, separator));
        var dot = token.LastIndexOf('.');
        if (dot < 0)
        {
            return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? (ViewTarget.Page, id) : null;
        }

        var owner = token[..dot];
        var dash = owner.IndexOf('-');
        var slot = dash < 0 ? owner : owner[..dash];
        return int.TryParse(slot, NumberStyles.None, CultureInfo.InvariantCulture, out var view)
            && int.TryParse(token[(dot + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var inner)
            ? (new ViewTarget(view, dash < 0 ? null : Uri.UnescapeDataString(owner[(dash + 1)..].ToString())), inner)
            : null;
    }

    /// <summary>
    /// Binds a post into the models and runs its command: only the fields of the view the post's button belongs to are
    /// bound (the page's and its layouts' when the button is theirs or there is none), so another view's fields, an
    /// unchecked box absent from the post among them, are left as they are. A choice field takes only a value one of its
    /// options was written with. The button must be on the page with the posted parameter, which is checked before
    /// anything is bound; in a view where a slot renderer writes fields or buttons, which may depend on what the post
    /// carries, a button the render before binding does not write is looked for in the render of the post with the slot
    /// renderer's fields bound (the page the browser showed), and those fields are put back when it is not there either,
    /// so a refused post leaves the models as they were.
    /// </summary>
    public static ValueTask<PostStatus?> ApplyAsync(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver) =>
        ApplyAsync(plan, models, form, resolver, static _ => true);

    /// <summary>
    /// Binds and runs a post as <see cref="ApplyAsync(RenderPlan, ImmutableArray{object?}, ImmutableDictionary{string, string}, IFieldResolver)"/>
    /// does, but only when <paramref name="runs"/> takes the pressed button's command (one the generator found runs on any
    /// thread); nothing is bound and nothing runs otherwise, so the engine answers the post.
    /// </summary>
    public static async ValueTask<PostStatus?> ApplyAsync(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver, Func<SlotSpec, bool> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(resolver);
        Action? undoStates = null;
        if (form.Keys.Any(IsState))
        {
            var written = Fields(plan, models);
            if (ListItemPosts.StatesGone(written, form))
            {
                return PostStatus.Stale;
            }

            if (PlanStates.Restore(written, form, resolver) is not { } restored)
            {
                return null;
            }

            undoStates = restored;
        }

        var fields = SettledFields.Of(plan, models, form, resolver);
        if (!fields.Described || Bindings(fields, resolver) is not { } bindings || Shows(fields, form))
        {
            return null;
        }

        if (ViewOf(form) is not { } target)
        {
            return PostStatus.BadRequest;
        }

        if (ListItemPosts.HoldsItems(plan, models, form, resolver, (fields, bindings, target)) is not { } held)
        {
            undoStates?.Invoke();
            return PostStatus.Stale;
        }

        if (held.ChangedOnly)
        {
            Bind(bindings, target, form, field => field.Named(form[ChangeField]));
            return PostStatus.Rendered;
        }

        if (ReferenceEquals(held.Fields, fields))
        {
            return await PressAsync(plan, models, form, resolver, runs, (fields, bindings, target));
        }

        return !held.Fields.Described || Bindings(held.Fields, resolver) is not { } again || Shows(held.Fields, form)
            ? null
            : await PressAsync(plan, models, form, resolver, runs, (held.Fields, again, target));
    }

    /// <summary>Binds the post's fields and runs its button, on the fields a render wrote for its items.</summary>
    private static async ValueTask<PostStatus?> PressAsync(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver, Func<SlotSpec, bool> runs, (FieldLog Fields, List<(PlannedField Field, FieldBinding? Binding)> Bindings, ViewTarget Target) post)
    {
        var (fields, bindings, target) = post;
        if (!form.TryGetValue(ButtonField, out var posted) || posted.Length == 0)
        {
            Bind(bindings, target, form, static _ => true);
            return PostStatus.Rendered;
        }

        var button = Parse(posted)!.Value;
        if (Pressed(fields, button) is { Field: null })
        {
            return null;
        }

        var pressed = Find(fields, button, posted, resolver);
        if (pressed is { } first)
        {
            if (first.Field.Field!.Kind != "flip" && !runs(first.Field.Field))
            {
                return null;
            }

            Bind(bindings, target, form, static _ => true);
            return await RunAsync(first, posted);
        }

        return await PressedAfterBindingAsync(plan, models, form, resolver, runs, (fields, bindings, target, button, posted));
    }

    /// <summary>
    /// Looks for a button the render before binding did not write in the render of the post with its view's slot renderer
    /// fields bound (the page the browser showed), and runs it; those fields are put back when it is not there either
    /// (<c>409</c>) or must run on the engine (nothing).
    /// </summary>
    private static async ValueTask<PostStatus?> PressedAfterBindingAsync(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver, Func<SlotSpec, bool> runs,
        (FieldLog Fields, List<(PlannedField Field, FieldBinding? Binding)> Bindings, ViewTarget Target, (ViewTarget Target, int Id) Button, string Posted) post)
    {
        var (fields, bindings, target, button, posted) = post;
        if (!fields.Rerenders(button.Target) || Kept(bindings, target, resolver, static field => field.Rendered) is not { } undo)
        {
            return PostStatus.Conflict;
        }

        Bind(bindings, target, form, static field => field.Rendered);
        var rendered = Fields(plan, models);
        var found = Find(rendered, button, posted, resolver);
        if (found is not { } pressed || !runs(pressed.Field.Field!))
        {
            undo();
            return found is null && Pressed(rendered, button) is not { Field: null } ? PostStatus.Conflict : null;
        }

        Bind(bindings, target, form, static field => !field.Rendered);
        return await RunAsync(pressed, posted);
    }

    /// <summary>
    /// Binds the posted values of the view's fields this filter keeps, the field the post names as changed last, as the
    /// engine binds it (a table's header box selecting every row, then left as it is by the rows' boxes posted before it;
    /// SSR-299); a composite field only when the post names it as the field that changed, or names none, so its stale value
    /// beside a changed member does not overwrite it.
    /// </summary>
    internal static void Bind(List<(PlannedField Field, FieldBinding? Binding)> bindings, ViewTarget target, ImmutableDictionary<string, string> form, Func<PlannedField, bool> keeps)
    {
        var changed = form.TryGetValue(ChangeField, out var named) && named.Length > 0 ? named : null;
        foreach (var (field, binding) in bindings.OrderBy(pair => !pair.Field.Button && changed is not null && pair.Field.Named(changed)))
        {
            if (field.Button || field.Target != target || !keeps(field) || (changed is not null && field.Field?.Kind == CompositeFields.Kind && !field.Named(changed)))
            {
                continue;
            }

            var value = form.GetValueOrDefault(FieldName(field)) ?? (field.ItemKey is null ? null : form.GetValueOrDefault("f" + field.BareToken));
            if (!field.Options.IsDefault && value is not null && !field.Options.Contains(value))
            {
                continue;
            }

            var outer = OuterScopes.Swap(field.Outer);
            try
            {
                binding?.Invoke(field.Scope, value);
            }
            finally
            {
                OuterScopes.Swap(outer);
            }
        }
    }

    /// <summary>What puts back the values the view's slot renderer fields hold now, or nothing when one of them cannot be put back.</summary>
    internal static Action? Kept(List<(PlannedField Field, FieldBinding? Binding)> bindings, ViewTarget target, IFieldResolver resolver, Func<PlannedField, bool> keeps)
    {
        var undo = new List<(PlannedField Field, Action Put)>();
        foreach (var (field, binding) in bindings)
        {
            if (field.Target != target || !keeps(field) || binding is null)
            {
                continue;
            }

            var outer = OuterScopes.Swap(field.Outer);
            try
            {
                if (resolver.Kept(field.Field!, field.Scope) is not { } put)
                {
                    return null;
                }

                undo.Add((field, put));
            }
            finally
            {
                OuterScopes.Swap(outer);
            }
        }

        return () =>
        {
            foreach (var (field, put) in undo)
            {
                var outer = OuterScopes.Swap(field.Outer);
                try
                {
                    put();
                }
                finally
                {
                    OuterScopes.Swap(outer);
                }
            }
        };
    }

    /// <summary>Whether a post on these models is bound and run through the plan: it describes every field they show and the resolver binds or runs each.</summary>
    public static bool Binds(RenderPlan plan, ImmutableArray<object?> models, IFieldResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        try
        {
            var fields = Fields(plan, models);
            return fields.Described && Bindings(fields, resolver) is not null;
        }
        catch (PlanMissException)
        {
            return false;
        }
    }

    /// <summary>The button a post names, looked up among its view's buttons.</summary>
    private static PlannedField? Pressed(FieldLog fields, (ViewTarget Target, int Id) button) =>
        fields.Fields.FirstOrDefault(field => field.Button && field.Target == button.Target && field.Id == button.Id);

    /// <summary>
    /// The kind of a value input whose binding writes nothing back (bound one way, not bound, or with no data context):
    /// a post without it changes nothing, as on the engine; one that carries it shows the posted value only the engine writes.
    /// </summary>
    public const string KeptKind = "kept";

    /// <summary>Whether the post carries a kept field's value, which only the engine shows.</summary>
    private static bool Shows(FieldLog fields, ImmutableDictionary<string, string> form) =>
        fields.Fields.Any(field => !field.Button && field.Field?.Kind == KeptKind && form.ContainsKey(FieldName(field)));

    private static List<(PlannedField Field, FieldBinding? Binding)>? Bindings(FieldLog fields, IFieldResolver resolver)
    {
        var bindings = new List<(PlannedField, FieldBinding?)>(fields.Fields.Count);
        foreach (var field in fields.Fields)
        {
            if (field.Field is null || field.Field.Kind == KeptKind)
            {
                bindings.Add((field, null));
                continue;
            }

            var binding = resolver.Binding(field.Field);
            if (binding is null && resolver.Command(field.Field) is null)
            {
                return null;
            }

            bindings.Add((field, binding));
        }

        return bindings;
    }

    /// <summary>
    /// Runs the pressed button's command with its parameter as it is once the post is bound (the parameter it was found
    /// with was checked against the render), or a drop target's move.
    /// </summary>
    private static async ValueTask<PostStatus> RunAsync((PlannedField Field, FieldCommand Command, object? Parameter) pressed, string posted)
    {
        var (field, command, _) = pressed;
        ValueTask execution = default;
        ValueTask<PostStatus> dropped = default;
        var outer = OuterScopes.Swap(field.Outer);
        try
        {
            var parameter = command.Parameter(field.Scope);
            if (command.DropAsync is { } drop)
            {
                dropped = drop(field.Scope, parameter, posted[(posted.IndexOf('|', StringComparison.Ordinal) + 1)..]);
            }
            else
            {
                execution = command.ExecuteAsync(field.Scope, parameter);
            }
        }
        finally
        {
            OuterScopes.Swap(outer);
        }

        if (command.DropAsync is not null)
        {
            return await dropped;
        }

        await execution;
        return PostStatus.Rendered;
    }

    /// <summary>
    /// The button a post names, its command and its parameter, when this render wrote it with the posted parameter (a drop
    /// target with the posted move after its token), or nothing when the render has no such button.
    /// </summary>
    private static (PlannedField Field, FieldCommand Command, object? Parameter)? Find(FieldLog fields, (ViewTarget Target, int Id) button, string posted, IFieldResolver resolver)
    {
        if (Pressed(fields, button) is not { Field: { } spec } pressed || resolver.Command(spec) is not { } command)
        {
            return null;
        }

        var outer = OuterScopes.Swap(pressed.Outer);
        try
        {
            var parameter = command.Parameter(pressed.Scope);
            var found = command.DropAsync is null
                ? ButtonValue(pressed.Token, parameter) == posted || ButtonValue(pressed.BareToken, parameter) == posted
                : posted.StartsWith(pressed.Token + "|", StringComparison.Ordinal) || posted.StartsWith(pressed.BareToken + "|", StringComparison.Ordinal);
            return found ? (pressed, command, parameter) : null;
        }
        finally
        {
            OuterScopes.Swap(outer);
        }
    }
}
