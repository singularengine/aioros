using System.Text.Json.Serialization;

namespace Showcase.Board;

public enum Priority
{
    Low,
    Medium,
    High,
    Urgent,
}

/// <summary>A card on the board. Each kind has its own template in XAML, picked by its type.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TaskItem), "task")]
[JsonDerivedType(typeof(BugItem), "bug")]
[JsonDerivedType(typeof(EpicItem), "epic")]
public abstract record BoardItem(string Id, string Title, Priority Priority, string? Owner, DateOnly? Due, bool Blocked);

public sealed record TaskItem(string Id, string Title, Priority Priority, string? Owner, DateOnly? Due, bool Blocked, string Area)
    : BoardItem(Id, Title, Priority, Owner, Due, Blocked);

public sealed record BugItem(string Id, string Title, Priority Priority, string? Owner, DateOnly? Due, bool Blocked, int Reports, string Environment)
    : BoardItem(Id, Title, Priority, Owner, Due, Blocked);

public sealed record EpicItem(string Id, string Title, Priority Priority, string? Owner, DateOnly? Due, bool Blocked, int Done, int Total)
    : BoardItem(Id, Title, Priority, Owner, Due, Blocked);

public sealed record BoardLane(string Key, string Title, int Limit, IReadOnlyList<BoardItem> Items);

public sealed record BoardState(IReadOnlyList<BoardLane> Lanes);
