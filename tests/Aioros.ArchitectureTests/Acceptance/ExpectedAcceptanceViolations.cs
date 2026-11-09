using System.Collections.Immutable;

namespace Aioros.ArchitectureTests.Acceptance;

public static class ExpectedAcceptanceViolations
{
    public static ImmutableDictionary<string, ImmutableArray<string>> InViolatingFixture { get; } =
        new Dictionary<string, ImmutableArray<string>>
        {
            ["criteria-are-unique"] = ["AREA-04: defined 2 times"],
            ["every-criterion-has-a-test"] = ["AREA-02: has no test", "AREA-04: has no test"],
            ["one-test-per-criterion"] = ["AREA-03: has 2 tests (FirstDuplicate, SecondDuplicate)"],
            ["every-test-has-a-criterion"] = ["NoCriterion: has no acceptance criterion"],
            ["tests-name-known-criteria"] = ["UnknownCriterion: names AREA-99, which is not defined"],
        }.ToImmutableDictionary();
}
