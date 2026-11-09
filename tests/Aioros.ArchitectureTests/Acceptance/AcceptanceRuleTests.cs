namespace Aioros.ArchitectureTests.Acceptance;

public sealed class AcceptanceRuleTests
{
    public static TheoryData<string> RuleNames { get; } = [.. AcceptanceRules.All.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(RuleNames))]
    public async Task TheRepositoryFollowsTheRuleAsync(string rule)
    {
        var workspace = await AcceptanceWorkspace.LoadAsync(Workspaces.Workspaces.Root);

        Assert.Empty(AcceptanceRules.All[rule](workspace).Select(v => v.ToString()));
    }

    [Theory]
    [MemberData(nameof(RuleNames))]
    public async Task TheCompliantFixtureFollowsTheRuleAsync(string rule)
    {
        var workspace = await AcceptanceWorkspace.LoadAsync(Workspaces.Workspaces.Fixture("Acceptance", "Compliant"));

        Assert.Empty(AcceptanceRules.All[rule](workspace).Select(v => v.ToString()));
    }

    [Theory]
    [MemberData(nameof(RuleNames))]
    public async Task TheRuleFindsExactlyTheViolationsPlantedInTheViolatingFixtureAsync(string rule)
    {
        var workspace = await AcceptanceWorkspace.LoadAsync(Workspaces.Workspaces.Fixture("Acceptance", "Violating"));

        var found = AcceptanceRules.All[rule](workspace).Select(v => v.ToString()).Order(StringComparer.Ordinal);

        Assert.Equal(ExpectedAcceptanceViolations.InViolatingFixture[rule], found);
    }

    [Fact]
    public void EveryRuleDeclaresTheViolationsItMustFind() =>
        Assert.Equal(AcceptanceRules.All.Keys.Order(StringComparer.Ordinal), ExpectedAcceptanceViolations.InViolatingFixture.Keys.Order(StringComparer.Ordinal));
}
