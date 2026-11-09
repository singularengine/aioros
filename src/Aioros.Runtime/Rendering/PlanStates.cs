using System.Collections.Immutable;
using System.Globalization;

namespace Aioros.Runtime.Rendering;

/// <summary>The travelling states of a post (<c>_s{n}</c>, <c>_m{n}</c>) on a plan: which written state each name stands for, and restoring them.</summary>
public static class PlanStates
{
    /// <summary>
    /// Writes each travelling state the post carries into the models through the binding its state node records, before
    /// the post's fields are found and bound, as the engine restores the states into its controls; whether every posted
    /// state had a node the resolver binds, nothing being written otherwise; what puts the states back, or nothing when they cannot be restored.
    /// </summary>
    public static Action? Restore(FieldLog log, ImmutableDictionary<string, string> form, IFieldResolver resolver)
    {
        var restores = new List<(PlannedState State, FieldBinding Binding, string Value)>();
        foreach (var (name, value) in form)
        {
            if (!PlanForms.IsState(name))
            {
                continue;
            }

            if (StateOf(log, name) is not { } state || resolver.Binding(state.Field) is not { } binding)
            {
                return null;
            }

            restores.Add((state, binding, value));
        }

        var undo = new List<Action>();
        foreach (var (state, binding, value) in restores.OrderBy(restore => restore.State.Index))
        {
            var outer = OuterScopes.Swap(state.Outer);
            try
            {
                if (resolver.Kept(state.Field, state.Scope) is { } put)
                {
                    undo.Add(() => Scoped(state.Outer, put));
                }

                binding(state.Scope, value);
            }
            finally
            {
                OuterScopes.Swap(outer);
            }
        }

        return () => undo.ForEach(static put => put());
    }

    private static void Scoped(ImmutableStack<object?> scopes, Action action)
    {
        var outer = OuterScopes.Swap(scopes);
        try
        {
            action();
        }
        finally
        {
            OuterScopes.Swap(outer);
        }
    }

    /// <summary>The state a posted name stands for: the one written with that index and that key.</summary>
    public static PlannedState? StateOf(FieldLog log, string name)
    {
        var bare = ItemKeys.Bare(name.AsSpan(2));
        var suffix = name[(2 + bare.Length)..];
        return int.TryParse(bare, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? log.States.FirstOrDefault(state => state.Index == index && ItemKeys.Suffix(state.ItemKey) == suffix)
            : null;
    }
}
