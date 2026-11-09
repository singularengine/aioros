using System.Buffers;
using System.Collections.Immutable;

namespace Aioros.Runtime.Rendering;

public sealed record RenderPlan(ImmutableArray<IPlanNode> Head, ImmutableArray<IPlanNode> Body, ImmutableArray<IPlanNode> Tail)
{
    public bool HasFields { get; } = PlanScan.HasFields(Head) || PlanScan.HasFields(Body) || PlanScan.HasFields(Tail);

    /// <summary>Whether the plan writes a field of a local input the guarantee does not cover, whose posts it answers only once the page's verification proved it (<see cref="PlanForms.Unguaranteed"/>).</summary>
    public bool HasUnguaranteedLocals { get; } = PlanScan.HasUnguaranteedLocals(Head) || PlanScan.HasUnguaranteedLocals(Body) || PlanScan.HasUnguaranteedLocals(Tail);

    /// <summary>Whether the body writes slot regions (<see cref="SlotRegions"/>), whose answers then carry their hashes.</summary>
    public bool HasSlotRegions { get; } = PlanScan.HasSlotRegions(Body);

    /// <summary>
    /// Whether a slot of the plan may read an outer scope (a <c>^depth</c> path), so its renders keep the scopes around
    /// each nested body; true unless the plan is known to have none.
    /// </summary>
    public bool ReadsOuter { get; init; } = true;

    private PlanTemplate? _template;

    /// <summary>
    /// The body's statics and nested templates, named by a fingerprint: built the first time it is read, since only a
    /// reusable plan's clients ask for it and the engine creates a plan for every literal render and every post it binds.
    /// </summary>
    public PlanTemplate Template => Volatile.Read(ref _template) ?? Interlocked.CompareExchange(ref _template, PlanTemplate.Of(Body), null) ?? _template;

    /// <summary>Whether both plans write the same nodes the same way; the template, built from the body when read, takes no part.</summary>
    public bool Equals(RenderPlan? other) =>
        other is not null && (ReferenceEquals(this, other) || (Head == other.Head && Body == other.Body && Tail == other.Tail && ReadsOuter == other.ReadsOuter));

    public override int GetHashCode() => HashCode.Combine(Head, Body, Tail, ReadsOuter);

    /// <summary>
    /// Writes the body's dynamics for these models as JSON, in the order of <see cref="Template"/>'s slots, the slot
    /// regions' marks carrying <paramref name="nonce"/>: the nonce of the body a post rendered, whose hashes the answer sends.
    /// </summary>
    public void WriteDynamics(IBufferWriter<byte> writer, ImmutableArray<object?> models, string? path = null, PageStyles? styles = null, string? nonce = null) =>
        PlanDynamics.Write(writer, Body, models, path, styles: styles, nonce: nonce);

    /// <summary>
    /// Writes the body's dynamics, and in the same walk the body itself into <paramref name="body"/>, noting its slot
    /// regions in the log it returns: what a page's dynamics answer hashes, without rendering the body a second time.
    /// </summary>
    public SlotRegionLog WriteDynamics(IBufferWriter<byte> writer, ImmutableArray<object?> models, string? path, PooledBuffer body, PageStyles? styles = null) =>
        PlanDynamics.Write(writer, Body, models, path, body, styles)!;

    public void Render(IBufferWriter<byte> writer, ImmutableArray<object?> models, FieldLog? fields = null, string? path = null)
    {
        var output = new HtmlOutput(writer, fields);
        var scope = new RenderScope(models.IsDefaultOrEmpty ? null : models[0], models) { Path = path, Tracked = ReadsOuter };
        PlanValues.WriteAll(ref output, Head.AsSpan(), scope);
        PlanValues.WriteAll(ref output, Body.AsSpan(), scope);
        PlanValues.WriteAll(ref output, Tail.AsSpan(), scope);
    }

    /// <summary>
    /// Writes one view's region alone (one item's, in a repeated view), as it is written inside the body, and tells whether
    /// the body shows it: a view inside a condition that is false, a slot that names no view, or an item that is not in the
    /// list or not shown, is not found and nothing is written.
    /// </summary>
    public bool RenderView(IBufferWriter<byte> writer, ImmutableArray<object?> models, int view, string? item = null, string? path = null, PageStyles? styles = null)
    {
        if (models.IsDefaultOrEmpty || ViewNode.Find(Body, view, new RenderScope(models[0], models) { Path = path, Tracked = ReadsOuter }, item) is not { } found)
        {
            return false;
        }

        var output = new HtmlOutput(writer) { Styles = styles };
        found.View.Write(ref output, found.Scope);
        return true;
    }

    public void RenderBody(IBufferWriter<byte> writer, ImmutableArray<object?> models, string? path = null, SlotRegionLog? regions = null, PageStyles? styles = null)
    {
        var output = new HtmlOutput(writer) { Regions = regions, Styles = styles };
        PlanValues.WriteAll(ref output, Body.AsSpan(), new RenderScope(models.IsDefaultOrEmpty ? null : models[0], models) { Path = path, Tracked = ReadsOuter });
    }
}
