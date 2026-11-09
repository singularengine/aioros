using System.Collections.Immutable;
using Aioros.ArchitectureTests.Rules;

namespace Aioros.ArchitectureTests.Acceptance;

public static class AcceptanceRules
{
    public static ImmutableDictionary<string, Func<AcceptanceWorkspace, IEnumerable<Violation>>> All { get; } =
        new Dictionary<string, Func<AcceptanceWorkspace, IEnumerable<Violation>>>
        {
            ["criteria-are-unique"] = CriteriaAreUnique,
            ["every-criterion-has-a-test"] = EveryCriterionHasATest,
            ["one-test-per-criterion"] = OneTestPerCriterion,
            ["every-test-has-a-criterion"] = EveryTestHasACriterion,
            ["tests-name-known-criteria"] = TestsNameKnownCriteria,
        }.ToImmutableDictionary();

    private static IEnumerable<Violation> CriteriaAreUnique(AcceptanceWorkspace workspace) =>
        workspace.Criteria.GroupBy(id => id).Where(g => g.Count() > 1)
            .Select(g => new Violation(g.Key, $"defined {g.Count()} times"));

    private static IEnumerable<Violation> EveryCriterionHasATest(AcceptanceWorkspace workspace) =>
        workspace.Criteria.Distinct().Where(id => !workspace.Tests.Any(t => t.Criteria.Contains(id)))
            .Select(id => new Violation(id, "has no test"));

    private static IEnumerable<Violation> OneTestPerCriterion(AcceptanceWorkspace workspace) =>
        from test in workspace.Tests
        from id in test.Criteria
        group test.Name by id into covering
        where covering.Count() > 1
        select new Violation(covering.Key, $"has {covering.Count()} tests ({string.Join(", ", covering.Order(StringComparer.Ordinal))})");

    private static IEnumerable<Violation> EveryTestHasACriterion(AcceptanceWorkspace workspace) =>
        workspace.Tests.Where(t => t.Criteria.IsEmpty).Select(t => new Violation(t.Name, "has no acceptance criterion"));

    private static IEnumerable<Violation> TestsNameKnownCriteria(AcceptanceWorkspace workspace) =>
        from test in workspace.Tests
        from id in test.Criteria
        where !workspace.Criteria.Contains(id)
        select new Violation(test.Name, $"names {id}, which is not defined");
}
