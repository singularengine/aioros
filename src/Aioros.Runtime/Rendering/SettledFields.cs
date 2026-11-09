using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

/// <summary>The page's fields as a post leaves them: its local inputs bound in Avalonia's order, each as the inputs before it leave it (ENGINE-324).</summary>
internal static class SettledFields
{
    /// <summary>
    /// The page's fields once the post's values of its local inputs are written into the request's own state, one input at
    /// a time in the order Avalonia binds them (the page's order, the changed field last), the page described again after
    /// each whose value changed: an input another local input enables or disables (<c>IsEnabled="{Binding !#a.IsChecked}"</c>)
    /// is bound as it then is, its posted value dropped while it is disabled; each by the name the browser gave it, while
    /// that name still names the same input (an input another hides or shows shifts only the names of those after it).
    /// </summary>
    public static FieldLog Of(RenderPlan plan, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, IFieldResolver resolver)
    {
        var fields = PlanForms.Fields(plan, models);
        if (!LocalValues.InEffect || PlanForms.ViewOf(form) is not { } target)
        {
            return fields;
        }

        var changed = form.TryGetValue(PlanForms.ChangeField, out var named) && named.Length > 0 ? named : null;
        var browser = Locals(fields, resolver).GroupBy(static local => local.Field.Token, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => Key(group.First().Field), StringComparer.Ordinal);
        var current = fields;
        foreach (var token in browser.Keys.OrderBy(token => token == changed).ToList())
        {
            if (!Bound(Locals(current, resolver).Where(local => local.Field.Token == token && Key(local.Field) == browser[token]).ToList(), target, form))
            {
                continue;
            }

            current = PlanForms.Fields(plan, models);
            fields = current.Fields.Select(static field => field.Token).SequenceEqual(fields.Fields.Select(static field => field.Token)) ? current : fields;
        }

        return fields;
    }

    /// <summary>The key of the local input a field posts, which a field keeps while the page is described again.</summary>
    private static int Key(PlannedField field) => Slots.LocalPaths.TryRead(field.Field!.Path!, out var path) ? path.Key : -1;

    private static readonly object Unset = new();

    /// <summary>Binds one local input's field and says whether the request's own state took another value for it.</summary>
    private static bool Bound(List<(PlannedField Field, FieldBinding? Binding)> local, ViewTarget target, ImmutableDictionary<string, string> form)
    {
        if (local.Count == 0 || !Slots.LocalPaths.TryRead(local[0].Field.Field!.Path!, out var path))
        {
            return false;
        }

        var before = LocalValues.TryGet(path.Key, out var held) ? held : Unset;
        PlanForms.Bind(local, target, form, static _ => true);
        return !Equals(before, LocalValues.TryGet(path.Key, out var now) ? now : Unset);
    }

    public static List<(PlannedField Field, FieldBinding? Binding)> Locals(FieldLog fields, IFieldResolver resolver) =>
        [.. fields.Fields.Where(static field => !field.Button && field.Field is { Path: { } path } && Slots.LocalPaths.TryRead(path, out _)).Select(field => (field, resolver.Binding(field.Field!)))];
}
