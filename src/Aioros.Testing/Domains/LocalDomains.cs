using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Aioros.Avalonia.Hosting;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Aioros.Targets.Ssr.Pages;

namespace Aioros.Testing.Domains;

/// <summary>
/// Walks the domain of every input a page's plan holds the page's own state of (decision "Guaranteed scope, closed per
/// control type"): each flag, each index and none, the two ends of a range and its middle, an empty, a short and a long
/// text, two dates, two times, and compares the plan's render of each value with Avalonia's render of the page posted it.
/// A library's or an app's tests call it once per input type they ship, so the type is closed over its domain; each pair of
/// values of each two inputs is posted too, so inputs that follow one another (a check box enabling another) are closed
/// together.
/// </summary>
public static class LocalDomains
{
    /// <summary>
    /// The disagreements of a page's plan with Avalonia over its local inputs' domains, each naming the field, the value and
    /// where the two renders part; empty when the plan renders every value as Avalonia does. A page whose plan is not
    /// reusable, that holds no local input, or whose input's domain holds fewer than two values is reported too.
    /// </summary>
    /// <param name="engine">The engine that compiles the page and renders it posted.</param>
    /// <param name="xaml">The page's XAML.</param>
    /// <param name="models">The page's models, created anew for each render so no render sees another's state.</param>
    /// <param name="theme">The theme variant the page is rendered in.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public static async Task<ImmutableArray<string>> DisagreementsAsync(AvaloniaPageEngine engine, string xaml, Func<ImmutableArray<object?>> models, string theme, CancellationToken cancellationToken)
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        await foreach (var failure in WalkAsync(engine, xaml, models, theme, cancellationToken))
        {
            failures.Add(failure.Text);
        }

