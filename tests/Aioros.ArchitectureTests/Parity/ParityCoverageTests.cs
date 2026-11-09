using Aioros.ArchitectureTests.Acceptance;

namespace Aioros.ArchitectureTests.Parity;

public sealed class ParityCoverageTests
{
    private const string Document = """
        ## Controls (`control`)

        | Construct | Status | Reason | Criteria and evidence |
        | --- | --- | --- | --- |
        | `Ns.Button` | unmeasured | | BUTTONS-01 |
        | `Ns.Window` | n/a | The host window | |

        ## States (`state`)

        | Construct | Status | Reason | Criteria and evidence |
        | --- | --- | --- | --- |
        | `:pointerover` | equal | | `bench:/controls/buttons` |
        | `:dragging` | not supported | Needs script | |
        """;

    private static readonly Construct[] Inventory =
    [
        new("control", "Ns.Button"), new("control", "Ns.Window"), new("state", ":pointerover"), new("state", ":dragging"),
    ];

    [Fact]
    public async Task EveryXamlConstructIsInTheParityInventoryAsync()
    {
        var document = await File.ReadAllTextAsync(Path.Combine(Workspaces.Workspaces.Root, "docs", "parity.md"), TestContext.Current.CancellationToken);
        var criteria = (await AcceptanceWorkspace.LoadAsync(Workspaces.Workspaces.Root)).Criteria.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(ParityCoverage.Check(ParityInventory.All(), document, criteria));
    }

    [Fact]
    public void TheInventoryCoversEveryKindOfConstruct()
    {
        var kinds = ParityInventory.All().Select(construct => construct.Kind).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ParityInventory.Kinds.ToHashSet(StringComparer.Ordinal), kinds);
        Assert.Contains(new Construct("state", ":pointerover"), ParityInventory.All());
        Assert.Contains(new Construct("layout", "DefinitionBase.SharedSizeGroup"), ParityInventory.All());
    }

    [Fact]
    public void AWellFormedInventoryPasses()
    {
        Assert.Empty(ParityCoverage.Check(Inventory, Document, new HashSet<string> { "BUTTONS-01" }));
    }

    [Fact]
    public void GapsAreReported()
    {
        const string Broken = """
            ## Controls (`control`)

            | `Ns.Button` | equal | | |
            | `Ns.Ghost` | unmeasured | | BUTTONS-99 |
            | `Ns.Window` | n/a | | |
            | `Ns.Bare` | unmeasured | | |

            ## States (`state`)

            | `:pointerover` | maybe | | |
            | `:dragging` | different | | |
            """;

        Assert.Equal(
            [
                "control Ns.Missing: not in the parity inventory",
                "control Ns.Button: equal without evidence",
                "control Ns.Ghost: not a construct of Avalonia",
                "control Ns.Ghost: names BUTTONS-99, which does not exist",
                "control Ns.Window: n/a without a reason",
                "control Ns.Bare: unmeasured without a criterion or a reason",
                "state :pointerover: unknown status 'maybe'",
                "state :dragging: different without evidence",
                "state :dragging: different without a reason",
            ],
            ParityCoverage.Check([.. Inventory, new("control", "Ns.Bare"), new("control", "Ns.Missing")], Broken, new HashSet<string> { "BUTTONS-01" }));
    }
}
