using Aioros.ArchitectureTests.Workspaces;

namespace Aioros.ArchitectureTests.Rules;

public sealed class ArchitectureRuleTests
{
    public static TheoryData<string> RuleNames { get; } = [.. ArchitectureRules.All.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(RuleNames))]
    public async Task TheFrameworkFollowsTheRuleAsync(string rule)
    {
        var workspace = await Workspaces.Workspaces.ProductionAsync();

        Assert.Empty(ArchitectureRules.All[rule](workspace).Select(v => v.ToString()));
    }

    [Theory]
    [MemberData(nameof(RuleNames))]
    public async Task TheCompliantFixtureFollowsTheRuleAsync(string rule)
    {
        var workspace = await Workspaces.Workspaces.CompliantAsync();

        Assert.Empty(ArchitectureRules.All[rule](workspace).Select(v => v.ToString()));
    }

    [Theory]
    [MemberData(nameof(RuleNames))]
    public async Task TheRuleFindsExactlyTheViolationsPlantedInTheViolatingFixtureAsync(string rule)
    {
        var workspace = await Workspaces.Workspaces.ViolatingAsync();

        var found = ArchitectureRules.All[rule](workspace).Select(v => v.ToString()).Order(StringComparer.Ordinal);

        Assert.Equal(ExpectedViolations.InViolatingFixture[rule], found);
    }

    [Fact]
    public void EveryRuleDeclaresTheViolationsItMustFind() =>
        Assert.Equal(ArchitectureRules.All.Keys.Order(StringComparer.Ordinal), ExpectedViolations.InViolatingFixture.Keys.Order(StringComparer.Ordinal));
}
