using System.Text.RegularExpressions;

namespace Aioros.ArchitectureTests.Controls;

public static partial class ControlCoverage
{
    private static readonly string[] Statuses = ["done", "n/a"];

    public static IEnumerable<string> AvaloniaControls() =>
        typeof(global::Avalonia.Controls.Button).Assembly.GetExportedTypes()
            .Where(type => typeof(global::Avalonia.Controls.Control).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericTypeDefinition)
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal);

    public static IEnumerable<string> Check(IEnumerable<string> controls, string matrix, IReadOnlySet<string> criteria)
    {
        var rows = Rows(matrix).ToList();
        var listed = rows.Select(row => row.Control).ToHashSet(StringComparer.Ordinal);
        foreach (var control in controls.Where(control => !listed.Contains(control)))
        {
            yield return $"{control}: not in the control matrix";
        }

        foreach (var row in rows)
        {
            foreach (var problem in Problems(row, criteria))
            {
                yield return $"{row.Control}: {problem}";
            }
        }
    }

    private static IEnumerable<string> Problems(Row row, IReadOnlySet<string> criteria)
    {
        if (row.Status == "todo")
        {
            yield return "not done yet";
        }
        else if (!Statuses.Contains(row.Status))
        {
            yield return $"unknown status '{row.Status}'";
        }

        if (row.Status == "done" && row.Criteria.Count == 0)
        {
            yield return "done without acceptance criteria";
        }

        if (row.Status == "n/a" && row.Web.Length == 0)
        {
            yield return "n/a without a reason";
        }

        foreach (var criterion in row.Criteria.Where(criterion => !criteria.Contains(criterion)))
        {
            yield return $"names {criterion}, which does not exist";
        }
    }

    private static IEnumerable<Row> Rows(string matrix) =>
        from line in matrix.Split('\n')
        let match = RowPattern().Match(line)
        where match.Success
        select new Row(
            match.Groups["control"].Value,
            match.Groups["web"].Value.Trim(),
            match.Groups["status"].Value.Trim(),
            [.. CriterionPattern().Matches(match.Groups["ac"].Value).Select(criterion => criterion.Value)]);

    [GeneratedRegex(@"^\|\s*`(?<control>[\w.]+)`\s*\|[^|]*\|(?<web>[^|]*)\|(?<status>[^|]*)\|(?<ac>[^|]*)\|\s*$")]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"[A-Z]+-\d{2,}")]
    private static partial Regex CriterionPattern();

    private sealed record Row(string Control, string Web, string Status, IReadOnlyList<string> Criteria);
}
