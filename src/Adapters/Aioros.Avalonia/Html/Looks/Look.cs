namespace Aioros.Avalonia.Html.Looks;

internal sealed record Look(string Css, IReadOnlyList<LookState> States)
{
    public static Look None { get; } = new(string.Empty, []);
}

internal readonly record struct LookState(string Selector, string Rule);
