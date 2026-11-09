using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>The fields a post's items are bound on, and whether it binds only the field it names as changed.</summary>
public sealed record HeldItems(FieldLog Fields, bool ChangedOnly);

/// <summary>Whether the lists and the list items a post names still stand where the page wrote them (decision "List items posted by key").</summary>
public static class ListItemPosts
{
    /// <summary>
    /// The page's fields once the post's lists are the ones its page wrote. Otherwise the fields written before the first
    /// list, which no list can shift, are bound (a list that follows a filter) and the lists looked at again, the field the
    /// post names as changed left out, since the browser showed them before that change; when they still differ, a post
    /// that only changed such a field binds that field alone, and anything else is refused with what was bound put back.
    /// </summary>
    public static HeldItems? HoldsItems(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver, (FieldLog Fields, List<(PlannedField Field, FieldBinding? Binding)> Bindings, ViewTarget Target) post)
    {
        var (fields, bindings, target) = post;
        if (Holds(fields, form, target))
        {
            return new HeldItems(fields, false);
        }

        var changed = form.TryGetValue(PlanForms.ChangeField, out var named) && named.Length > 0 ? named : null;
        var limit = Limit(form, target, fields.ShapeOf(target));
        bool Early(PlannedField field) => !field.Button && !field.Listed && field.Id < limit;
        bool Before(PlannedField field) => Early(field) && !field.Named(changed);
        var undo = bindings.Exists(pair => pair.Field.Target == target && Before(pair.Field)) ? PlanForms.Kept(bindings, target, resolver, Before) : null;
        if (undo is not null)
        {
            PlanForms.Bind(bindings, target, form, Before);
            var again = PlanForms.Fields(plan, models);
            if (Holds(again, form, target))
            {
                return new HeldItems(again, false);
            }
        }

        if (changed is not null && !form.ContainsKey(PlanForms.ButtonField) && bindings.Exists(pair => pair.Field.Target == target && Early(pair.Field) && pair.Field.Named(changed)))
        {
            return new HeldItems(fields, true);
        }

        undo?.Invoke();
        return null;
    }

    private static bool Holds(FieldLog fields, ImmutableDictionary<string, string> form, ViewTarget target) =>
        ShapeHolds(fields.ShapeOf(target), form, target) && !Stale(fields.Fields.Select(static field => field.Token), form, target);

    /// <summary>Whether the post carries no mark of its region's lists, or the mark of the lists this render wrote.</summary>
    public static bool ShapeHolds(string? mark, ImmutableDictionary<string, string> form, ViewTarget target)
    {
        ArgumentNullException.ThrowIfNull(form);
        return !form.TryGetValue(PlanForms.ShapeName(target), out var posted) || ItemKeys.ShapeDigest(posted).SequenceEqual(ItemKeys.ShapeDigest(mark));
    }

    /// <summary>The id below which a field stands before every list of the page the post came from and of this render.</summary>
    public static int Limit(ImmutableDictionary<string, string> form, ViewTarget target, string? mark)
    {
        ArgumentNullException.ThrowIfNull(form);
        return Math.Min(ItemKeys.ShapeFirst(form.GetValueOrDefault(PlanForms.ShapeName(target))), ItemKeys.ShapeFirst(mark));
    }

    /// <summary>Whether the post carries a travelling state of a list item this render does not write.</summary>
    public static bool StatesGone(FieldLog fields, ImmutableDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(form);
        return form.Keys.Any(name => PlanForms.IsState(name) && ItemKeys.Keyed(name) && PlanStates.StateOf(fields, name) is null);
    }

    /// <summary>Whether the post names a list item of its view by a key the render does not write at that token: its fields, its button or the field it changed.</summary>
    public static bool Stale(IEnumerable<string> written, ImmutableDictionary<string, string> form, ViewTarget target)
    {
        ArgumentNullException.ThrowIfNull(written);
        ArgumentNullException.ThrowIfNull(form);
        var named = KeyedTokens(form).Where(token => PlanForms.Parse(token + "|") is { } field && field.Target == target).ToList();
        if (named.Count == 0)
        {
            return false;
        }

        var tokens = written.ToHashSet(StringComparer.Ordinal);
        return !named.TrueForAll(tokens.Contains);
    }

    /// <summary>The tokens of the list items a post names: its fields', its button's and the changed field's.</summary>
    private static IEnumerable<string> KeyedTokens(ImmutableDictionary<string, string> form)
    {
        var posted = form.Keys.Where(static name => name.StartsWith('f') && ItemKeys.Keyed(name)).Select(static name => name[1..]);
        var button = form.GetValueOrDefault(PlanForms.ButtonField) is { } value && value.IndexOf('|', StringComparison.Ordinal) is var bar and > 0 ? value[..bar] : string.Empty;
        return posted.Concat(new[] { button, form.GetValueOrDefault(PlanForms.ChangeField) ?? string.Empty }.Where(static token => ItemKeys.Keyed(token)));
    }
}
