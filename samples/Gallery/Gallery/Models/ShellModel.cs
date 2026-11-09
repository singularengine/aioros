namespace Gallery.Models;

public sealed record Section(string Name);

public sealed class ShellModel(Section section)
{
    public string Current { get; } = section.Name;
}
