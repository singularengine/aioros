using System.Collections.Immutable;

namespace Aioros.Routing.Templates;

public sealed class RouteTemplate
{
    private readonly ImmutableArray<RouteSegment> _segments;

    private RouteTemplate(string text, ImmutableArray<RouteSegment> segments)
    {
        Text = text;
        _segments = segments;
        Shape = "/" + string.Join('/', segments.Select(segment => segment.Shape));
    }

    public string Text { get; }

    internal string Shape { get; }

    public static RouteTemplate Parse(string template)
    {
        var segments = template.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => RouteSegment.Parse(part, template))
            .ToImmutableArray();
        var catchAll = segments.IndexOf(segments.FirstOrDefault(segment => segment.Kind == SegmentKind.CatchAll));
        if (catchAll >= 0 && catchAll != segments.Length - 1)
        {
            throw new ArgumentException($"The catch-all parameter must be the last segment of '{template}'.", nameof(template));
        }

        return new RouteTemplate(template, segments);
    }

    public RouteMatch Match(string path) => Match(path.AsSpan());

    public RouteMatch Match(ReadOnlySpan<char> path)
    {
        var parts = new PathSegments(path);
        ImmutableDictionary<string, string>.Builder? values = null;
        foreach (var segment in _segments)
        {
            if (!parts.MoveNext() || !segment.Accepts(parts.Current))
            {
                return RouteMatch.None;
            }

            if (segment.Kind == SegmentKind.Literal)
            {
                continue;
            }

            values ??= ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
            if (segment.Kind == SegmentKind.CatchAll)
            {
                values[segment.Value] = Uri.UnescapeDataString(parts.Rest);
                return new RouteMatch(IsMatch: true, Text, new RouteValues(values.ToImmutable()));
            }

            values[segment.Value] = Uri.UnescapeDataString(parts.Current);
        }

        return parts.MoveNext()
            ? RouteMatch.None
            : new RouteMatch(IsMatch: true, Text, values is null ? RouteValues.Empty : new RouteValues(values.ToImmutable()));
    }

    internal int ComparePrecedence(RouteTemplate other)
    {
        var shared = Math.Min(_segments.Length, other._segments.Length);
        for (var i = 0; i < shared; i++)
        {
            var order = _segments[i].Kind.CompareTo(other._segments[i].Kind);
            if (order != 0)
            {
                return order;
            }
        }

        return other._segments.Length.CompareTo(_segments.Length);
    }
}
