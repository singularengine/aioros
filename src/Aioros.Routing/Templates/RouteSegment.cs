namespace Aioros.Routing.Templates;

internal readonly record struct RouteSegment(SegmentKind Kind, string Value, RouteConstraint Constraint)
{
    public string Shape => Kind switch
    {
        SegmentKind.Literal => Value.ToUpperInvariant(),
        SegmentKind.ConstrainedParameter => "{:" + Constraint.Name + "}",
        SegmentKind.Parameter => "{}",
        _ => "{*}",
    };

    public static RouteSegment Parse(string text, string template)
    {
        if (!text.StartsWith('{') || !text.EndsWith('}'))
        {
            return new RouteSegment(SegmentKind.Literal, text, RouteConstraint.None);
        }

        var inner = text[1..^1];
        if (inner.StartsWith('*'))
        {
            return new RouteSegment(SegmentKind.CatchAll, inner[1..], RouteConstraint.None);
        }

        var colon = inner.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new RouteSegment(SegmentKind.Parameter, inner, RouteConstraint.None)
            : new RouteSegment(SegmentKind.ConstrainedParameter, inner[..colon], RouteConstraint.Parse(inner[(colon + 1)..], template));
    }

    public bool Accepts(ReadOnlySpan<char> part) => Kind switch
    {
        SegmentKind.Literal => part.Equals(Value, StringComparison.OrdinalIgnoreCase),
        SegmentKind.ConstrainedParameter => Constraint.Accepts(part),
        _ => true,
    };
}
