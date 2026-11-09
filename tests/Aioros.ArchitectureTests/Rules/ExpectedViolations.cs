using System.Collections.Immutable;

namespace Aioros.ArchitectureTests.Rules;

public static class ExpectedViolations
{
    public static ImmutableDictionary<string, ImmutableArray<string>> InViolatingFixture { get; } =
        new Dictionary<string, ImmutableArray<string>>
        {
            ["every-project-is-mapped"] = ["Ghost.Project: in the project map but not on disk", "Unmapped.Project: not in the project map"],
            ["references-follow-roles"] = ["Bad.Feature: references Bad.Target (WebTarget)"],
            ["avalonia-only-in-adapters"] = ["Bad.Feature: Avalonia in BadFeature.cs", "Bad.Feature: package Avalonia"],
            ["aspnetcore-only-in-web-targets"] = ["Bad.Feature: Microsoft.AspNetCore.Http in BadFeature.cs", "Bad.Feature: framework Microsoft.AspNetCore.App"],
            ["every-class-is-sealed"] = ["Bad.Feature: BadFeature is not sealed", "Bad.Feature: Settings is not sealed"],
            ["no-locks"] = ["Bad.Feature: lock in BadFeature.cs"],
            ["no-reflection-on-the-request-path"] = ["Bad.Feature: System.Reflection in BadFeature.cs"],
            ["no-comments"] = ["Bad.Feature: comment in BadFeature.cs"],
            ["no-type-over-600-lines"] = ["Bad.Feature: HugeType is longer than 600 lines"],
            ["no-avalonia-namespace-outside-the-adapter"] = ["Bad.Target: namespace Bad.Avalonia.Controls shadows Avalonia in Shadowing.cs"],
        }.ToImmutableDictionary();
}
