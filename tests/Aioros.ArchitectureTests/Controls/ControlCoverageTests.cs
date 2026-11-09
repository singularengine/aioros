using Aioros.ArchitectureTests.Acceptance;
using Aioros.ArchitectureTests.Workspaces;

namespace Aioros.ArchitectureTests.Controls;

public sealed class ControlCoverageTests
{
    private const string Matrix = """
        | Control | Family | Web | Status | AC |
        | --- | --- | --- | --- | --- |
        | `Ns.Done` | Text | A div | done | TEXT-01 |
        | `Ns.Infra` | — | Never on a page | n/a | |
        """;

    [Fact]
    public async Task EveryAvaloniaControlIsCoveredAsync()
    {
        var matrix = await File.ReadAllTextAsync(Path.Combine(Workspaces.Workspaces.Root, "docs", "controls.md"), TestContext.Current.CancellationToken);
        var criteria = (await AcceptanceWorkspace.LoadAsync(Workspaces.Workspaces.Root)).Criteria.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(ControlCoverage.Check(ControlCoverage.AvaloniaControls(), matrix, criteria));
    }

    [Fact]
    public void AWellFormedMatrixPasses()
    {
        Assert.Empty(ControlCoverage.Check(["Ns.Done", "Ns.Infra"], Matrix, new HashSet<string> { "TEXT-01" }));
    }

    [Fact]
    public void GapsAreReported()
    {
        const string Broken = """
            | `Ns.Done` | Text | A div | done | |
            | `Ns.Ghost` | Text | A div | done | TEXT-99 |
            | `Ns.Infra` | — |  | n/a | |
            | `Ns.Odd` | Text | A div | maybe | |
            | `Ns.Later` | Text | A div | todo | |
            """;

        Assert.Equal(
            [
                "Ns.Missing: not in the control matrix",
                "Ns.Done: done without acceptance criteria",
                "Ns.Ghost: names TEXT-99, which does not exist",
                "Ns.Infra: n/a without a reason",
                "Ns.Odd: unknown status 'maybe'",
                "Ns.Later: not done yet",
            ],
            ControlCoverage.Check(["Ns.Done", "Ns.Missing"], Broken, new HashSet<string> { "TEXT-01" }));
    }
}
