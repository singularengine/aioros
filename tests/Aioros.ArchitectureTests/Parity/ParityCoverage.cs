using System.Text.RegularExpressions;

namespace Aioros.ArchitectureTests.Parity;

public static partial class ParityCoverage
{
    private static readonly string[] Statuses = ["equal", "different", "not supported", "unmeasured", "n/a"];

    private static readonly string[] NeedEvidence = ["equal", "different"];

    private static readonly string[] NeedReason = ["different", "not supported", "n/a"];

    public static IEnumerable<string> Check(IEnumerable<Construct> constructs, string document, IReadOnlySet<string> criteria)
    {
        var inventory = constructs.ToList();
        var rows = Rows(document).ToList();
        var listed = rows.Select(row => row.Construct).ToHashSet();
        foreach (var construct in inventory.Where(construct => !listed.Contains(construct)))
        {
            yield return $"{construct.Kind} {construct.Name}: not in the parity inventory";
        }

        var known = inventory.ToHashSet();
        foreach (var row in rows)
        {
            if (!known.Contains(row.Construct))
            {
                yield return $"{row.Construct.Kind} {row.Construct.Name}: not a construct of Avalonia";
            }

            foreach (var problem in Problems(row, criteria))
            {
                yield return $"{row.Construct.Kind} {row.Construct.Name}: {problem}";
            }
        }
    }

    private static IEnumerable<string> Problems(Row row, IReadOnlySet<string> criteria)
    {
        if (!Statuses.Contains(row.Status))
        {
            yield return $"unknown status '{row.Status}'";
        }

        if (NeedEvidence.Contains(row.Status) && row.Criteria.Count == 0 && !row.Benched)
        {
            yield return $"{row.Status} without evidence";
        }

        if (row.Status == "unmeasured" && row.Criteria.Count == 0 && !row.Benched && row.Reason.Length == 0)
        {
            yield return "unmeasured without a criterion or a reason";
        }

        if (NeedReason.Contains(row.Status) && row.Reason.Length == 0)
        {
            yield return $"{row.Status} without a reason";
        }

        foreach (var criterion in row.Criteria.Where(criterion => !criteria.Contains(criterion)))
        {
            yield return $"names {criterion}, which does not exist";
        }
    }

    private static IEnumerable<Row> Rows(string document)
    {
        string? kind = null;
        foreach (var line in document.Split('\n'))
        {
            if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                kind = heading.Groups["kind"].Value;
                continue;
            }

            if (kind is null || RowPattern().Match(line) is not { Success: true } match)
            {
                continue;
            }

            var evidence = match.Groups["evidence"].Value;
            yield return new Row(
                new Construct(kind, match.Groups["construct"].Value),
                match.Groups["status"].Value.Trim(),
                [.. CriterionPattern().Matches(evidence).Select(criterion => criterion.Value)],
                BenchPattern().IsMatch(evidence),
                match.Groups["reason"].Value.Trim());
        }
    }

    [GeneratedRegex(@"^##\s.*\(`(?<kind>[a-z]+)`\)\s*$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\|\s*`(?<construct>[^`]+)`\s*\|(?<status>[^|]*)\|(?<reason>[^|]*)\|(?<evidence>[^|]*)\|\s*$")]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"[A-Z]+-\d{2,}")]
    private static partial Regex CriterionPattern();

    [GeneratedRegex(@"bench:/\S*")]
    private static partial Regex BenchPattern();

    private sealed record Row(Construct Construct, string Status, IReadOnlyList<string> Criteria, bool Benched, string Reason);
}
