using System.Globalization;

namespace Aioros.Routing.Templates;

internal readonly record struct RouteConstraint(string Name)
{
    private static readonly string[] Known = ["int", "long", "guid", "bool"];

    public static RouteConstraint None { get; } = new(string.Empty);

    public static RouteConstraint Parse(string name, string template) =>
        Array.IndexOf(Known, name) >= 0
            ? new RouteConstraint(name)
            : throw new ArgumentException($"Unknown route constraint '{name}' in '{template}'.", nameof(template));

    public bool Accepts(ReadOnlySpan<char> value) => Name switch
    {
        "int" => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
        "long" => long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
        "guid" => Guid.TryParse(value, out _),
        "bool" => bool.TryParse(value, out _),
        _ => true,
    };
}
