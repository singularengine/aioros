using System.Collections.Immutable;

namespace Aioros.Routing.Templates;

public sealed class RouteValues(ImmutableDictionary<string, string> values)
{
    public static RouteValues Empty { get; } = new(ImmutableDictionary<string, string>.Empty);

    public int Count => values.Count;

    public ImmutableDictionary<string, string> All => values;

    public string this[string name] => values[name];

    public bool Contains(string name) => values.ContainsKey(name);
}