        return failures.ToImmutable();
    }

    /// <summary>
    /// The first value or pair of values whose render through the plan differs from Avalonia's, or nothing when every one
    /// agrees: the walk of <see cref="DisagreementsAsync"/> stopped at its first disagreement, for a search that only asks
    /// whether a page still disagrees (a reduction to the smallest page that does). A page that cannot be walked (a refused
    /// plan, no local input, a domain of one value) disagrees with nothing.
    /// </summary>
    /// <param name="engine">The engine that compiles the page and renders it posted.</param>
    /// <param name="xaml">The page's XAML.</param>
    /// <param name="models">The page's models, created anew for each render so no render sees another's state.</param>
    /// <param name="theme">The theme variant the page is rendered in.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public static async Task<string?> FirstDisagreementAsync(AvaloniaPageEngine engine, string xaml, Func<ImmutableArray<object?>> models, string theme, CancellationToken cancellationToken)
    {
        await foreach (var failure in WalkAsync(engine, xaml, models, theme, cancellationToken))
        {
            if (failure.Disagrees)
            {
                return failure.Text;
            }
        }

        return null;
    }

    private static IAsyncEnumerable<(string Text, bool Disagrees)> WalkAsync(AvaloniaPageEngine engine, string xaml, Func<ImmutableArray<object?>> models, string theme, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(models);
        return WalkingAsync(engine, xaml, models, theme, cancellationToken);
    }

    private static async IAsyncEnumerable<(string Text, bool Disagrees)> WalkingAsync(AvaloniaPageEngine engine, string xaml, Func<ImmutableArray<object?>> models, string theme, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var page = await engine.RenderXamlAsync(xaml, models(), theme, cancellationToken);
        if (!page.Reusable)
        {
            yield return ($"not reusable: {page.Failure}", false);
            yield break;
        }

        var written = Body(page, models());
        var fields = PlanForms.Fields(page.Plan, models()).Fields.Where(static field => field.Field?.Path is { } path && LocalPaths.TryRead(path, out _)).ToList();
        if (fields.Count == 0)
        {
            yield return ("holds no local input", false);
            yield break;
        }

        var forms = fields.SelectMany(field => Forms(field, written)).ToList();
        foreach (var field in fields.Where(field => forms.Count(form => form.Field == field) < LocalDomain.Least(field.Field!)))
        {
            yield return ($"{field.Field!.Kind}: a domain of fewer than two values", false);
        }

        foreach (var (field, form, value) in forms)
        {
            if (await DisagreementAsync(engine, xaml, page, form, models, theme, cancellationToken) is { } reason)
            {
                yield return ($"{field.Field!.Kind}={value ?? "(none)"}: {reason}", true);
            }
        }

        foreach (var (first, second, form) in Pairs(fields, written))
        {
            if (await DisagreementAsync(engine, xaml, page, form, models, theme, cancellationToken) is { } reason)
            {
                yield return ($"{first} and {second}: {reason}", true);
            }
        }
    }

    private static IEnumerable<(PlannedField Field, ImmutableDictionary<string, string> Form, string? Value)> Forms(PlannedField field, string written)
    {
        var name = PlanForms.FieldName(field);
        var posted = PostedForms.Fields(written).FirstOrDefault(posted => posted.Name == name);
        foreach (var value in LocalDomain.Values(field.Field!, posted))
        {
            var form = value is null ? PostedForms.Of(written).Remove(name) : PostedForms.Of(written).SetItem(name, value);
            yield return (field, form, value);
        }
    }

    private static IEnumerable<(string First, string Second, ImmutableDictionary<string, string> Form)> Pairs(List<PlannedField> fields, string written)
    {
        var posted = PostedForms.Fields(written);
        var domains = fields.Select(field => (Name: PlanForms.FieldName(field), Kind: field.Field!.Kind, Values: LocalDomain.Values(field.Field, posted.FirstOrDefault(entry => entry.Name == PlanForms.FieldName(field))).ToList())).DistinctBy(static domain => domain.Name).ToList();
        var form = PostedForms.Of(written);
        for (var one = 0; one < domains.Count; one++)
        {
            for (var two = one + 1; two < domains.Count; two++)
            {
                foreach (var (a, b) in domains[one].Values.SelectMany(a => domains[two].Values.Select(b => (a, b))))
                {
                    yield return ($"{domains[one].Kind}={a ?? "(none)"}", $"{domains[two].Kind}={b ?? "(none)"}", Set(Set(form, domains[one].Name, a), domains[two].Name, b));
                }
            }
        }
    }

    private static ImmutableDictionary<string, string> Set(ImmutableDictionary<string, string> form, string name, string? value) =>
        value is null ? form.Remove(name) : form.SetItem(name, value);

    private static async Task<string?> DisagreementAsync(AvaloniaPageEngine engine, string xaml, CompiledPage page, ImmutableDictionary<string, string> form, Func<ImmutableArray<object?>> models, string theme, CancellationToken cancellationToken)
    {
        string planned;
        PostStatus? status;
        using (LocalValues.Use())
        {
            var own = models();
            try
            {
                status = await PlanForms.ApplyAsync(page.Plan, own, form, new AdapterSlotResolver());
                planned = Body(page, own);
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

        var posted = models();
        var outcome = await engine.PostXamlAsync(xaml, posted, form, [], theme, cancellationToken);
        if (outcome.Page is not { } rendered)
        {
            return $"Avalonia answers {outcome.Status}";
        }

        var expected = Body(rendered, posted);
        var at = expected.AsSpan().CommonPrefixLength(planned);
        return at == expected.Length && at == planned.Length ? null : $"differs at {at}: Avalonia \"{Excerpt(expected, at)}\", the plan \"{Excerpt(planned, at)}\"";
    }

    private static string Body(CompiledPage page, ImmutableArray<object?> models)
    {
        var writer = new ArrayBufferWriter<byte>();
        page.Plan.RenderBody(writer, models);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private static string Excerpt(string html, int at) => html[Math.Max(0, at - 40)..Math.Min(html.Length, at + 60)];
}
