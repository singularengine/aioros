namespace Aioros.Routing.Templates;

public readonly record struct RouteMatch(bool IsMatch, string Template, RouteValues Values)
{
    public static RouteMatch None { get; } = new(IsMatch: false, string.Empty, RouteValues.Empty);
}
