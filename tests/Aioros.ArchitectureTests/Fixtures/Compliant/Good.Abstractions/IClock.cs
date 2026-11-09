namespace Good.Abstractions;

/// <summary>Documentation comments on the public API are allowed.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
