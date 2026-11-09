namespace Aioros.ArchitectureTests.Rules;

public sealed record Violation(string Project, string Detail)
{
    public override string ToString() => $"{Project}: {Detail}";
}
