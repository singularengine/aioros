using System.Collections.Immutable;
using Aioros.Routing.Templates;

namespace Aioros.Routing.Tables;

public sealed class RouteTable
{
    private readonly ImmutableArray<RouteTemplate> _routes;

    private RouteTable(ImmutableArray<RouteTemplate> routes) => _routes = routes;

    public static RouteTable Empty { get; } = new([]);

    public int Count => _routes.Length;

    public RouteTable Add(string template)
    {
        var route = RouteTemplate.Parse(template);
        var conflict = _routes.FirstOrDefault(existing => string.Equals(existing.Shape, route.Shape, StringComparison.Ordinal));
        if (conflict is not null)
        {
            throw new ArgumentException($"'{template}' conflicts with the existing route '{conflict.Text}'.", nameof(template));
        }

        var index = 0;
        while (index < _routes.Length && _routes[index].ComparePrecedence(route) <= 0)
        {
            index++;
        }

        return new RouteTable(_routes.Insert(index, route));
    }

    public RouteMatch Match(string path) => Match(path.AsSpan());

    public RouteMatch Match(ReadOnlySpan<char> path)
    {
        foreach (var route in _routes)
        {
            var match = route.Match(path);
            if (match.IsMatch)
            {
                return match;
            }
        }

        return RouteMatch.None;
    }
}
