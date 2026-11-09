using System.Collections.Immutable;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Themes;

namespace Aioros.Targets.Ssr.Pages;

/// <summary>
/// Proves a page's local inputs the guarantee does not cover (decision "Guaranteed scope, closed per control type",
/// verified per page): in the light and the dark theme, each value of each such input's domain (<see cref="LocalDomain"/>)
/// is posted through the plan into one load of the page's models and through the engine into another, and the plan's render
/// is compared with the engine's own; an input is proven when every value agrees. Inputs no code outside the ecosystem
/// observes are the only ones planned as local (<c>GuaranteedInputs</c>), so the walk runs none of the app's code-behind.
/// </summary>
public static class LocalProofs
{
    public static async Task<LocalProof> ProveAsync(PageDefinition page, PageRequest request, IServiceProvider services, PageHandler handler, IPageEngine engine, IFieldResolver resolver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);
        var walk = new Walk(page, request, services, handler, engine, resolver, cancellationToken);
        var walked = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<LocalDisproof>();
        foreach (var theme in (string[])[ThemeSwitch.Light, ThemeSwitch.Dark])
        {
            await walk.ThemeAsync(theme, walked, failures);
        }

        var disproved = failures.Select(static failure => failure.Field).ToHashSet(StringComparer.Ordinal);
        return new LocalProof([.. walked.Where(field => !disproved.Contains(field)).Order(StringComparer.Ordinal)], [.. failures]);
    }

    private sealed record Walk(PageDefinition Page, PageRequest Request, IServiceProvider Services, PageHandler Handler, IPageEngine Engine, IFieldResolver Resolver, CancellationToken CancellationToken)
    {
        public async Task ThemeAsync(string theme, HashSet<string> walked, List<LocalDisproof> failures)
        {
            var models = await LoadAsync(theme);
            var plan = (await Handler.EntryAsync(Page, models, theme, CancellationToken)).Page.Plan;
            if (!plan.HasUnguaranteedLocals)
            {
                return;
            }

            var written = PlanParity.Render(plan, models, Request.Path);
            var unguaranteed = PlanForms.Unguaranteed(plan, models).ToHashSet(StringComparer.Ordinal);
            var specs = PlanForms.Fields(plan, models).Fields.Where(field => field.Field is not null && unguaranteed.Contains(PlanForms.FieldName(field)))
                .GroupBy(PlanForms.FieldName).ToDictionary(static group => group.Key, static group => group.First().Field!, StringComparer.Ordinal);
            var form = PostedForms.Of(written);
            foreach (var field in PostedForms.Fields(written).Where(field => specs.ContainsKey(field.Name)))
            {
                walked.Add(field.Name);
                failures.AddRange(await FieldAsync(plan, form, field, specs[field.Name], theme));
            }
        }

        private async Task<List<LocalDisproof>> FieldAsync(RenderPlan plan, ImmutableDictionary<string, string> form, PostedField field, SlotSpec spec, string theme)
        {
            var values = LocalDomain.Values(spec, field).ToList();
            if (values.Count < LocalDomain.Least(spec))
            {
                return [new LocalDisproof(field.Name, field.Kind, theme, null, "a domain of fewer than two values")];
            }

            var failures = new List<LocalDisproof>();
            foreach (var value in values)
            {
                var posted = (value is null ? form.Remove(field.Name) : form.SetItem(field.Name, value)).SetItem(PlanForms.ChangeField, field.Name[1..]);
                if (await DisagreementAsync(plan, posted, theme) is { } reason)
                {
                    failures.Add(new LocalDisproof(field.Name, field.Kind, theme, value, reason));
                }
            }

            return failures;
        }

        private async Task<string?> DisagreementAsync(RenderPlan plan, ImmutableDictionary<string, string> posted, string theme)
        {
            string mine;
            PostStatus? status;
            var own = await LoadAsync(theme);
            using (LocalValues.Use())
            {
                try
                {
                    status = await PlanForms.ApplyAsync(plan, own, posted, Resolver, static _ => false);
                    mine = PlanParity.Normal(PlanParity.Render(plan, own, Request.Path));
                }
                catch (PlanMissException miss)
                {
                    return "the plan does not hold the value: " + miss.Message;
                }
            }

            if (status != PostStatus.Rendered)
            {
                return $"the plan answers {status?.ToString() ?? "nothing"}";
            }

            var theirs = await LoadAsync(theme);
            var outcome = await Engine.PostAsync(Page, theirs, posted, [], theme, CancellationToken);
            return outcome.Page is { } rendered
                ? PlanParity.Difference(mine, PlanParity.Normal(PlanParity.Render(rendered.Plan, theirs, Request.Path)))
                : $"the engine answers {outcome.Status}";
        }

        private async Task<ImmutableArray<object?>> LoadAsync(string theme) => (await Handler.LoadAsync(Services, Page, Request, theme, CancellationToken)).Models;
    }
}

/// <summary>A page's proof: the fields of its inputs outside the guarantee whose every value agreed, and each value that did not.</summary>
public sealed record LocalProof(ImmutableArray<string> Proven, ImmutableArray<LocalDisproof> Failures);

/// <summary>A value of an input outside the guarantee the plan renders otherwise than the engine, in a theme, and where the two renders part.</summary>
public sealed record LocalDisproof(string Field, string Kind, string Theme, string? Value, string Reason);
