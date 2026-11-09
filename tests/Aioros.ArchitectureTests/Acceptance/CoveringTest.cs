using System.Collections.Immutable;

namespace Aioros.ArchitectureTests.Acceptance;

public sealed record CoveringTest(string Name, ImmutableArray<string> Criteria);
